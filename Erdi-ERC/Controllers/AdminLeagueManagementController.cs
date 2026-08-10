using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Helpers;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace <OWNER_HANDLE>_ERC.Controllers
{
    /// <summary>Liga-Verwaltung (CRUD): Anlegen, Bearbeiten, Klonen, Archivieren, Löschen, Stats-Rebuild.</summary>
    [Authorize(Policy = "Admin")]
    public class AdminLeagueManagementController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IStatsService _statsService;
        private readonly IAdminAuditService _audit;
        private readonly IStaticDataCache _staticCache;

        public AdminLeagueManagementController(AppDbContext db, IStatsService statsService, IAdminAuditService audit, IStaticDataCache staticCache)
        {
            _db = db;
            _statsService = statsService;
            _audit = audit;
            _staticCache = staticCache;
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLeague(string? id, string name, string? description)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["AdminMessage"] = "Bitte einen Liga-Namen eingeben.";
                return RedirectToAction("Index", "Admin");
            }

            // ID ist optional — wenn leer, wird sie automatisch aus dem Namen abgeleitet (idiotensicher).
            var requestedId = Slugify(string.IsNullOrWhiteSpace(id) ? name : id);
            if (string.IsNullOrWhiteSpace(requestedId))
                requestedId = "liga";

            // Eindeutige ID sicherstellen — bei Kollision -2, -3 … anhängen.
            var existingIds = await _db.Leagues.Select(l => l.Id).ToListAsync();
            var existingSet = new HashSet<string>(existingIds, StringComparer.OrdinalIgnoreCase);
            var finalId = requestedId;
            var suffix = 2;
            while (existingSet.Contains(finalId))
            {
                finalId = $"{requestedId}-{suffix}";
                suffix++;
            }

            var maxSort = existingIds.Count == 0 ? 0 : await _db.Leagues.MaxAsync(l => l.SortOrder);
            _db.Leagues.Add(new League
            {
                Id = finalId,
                Name = name.Trim(),
                Description = description?.Trim() ?? "",
                SortOrder = maxSort + 1
            });
            await _db.SaveChangesAsync();
            _staticCache.InvalidateLeagues();
            await _audit.LogAsync("CreateLeague", "League", finalId, $"Name={name.Trim()}");
            TempData["AdminMessage"] = $"Liga '{name.Trim()}' angelegt (ID: {finalId}).";
            return RedirectToAction(nameof(EditLeague), new { id = finalId });
        }

        // ---- Liga-Reihenfolge auf dem Dashboard (Hoch/Runter) ----
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveLeague(string id, string direction)
        {
            var leagues = await _db.Leagues.AsTracking()
                .OrderBy(l => l.SortOrder)
                .ThenBy(l => l.Name)
                .ToListAsync();

            var index = leagues.FindIndex(l => l.Id == id);
            if (index < 0) return RedirectToAction("Index", "Admin");

            var target = string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase) ? index - 1 : index + 1;
            if (target < 0 || target >= leagues.Count)
                return RedirectToAction("Index", "Admin");

            // Reihenfolge zuerst sequentiell normalisieren (Default-Werte sind oft alle 0),
            // danach die beiden Nachbarn tauschen.
            for (int i = 0; i < leagues.Count; i++)
                leagues[i].SortOrder = i;
            (leagues[index].SortOrder, leagues[target].SortOrder) = (leagues[target].SortOrder, leagues[index].SortOrder);

            await _db.SaveChangesAsync();
            _staticCache.InvalidateLeagues();
            return RedirectToAction("Index", "Admin");
        }

        /// <summary>Erzeugt einen URL-sicheren Slug aus beliebigem Text (lowercase, Umlaute aufgelöst, nur a-z0-9 und Bindestriche).</summary>
        private static string Slugify(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            var lower = raw.Trim().ToLowerInvariant()
                .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");

            var sb = new System.Text.StringBuilder(lower.Length);
            var lastDash = false;
            foreach (var ch in lower)
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                {
                    sb.Append(ch);
                    lastDash = false;
                }
                else if (!lastDash && sb.Length > 0)
                {
                    sb.Append('-');
                    lastDash = true;
                }
            }

            return sb.ToString().Trim('-');
        }

        // ---- Liga duplizieren / Neue Saison starten ----
        // Übernimmt das Fahrer-Roster (Name/Nr/Team/Reserve) in eine neue Liga, setzt Punkte/Siege/Rennen zurück.
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CloneLeague(string sourceId, string newName, string? newId, bool archiveSource = false)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                TempData["AdminMessage"] = "Bitte einen Namen für die neue Liga/Saison eingeben.";
                return RedirectToAction(nameof(EditLeague), new { id = sourceId });
            }

            var source = await _db.Leagues
                .Include(l => l.Standings)
                .FirstOrDefaultAsync(l => l.Id == sourceId);
            if (source is null) return NotFound();

            // Eindeutige Ziel-ID ableiten.
            var requestedId = Slugify(string.IsNullOrWhiteSpace(newId) ? newName : newId);
            if (string.IsNullOrWhiteSpace(requestedId)) requestedId = "liga";
            var existingIds = await _db.Leagues.Select(l => l.Id).ToListAsync();
            var existingSet = new HashSet<string>(existingIds, StringComparer.OrdinalIgnoreCase);
            var finalId = requestedId;
            var suffix = 2;
            while (existingSet.Contains(finalId))
            {
                finalId = $"{requestedId}-{suffix}";
                suffix++;
            }

            var maxSort = existingIds.Count == 0 ? 0 : await _db.Leagues.MaxAsync(l => l.SortOrder);

            var newLeague = new League
            {
                Id = finalId,
                Name = newName.Trim(),
                Description = source.Description,
                SortOrder = maxSort + 1
            };
            _db.Leagues.Add(newLeague);

            // Roster kopieren — Punkte/Siege/Reserve-Statistik auf 0.
            foreach (var s in source.Standings.OrderBy(s => s.Position))
            {
                _db.DriverStandings.Add(new DriverStanding
                {
                    LeagueId = finalId,
                    Position = s.Position,
                    Driver = s.Driver,
                    Team = s.Team,
                    DriverNumber = s.DriverNumber,
                    Points = 0,
                    Wins = 0,
                    IsReserveDriver = s.IsReserveDriver,
                    ReserveForDriver = s.ReserveForDriver,
                    ReserveStarts = 0,
                    ReservePointsForMain = 0
                });
            }

            await _db.SaveChangesAsync();

            // Optional die alte Saison abschließen (inkl. .xlsx-Snapshot).
            if (archiveSource && !source.IsArchived)
            {
                var trackedSource = await _db.Leagues.AsTracking().FirstAsync(l => l.Id == sourceId);
                var archivedName = trackedSource.Name;
                trackedSource.IsArchived = true;
                trackedSource.ArchivedName = archivedName;
                trackedSource.ArchivedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                await ExportArchivedLeagueWorkbookAsync(sourceId, archivedName);
            }

            _staticCache.InvalidateLeagues();
            await _audit.LogAsync("CloneLeague", "League", finalId,
                $"From={sourceId}, Drivers={source.Standings.Count}, ArchivedSource={archiveSource}");
            TempData["AdminMessage"] = archiveSource
                ? $"Neue Saison '{newLeague.Name}' (ID: {finalId}) erstellt — {source.Standings.Count} Fahrer übernommen, alte Liga archiviert."
                : $"Liga '{newLeague.Name}' (ID: {finalId}) erstellt — {source.Standings.Count} Fahrer übernommen, Punkte zurückgesetzt.";
            return RedirectToAction(nameof(EditLeague), new { id = finalId });
        }

        [HttpGet]
        public async Task<IActionResult> EditLeague(string id)
        {
            var league = await _db.Leagues
                .Include(l => l.Standings)
                .Include(l => l.Races)
                    .ThenInclude(r => r.ReserveAssignments)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (league is null) return NotFound();

            league.Standings = league.Standings.OrderBy(s => s.Position).ToList();
            league.Races = league.Races.OrderByDescending(r => r.Date).ThenByDescending(r => r.RowId).ToList();

            ViewBag.RecentRaceUndos = await _db.RaceUndoEntries
                .Where(x => x.LeagueId == id && !x.IsUsed)
                .OrderByDescending(x => x.CreatedAt)
                .Take(10)
                .ToListAsync();

            return View("~/Views/Admin/EditLeague.cshtml", league);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateLeague(string id, string name, string? description, int sortOrder = 0, int? capacity = null, int? dropWorstResults = null, string? currentSeason = null, bool countsTowardOverall = true, bool acceptsApplications = false)
        {
            var league = await _db.Leagues.AsTracking().FirstOrDefaultAsync(l => l.Id == id);
            if (league is null) return NotFound();

            var oldName = league.Name;
            var newName = name?.Trim() ?? league.Name;

            league.Name = newName;
            league.Description = description?.Trim() ?? "";
            league.SortOrder = Math.Clamp(sortOrder, 0, 9999);
            // Kapazität: 0/negativ bedeutet "unbegrenzt" (null), sonst auf sinnvolles Maximum begrenzen.
            league.Capacity = capacity.HasValue && capacity.Value > 0 ? Math.Min(capacity.Value, 999) : (int?)null;
            // Streichresultate: 0/negativ = keine Streichung (null).
            league.DropWorstResults = dropWorstResults.HasValue && dropWorstResults.Value > 0 ? Math.Min(dropWorstResults.Value, 99) : (int?)null;
            // Aktuelle Saison (leer = alle Rennen zählen).
            league.CurrentSeason = string.IsNullOrWhiteSpace(currentSeason) ? null : currentSeason.Trim();
            // Opt-in Liga-übergreifende Constructors-Meisterschaft (Default true).
            league.CountsTowardOverall = countsTowardOverall;
            // Checkbox: unchecked wird nicht mitgesendet → false.
            league.AcceptsApplications = acceptsApplications;

            try
            {
                await _db.SaveChangesAsync();

                // Drop-Scores wirken auf die abgeleiteten Punkte → Tabelle neu berechnen.
                await _statsService.RebuildLeagueStandingsAsync(id);
                _staticCache.InvalidateLeagues();
                await _audit.LogAsync("UpdateLeague", "League", id,
                    $"Name={newName}, CountsTowardOverall={league.CountsTowardOverall}, DropWorst={league.DropWorstResults}");
                await _db.SaveChangesAsync();

                TempData["AdminMessage"] = $"Liga '{newName}' aktualisiert.";
            }
            catch (Exception ex)
            {
                TempData["AdminMessage"] = $"Fehler beim Speichern: {ex.Message}";
            }

            return RedirectToAction(nameof(EditLeague), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RenameLeagueId(string oldId, string newId)
        {
            newId = newId?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(newId) || newId.Length > 64)
            {
                TempData["AdminMessage"] = "Neue ID ungültig (leer oder zu lang).";
                return RedirectToAction(nameof(EditLeague), new { id = oldId });
            }
            if (newId == oldId)
            {
                TempData["AdminMessage"] = "Neue ID ist identisch mit der alten.";
                return RedirectToAction(nameof(EditLeague), new { id = oldId });
            }

            var league = await _db.Leagues.AsTracking().FirstOrDefaultAsync(l => l.Id == oldId);
            if (league is null) return NotFound();

            if (await _db.Leagues.AnyAsync(l => l.Id == newId))
            {
                TempData["AdminMessage"] = $"Liga-ID \"{newId}\" existiert bereits.";
                return RedirectToAction(nameof(EditLeague), new { id = oldId });
            }

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // 1. Neue Liga-Zeile mit neuer ID einfügen (alle anderen Felder 1:1 kopieren).
                _db.Leagues.Add(new League
                {
                    Id = newId,
                    Name = league.Name,
                    Description = league.Description,
                    ArchivedName = league.ArchivedName,
                    IsArchived = league.IsArchived,
                    ArchivedAt = league.ArchivedAt,
                });
                await _db.SaveChangesAsync();

                // 2. Alle FK- und String-Referenzen auf die neue ID umschreiben,
                //    bevor die alte Zeile gelöscht wird (vermeidet Cascade-DELETE).
                await _db.DriverStandings
                    .Where(x => x.LeagueId == oldId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeagueId, newId));

                await _db.RaceResults
                    .Where(x => x.LeagueId == oldId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeagueId, newId));

                await _db.RaceWeekendLegs
                    .Where(x => x.LeagueId == oldId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeagueId, newId));

                await _db.RaceUndoEntries
                    .Where(x => x.LeagueId == oldId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeagueId, newId));

                await _db.LeaguePenalties
                    .Where(x => x.LeagueId == oldId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeagueId, newId));

                await _db.DriverRoleHistories
                    .Where(x => x.LeagueId == oldId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeagueId, newId));

                await _db.CustomAchievements
                    .Where(x => x.LeagueId == oldId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeagueId, newId));

                // 3. Alte Liga-Zeile entfernen — hat jetzt keine Kinder mehr.
                _db.Leagues.Remove(league);
                await _db.SaveChangesAsync();

                await tx.CommitAsync();

                _staticCache.InvalidateLeagues();
                await _audit.LogAsync("RenameLeagueId", "League", newId, $"OldId={oldId}");
                await _db.SaveChangesAsync();

                TempData["AdminMessage"] = $"Liga-ID erfolgreich von \"{oldId}\" in \"{newId}\" umbenannt.";
                return RedirectToAction(nameof(EditLeague), new { id = newId });
            }
            catch (Exception ex)
            {
                try { await tx.RollbackAsync(); } catch { }
                TempData["AdminMessage"] = $"Fehler beim Umbenennen der ID: {ex.Message}";
                return RedirectToAction(nameof(EditLeague), new { id = oldId });
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLeague(string id)
        {
            var league = await _db.Leagues.FindAsync(id);
            if (league is null) return NotFound();

            // Lösch-Schutz: Eine aktive Liga mit Daten wird per Cascade samt Fahrern, Rennen,
            // Strafen usw. unwiderruflich entfernt. Das erlauben wir nur, wenn die Liga
            // archiviert (Snapshot in der Ewigen Liste gesichert) ODER bereits leer ist.
            if (!league.IsArchived)
            {
                var driverCount = await _db.DriverStandings.CountAsync(s => s.LeagueId == id);
                var raceCount = await _db.RaceResults.CountAsync(r => r.LeagueId == id);
                if (driverCount > 0 || raceCount > 0)
                {
                    TempData["AdminMessage"] =
                        $"„{league.Name}“ wurde NICHT gelöscht: noch {driverCount} Fahrer und {raceCount} Rennen vorhanden. " +
                        "Bitte die Liga zuerst abschließen (archivieren) oder über „Liga vollständig leeren“ leeren.";
                    return RedirectToAction("Index", "Admin");
                }
            }

            _db.Leagues.Remove(league);
            await _db.SaveChangesAsync();
            _staticCache.InvalidateLeagues();
            await _audit.LogAsync("DeleteLeague", "League", id, $"Name={league.Name}, Archived={league.IsArchived}");
            TempData["AdminMessage"] = $"Liga „{league.Name}“ gelöscht.";
            return RedirectToAction("Index", "Admin");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveLeague(string id, string? archivedName)
        {
            var league = await _db.Leagues.AsTracking()
                .FirstOrDefaultAsync(l => l.Id == id);
            if (league is null) return NotFound();

            var normalizedArchivedName = archivedName?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedArchivedName))
            {
                normalizedArchivedName = league.Name;
            }

            league.IsArchived = true;
            league.ArchivedName = normalizedArchivedName;
            league.ArchivedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            _staticCache.InvalidateLeagues();

            await ExportArchivedLeagueWorkbookAsync(league.Id, normalizedArchivedName);

            await _audit.LogAsync("ArchiveLeague", "League", id, $"ArchivedName={normalizedArchivedName}");
            TempData["AdminMessage"] = $"Liga '{league.Name}' wurde als abgeschlossen markiert und als .xlsx Snapshot aktualisiert.";
            return RedirectToAction(nameof(EditLeague), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UnarchiveLeague(string id)
        {
            var league = await _db.Leagues.AsTracking().FirstOrDefaultAsync(l => l.Id == id);
            if (league is null) return NotFound();

            league.IsArchived = false;
            league.ArchivedName = null;
            league.ArchivedAt = null;
            await _db.SaveChangesAsync();
            _staticCache.InvalidateLeagues();

            await _audit.LogAsync("UnarchiveLeague", "League", id, "League re-opened as active");
            TempData["AdminMessage"] = $"Liga '{league.Name}' ist wieder aktiv.";
            return RedirectToAction(nameof(EditLeague), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearLeagueData(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["AdminMessage"] = "Liga-ID fehlt.";
                return RedirectToAction("Index", "Admin");
            }

            var league = await _db.Leagues.FirstOrDefaultAsync(l => l.Id == id);
            if (league is null)
            {
                return NotFound();
            }

            if (league.IsArchived)
            {
                await ExportArchivedLeagueWorkbookAsync(league.Id, league.ArchivedName?.Trim() ?? league.Name);
            }

            var raceResults = await _db.RaceResults.Where(x => x.LeagueId == id).ToListAsync();
            var standings = await _db.DriverStandings.Where(x => x.LeagueId == id).ToListAsync();
            var legsForLeague = await _db.RaceWeekendLegs.Where(x => x.LeagueId == id).ToListAsync();
            var penalties = await _db.LeaguePenalties.Where(x => x.LeagueId == id).ToListAsync();
            var undoEntries = await _db.RaceUndoEntries.Where(x => x.LeagueId == id).ToListAsync();

            // Atomar: schlägt ein RemoveRange fehl, bleiben alle 5 Entitäten erhalten —
            // ein teilweiser Clear würde inkonsistente Ligen-Stände erzeugen.
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                if (raceResults.Count > 0) _db.RaceResults.RemoveRange(raceResults);
                if (standings.Count > 0) _db.DriverStandings.RemoveRange(standings);
                if (legsForLeague.Count > 0) _db.RaceWeekendLegs.RemoveRange(legsForLeague);
                if (penalties.Count > 0) _db.LeaguePenalties.RemoveRange(penalties);
                if (undoEntries.Count > 0) _db.RaceUndoEntries.RemoveRange(undoEntries);

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                try { await tx.RollbackAsync(); } catch { }
                await _audit.LogAndSaveAsync("ClearLeagueDataError", "League", id, ex.Message);
                TempData["AdminMessage"] = $"Fehler beim Leeren der Liga '{league.Name}': {ex.Message}";
                return RedirectToAction(nameof(EditLeague), new { id });
            }

            await _audit.LogAsync("ClearLeagueData", "League", id, $"Cleared all league data (Standings={standings.Count}, Races={raceResults.Count}, Legs={legsForLeague.Count}, Penalties={penalties.Count}, Undo={undoEntries.Count})");
            TempData["AdminMessage"] = league.IsArchived
                ? $"Liga '{league.Name}' wurde geleert. Archiv-Snapshot (.xlsx) wurde vorher aktualisiert."
                : $"Liga '{league.Name}' wurde vollständig geleert und ist bereit für neue Saison-Daten.";

            return RedirectToAction(nameof(EditLeague), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RebuildLeagueStats(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["AdminMessage"] = "Liga-ID fehlt für Rebuild.";
                return RedirectToAction("Index", "Admin");
            }

            await _statsService.RebuildLeagueStandingsAsync(id);
            await _audit.LogAsync("RebuildLeagueStats", "League", id, "Manual rebuild for single league");
            TempData["AdminMessage"] = $"Statistiken für Liga '{id}' wurden neu berechnet.";

            return RedirectToAction(nameof(EditLeague), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin.System")]
        public async Task<IActionResult> RebuildAllStats()
        {
            await _statsService.RebuildAllLeagueStandingsAsync();
            await _audit.LogAsync("RebuildAllStats", "League", "*", "Manual rebuild for all leagues");
            TempData["AdminMessage"] = "Alle Liga-Statistiken wurden neu berechnet.";
            return RedirectToAction("Index", "Admin");
        }

        // ---- Helpers ----
        private static string BuildWorksheetName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '[', ']', '*', '?', '/', '\\', ':' }).ToHashSet();
            var cleaned = new string((value ?? string.Empty).Where(c => !invalid.Contains(c)).ToArray()).Trim();
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                cleaned = "Liga";
            }

            return cleaned.Length <= 31 ? cleaned : cleaned[..31];
        }

        private static string ResolveTeamForExport(League league, RaceResult race, string? driverName)
        {
            if (string.IsNullOrWhiteSpace(driverName)) return "";
            var normalizedDriver = driverName.Trim();

            var raceMainDriver = race.ReserveAssignments
                .FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.ReserveDriver)
                                     && a.ReserveDriver.Trim().Equals(normalizedDriver, StringComparison.OrdinalIgnoreCase))?.MainDriver;

            if (!string.IsNullOrWhiteSpace(raceMainDriver))
            {
                var mainTeam = league.Standings.FirstOrDefault(s =>
                    !string.IsNullOrWhiteSpace(s.Driver)
                    && s.Driver.Trim().Equals(raceMainDriver.Trim(), StringComparison.OrdinalIgnoreCase))?.Team;

                if (!string.IsNullOrWhiteSpace(mainTeam))
                {
                    return mainTeam.Trim();
                }
            }

            var standing = league.Standings.FirstOrDefault(s =>
                !string.IsNullOrWhiteSpace(s.Driver)
                && s.Driver.Trim().Equals(normalizedDriver, StringComparison.OrdinalIgnoreCase));

            if (standing is null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(standing.Team))
            {
                return standing.Team.Trim();
            }

            if (standing.IsReserveDriver && !string.IsNullOrWhiteSpace(standing.ReserveForDriver))
            {
                var fallbackTeam = league.Standings.FirstOrDefault(s =>
                    !string.IsNullOrWhiteSpace(s.Driver)
                    && s.Driver.Trim().Equals(standing.ReserveForDriver.Trim(), StringComparison.OrdinalIgnoreCase))?.Team;

                return fallbackTeam?.Trim() ?? string.Empty;
            }

            return string.Empty;
        }

        private async Task ExportArchivedLeagueWorkbookAsync(string leagueId, string archivedName)
        {
            var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var league = await _db.Leagues
                .Include(l => l.Standings)
                .Include(l => l.Races).ThenInclude(r => r.Finishes)
                .Include(l => l.Races).ThenInclude(r => r.ReserveAssignments)
                .FirstOrDefaultAsync(l => l.Id == leagueId);

            if (league is null)
            {
                return;
            }

            var standings = league.Standings
                .Where(s => !string.IsNullOrWhiteSpace(s.Driver))
                .OrderByDescending(s => s.Points)
                .ThenByDescending(s => s.Wins)
                .ThenBy(s => s.Driver)
                .ToList();

            var races = league.Races
                .OrderBy(r => r.Date)
                .ThenBy(r => r.RowId)
                .ToList();

            var targetDir = Path.Combine(env.ContentRootPath, "data", "ewige");
            Directory.CreateDirectory(targetDir);
            var workbookPath = EwigeWorkbookHelper.GetPath(env);

            using var workbook = System.IO.File.Exists(workbookPath)
                ? new ClosedXML.Excel.XLWorkbook(workbookPath)
                : new ClosedXML.Excel.XLWorkbook();

            var driverSheetName = BuildWorksheetName($"{archivedName} - Fahrer");
            var teamSheetName = BuildWorksheetName($"{archivedName} - Teams");

            if (workbook.Worksheets.Any(x => x.Name == driverSheetName))
            {
                workbook.Worksheet(driverSheetName).Delete();
            }

            if (workbook.Worksheets.Any(x => x.Name == teamSheetName))
            {
                workbook.Worksheet(teamSheetName).Delete();
            }

            var driverSheet = workbook.Worksheets.Add(driverSheetName);
            driverSheet.Cell(1, 1).Value = "Position";
            driverSheet.Cell(1, 2).Value = "Fahrer";
            driverSheet.Cell(1, 3).Value = "Team";
            driverSheet.Cell(1, 4).Value = "Reserve For";
            driverSheet.Cell(1, 5).Value = "Punkte";
            driverSheet.Cell(1, 6).Value = "Siege";
            driverSheet.Cell(1, 7).Value = "Rennen";

            for (int i = 0; i < standings.Count; i++)
            {
                var s = standings[i];
                var row = i + 2;
                var raceCount = races.Count(r => r.Finishes.Any(f => !string.IsNullOrWhiteSpace(f.Driver) && f.Driver.Trim().Equals(s.Driver.Trim(), StringComparison.OrdinalIgnoreCase)));

                driverSheet.Cell(row, 1).Value = i + 1;
                driverSheet.Cell(row, 2).Value = s.Driver;
                driverSheet.Cell(row, 3).Value = s.Team;
                driverSheet.Cell(row, 4).Value = s.IsReserveDriver ? (s.ReserveForDriver ?? "") : "";
                driverSheet.Cell(row, 5).Value = s.Points;
                driverSheet.Cell(row, 6).Value = s.Wins;
                driverSheet.Cell(row, 7).Value = raceCount;
            }

            var teamRows = races
                .SelectMany(r => r.Finishes.Select(f => new { Race = r, Finish = f }))
                .Where(x => x.Finish.Position > 0)
                .Select(x => new
                {
                    Team = ResolveTeamForExport(league, x.Race, x.Finish.Driver),
                    Position = x.Finish.Position
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Team))
                .GroupBy(x => x.Team, StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    Team = g.Key,
                    Wins = g.Count(x => x.Position == 1),
                    Podiums = g.Count(x => x.Position is 1 or 2 or 3),
                    Punkte = g.Sum(x =>
                    {
                        var idx = x.Position - 1;
                        return idx >= 0 && idx < 15
                            ? new[] { 25, 21, 18, 16, 14, 12, 10, 8, 7, 6, 5, 4, 3, 2, 1 }[idx]
                            : 0;
                    })
                })
                .OrderByDescending(x => x.Punkte)
                .ThenByDescending(x => x.Wins)
                .ThenBy(x => x.Team)
                .ToList();

            var teamSheet = workbook.Worksheets.Add(teamSheetName);
            teamSheet.Cell(1, 1).Value = "Position";
            teamSheet.Cell(1, 2).Value = "Team";
            teamSheet.Cell(1, 3).Value = "Punkte";
            teamSheet.Cell(1, 4).Value = "Podiums";
            teamSheet.Cell(1, 5).Value = "Siege";

            for (int i = 0; i < teamRows.Count; i++)
            {
                var row = i + 2;
                teamSheet.Cell(row, 1).Value = i + 1;
                teamSheet.Cell(row, 2).Value = teamRows[i].Team;
                teamSheet.Cell(row, 3).Value = teamRows[i].Punkte;
                teamSheet.Cell(row, 4).Value = teamRows[i].Podiums;
                teamSheet.Cell(row, 5).Value = teamRows[i].Wins;
            }

            driverSheet.Columns().AdjustToContents();
            teamSheet.Columns().AdjustToContents();
            workbook.SaveAs(workbookPath);
        }
    }
}
