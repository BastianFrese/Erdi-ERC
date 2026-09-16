using Erdi_ERC.Data;
using Erdi_ERC.Helpers;
using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Erdi_ERC.Controllers
{
    [Authorize(Policy = "Admin.League")]
    public class AdminLeagueController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IAdminAuditService _audit;
        private readonly IDriverProfileService _driverProfiles;
        private readonly IWebhookAutomationService _webhookAuto;
        private readonly IStatsService _stats;
        private readonly IStaticDataCache _staticCache;

        public AdminLeagueController(
            AppDbContext db,
            IAdminAuditService audit,
            IDriverProfileService driverProfiles,
            IWebhookAutomationService webhookAuto,
            IStatsService stats,
            IStaticDataCache staticCache)
        {
            _db = db;
            _audit = audit;
            _driverProfiles = driverProfiles;
            _webhookAuto = webhookAuto;
            _stats = stats;
            _staticCache = staticCache;
        }

        /// <summary>
        /// Berechnet die Tabelle dieser Liga aus den Renn-Ergebnissen neu und invalidiert den
        /// Liga-Cache, damit öffentliche Seiten sofort aktuell sind. Wird nach jeder
        /// daten­ändernden Aktion (Rennen/Standings) aufgerufen, damit Punkte nie veralten.
        /// </summary>
        private async Task RecalculateAsync(string leagueId)
        {
            await _stats.RebuildLeagueStandingsAsync(leagueId);
            _staticCache.InvalidateLeagues();
        }

        // ---- Standings ----
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveStanding(int? rowId, string leagueId, int position, string driver, string? team, int? driverNumber, int pointsAdjustment = 0, bool isReserveDriver = false, string? reserveForDriver = null)
        {
            DriverStanding? entity = rowId.HasValue
                ? await _db.DriverStandings.AsTracking().FirstOrDefaultAsync(x => x.RowId == rowId.Value)
                : null;
            if (entity is null)
            {
                entity = new DriverStanding { LeagueId = leagueId };
                _db.DriverStandings.Add(entity);
            }

            var normalizedDriver = driver?.Trim() ?? string.Empty;
            var normalizedReserveFor = string.IsNullOrWhiteSpace(reserveForDriver) ? null : reserveForDriver.Trim();

            if (isReserveDriver
                && !string.IsNullOrWhiteSpace(normalizedDriver)
                && !string.IsNullOrWhiteSpace(normalizedReserveFor)
                && normalizedDriver.Equals(normalizedReserveFor, StringComparison.OrdinalIgnoreCase))
            {
                TempData["AdminMessage"] = "Ein Reservefahrer kann nicht für sich selbst eingetragen werden.";
                return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = leagueId });
            }

            var normalizedDriverLower = normalizedDriver.ToLowerInvariant();
            var duplicateNameExists = await _db.DriverStandings.AnyAsync(x =>
                x.LeagueId == leagueId
                && x.RowId != entity.RowId
                && !x.IsReserveDriver
                && x.Driver != null
                && x.Driver.Trim().ToLower() == normalizedDriverLower);

            if (!isReserveDriver && duplicateNameExists)
            {
                TempData["AdminMessage"] = $"Stammfahrer '{normalizedDriver}' ist in dieser Liga bereits eingetragen.";
                return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = leagueId });
            }

            if (driverNumber.HasValue)
            {
                var duplicateNumberExists = await _db.DriverStandings.AnyAsync(x =>
                    x.LeagueId == leagueId
                    && x.RowId != entity.RowId
                    && x.DriverNumber == driverNumber.Value);

                if (duplicateNumberExists)
                {
                    TempData["AdminMessage"] = $"Die Fahrernummer {driverNumber.Value} ist in dieser Liga bereits vergeben.";
                    return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = leagueId });
                }
            }

            var normalizedTeam = team?.Trim() ?? "";

            if (isReserveDriver && !string.IsNullOrWhiteSpace(normalizedReserveFor))
            {
                var leagueStandings = await _db.DriverStandings
                    .Where(x => x.LeagueId == leagueId && !string.IsNullOrWhiteSpace(x.Driver))
                    .ToListAsync();

                var reserveForStanding = leagueStandings.FirstOrDefault(x =>
                    x.Driver.Trim().Equals(normalizedReserveFor, StringComparison.OrdinalIgnoreCase));

                if (reserveForStanding is null)
                {
                    TempData["AdminMessage"] = $"Hauptfahrer '{normalizedReserveFor}' wurde in dieser Liga nicht gefunden.";
                    return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = leagueId });
                }

                if (reserveForStanding.IsReserveDriver)
                {
                    TempData["AdminMessage"] = $"'{normalizedReserveFor}' ist selbst als Reserve markiert. Bitte einen Stammfahrer wählen.";
                    return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = leagueId });
                }

                if (!string.IsNullOrWhiteSpace(reserveForStanding.Team))
                {
                    normalizedTeam = reserveForStanding.Team.Trim();
                }
            }

            var oldName = entity.Driver?.Trim() ?? string.Empty;
            entity.Position = position;
            entity.Driver = normalizedDriver;
            entity.Team = normalizedTeam;
            entity.DriverNumber = driverNumber;
            // Punkte/Siege werden aus den Rennen abgeleitet (read-only in der UI);
            // nur die manuelle Korrektur wird hier übernommen.
            entity.PointsAdjustment = pointsAdjustment;
            entity.IsReserveDriver = isReserveDriver;
            entity.ReserveForDriver = isReserveDriver ? normalizedReserveFor : null;

            await _db.SaveChangesAsync();

            // Systemweite Umbenennung: nur wenn der alte Name zu einem DriverProfile
            // aufgelöst werden kann (Fahrerkarte, andere Ligen, Renn-Ergebnisse).
            if (oldName.Length > 0
                && !string.Equals(oldName, normalizedDriver, StringComparison.OrdinalIgnoreCase))
            {
                var actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var changed = await _driverProfiles.RenameStandingDriverAsync(oldName, normalizedDriver, actorId);
                if (changed > 0)
                {
                    await _audit.LogAsync("SaveStandingRename", "DriverProfile", oldName,
                        $"'{oldName}' → '{normalizedDriver}', {changed} Referenz(en) aktualisiert");
                }
            }

            await RecalculateAsync(leagueId);
            await _audit.LogAsync("SaveStanding", "DriverStanding", entity.RowId.ToString(),
                $"League={leagueId}, Driver={entity.Driver}, Number={entity.DriverNumber}, Reserve={entity.IsReserveDriver}, Adj={pointsAdjustment}");

            return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = leagueId });
        }

        [HttpGet]
        public async Task<IActionResult> DriverSuggestions(string q, string? leagueId)
        {
            var query = q ?? string.Empty;

            // First gather league-specific suggestions from DriverStandings (if leagueId provided)
            var results = new List<DriverNameSuggestion>();
            if (!string.IsNullOrWhiteSpace(leagueId) && !string.IsNullOrWhiteSpace(query))
            {
                var fromStandings = await _db.DriverStandings
                    .Where(s => s.LeagueId == leagueId && !string.IsNullOrWhiteSpace(s.Driver) && s.Driver.Contains(query))
                    .OrderBy(s => s.Driver)
                    .Select(s => new DriverNameSuggestion(
                        DiscordId: string.Empty,
                        Platform: string.Empty,
                        GamerTag: s.Driver,
                        DiscordName: s.Driver,
                        Distance: 0,
                        ExactMatch: true))
                    .Take(10)
                    .ToListAsync();

                results.AddRange(fromStandings);
            }

            // Then add profile suggestions (fuzzy + tags)
            var profileMatches = await _driverProfiles.SuggestAsync(query);
            foreach (var m in profileMatches)
            {
                if (!results.Any(r => string.Equals(r.GamerTag, m.GamerTag, StringComparison.OrdinalIgnoreCase)))
                    results.Add(m);
            }

            return Json(results.Select(m => new
            {
                discordId = m.DiscordId,
                platform = m.Platform,
                gamerTag = m.GamerTag,
                discordName = m.DiscordName,
                exact = m.ExactMatch,
                distance = m.Distance
            }));
        }

        // ---- Bulk-Import: Fahrerliste per Textarea ----
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkAddStandings(string leagueId, string? bulk)
        {
            if (string.IsNullOrWhiteSpace(leagueId)) return RedirectToAction("Index", "Admin");

            var league = await _db.Leagues.AnyAsync(l => l.Id == leagueId);
            if (!league) return NotFound();

            if (string.IsNullOrWhiteSpace(bulk))
            {
                TempData["AdminMessage"] = "Keine Fahrer eingegeben.";
                return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = leagueId });
            }

            var existing = await _db.DriverStandings
                .Where(s => s.LeagueId == leagueId)
                .Select(s => new { s.Driver, s.DriverNumber })
                .ToListAsync();

            var existingNames = new HashSet<string>(
                existing.Where(e => !string.IsNullOrWhiteSpace(e.Driver)).Select(e => e.Driver.Trim()),
                StringComparer.OrdinalIgnoreCase);
            var usedNumbers = new HashSet<int>(existing.Where(e => e.DriverNumber.HasValue).Select(e => e.DriverNumber!.Value));
            var nextPosition = existing.Count + 1;

            int added = 0;
            var skipped = new List<string>();

            // Trennzeichen: Semikolon, Komma oder Tab. Format pro Zeile: Name [; Nr] [; Team]
            var separators = new[] { ';', ',', '\t' };
            foreach (var rawLine in bulk.Replace("\r\n", "\n").Split('\n'))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(separators, StringSplitOptions.TrimEntries);
                var name = parts.Length > 0 ? parts[0].Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (existingNames.Contains(name))
                {
                    skipped.Add($"{name} (bereits vorhanden)");
                    continue;
                }

                int? number = null;
                if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out var parsedNumber)
                    && parsedNumber >= 0 && parsedNumber <= 999)
                {
                    if (usedNumbers.Contains(parsedNumber))
                    {
                        skipped.Add($"{name} (Nr. {parsedNumber} schon vergeben → ohne Nummer angelegt)");
                    }
                    else
                    {
                        number = parsedNumber;
                        usedNumbers.Add(parsedNumber);
                    }
                }

                var team = string.Empty;
                if (parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]))
                {
                    var matched = Helpers.F1TeamsHelper.GetTeamByName(parts[2].Trim());
                    if (matched is not null) team = matched.Name;
                }

                _db.DriverStandings.Add(new DriverStanding
                {
                    LeagueId = leagueId,
                    Position = nextPosition++,
                    Driver = name,
                    Team = team,
                    DriverNumber = number,
                    Points = 0,
                    Wins = 0
                });
                existingNames.Add(name);
                added++;
            }

            if (added > 0)
            {
                await _db.SaveChangesAsync();
                await RecalculateAsync(leagueId);
            }
            await _audit.LogAsync("BulkAddStandings", "League", leagueId, $"Added={added}, Skipped={skipped.Count}");

            var msg = $"{added} Fahrer importiert.";
            if (skipped.Count > 0) msg += $" Übersprungen: {string.Join(", ", skipped.Take(10))}{(skipped.Count > 10 ? " …" : "")}";
            TempData["AdminMessage"] = msg;
            return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = leagueId });
        }

        // ---- Positionen rein nach Punkten neu nummerieren (ohne Renn-Neuberechnung) ----
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResortStandings(string leagueId)
        {
            if (string.IsNullOrWhiteSpace(leagueId)) return RedirectToAction("Index", "Admin");

            var standings = await _db.DriverStandings.AsTracking()
                .Where(s => s.LeagueId == leagueId)
                .ToListAsync();

            if (standings.Count == 0)
            {
                TempData["AdminMessage"] = "Keine Fahrer zum Sortieren vorhanden.";
                return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = leagueId });
            }

            // F1-Tiebreaker wie beim Rebuild: Punkte, dann meiste bessere Positionen.
            // Wichtig: dieselbe Saison-Filterung wie RebuildLeagueStandingsAsync, sonst
            // mischen sich Positions-Zähler aus alten Saisons in die aktuelle Wertung.
            var currentSeason = await _db.Leagues
                .Where(l => l.Id == leagueId)
                .Select(l => l.CurrentSeason)
                .FirstOrDefaultAsync();
            var finishesQuery = _db.RaceResults.Where(r => r.LeagueId == leagueId);
            if (!string.IsNullOrWhiteSpace(currentSeason))
                finishesQuery = finishesQuery.Where(r => r.Season == currentSeason);
            var finishes = await finishesQuery
                .SelectMany(r => r.Finishes)
                .ToListAsync();
            var positionCounts = StandingsRankingHelper.BuildPositionCounts(finishes);
            var ranked = StandingsRankingHelper.Rank(standings, positionCounts);

            for (int i = 0; i < ranked.Count; i++)
                ranked[i].Position = i + 1;

            await _db.SaveChangesAsync();
            await _audit.LogAsync("ResortStandings", "League", leagueId, $"Renumbered={ranked.Count}");
            TempData["AdminMessage"] = $"{ranked.Count} Fahrer nach Punkten & besten Positionen neu nummeriert.";
            return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = leagueId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStanding(int rowId)
        {
            var entity = await _db.DriverStandings.FindAsync(rowId);
            if (entity is null) return NotFound();
            var leagueId = entity.LeagueId;
            _db.DriverStandings.Remove(entity);
            await _db.SaveChangesAsync();
            await RecalculateAsync(leagueId);
            await _audit.LogAsync("DeleteStanding", "DriverStanding", rowId.ToString(), $"League={leagueId}");
            return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = leagueId });
        }

        // ---- Sammel-Speichern aller Standings (Anlegen + Ändern + Löschen in einem Durchgang) ----
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAllStandings(
            string leagueId,
            string[]? rowId, string[]? driver, string[]? driverNumber, string[]? team,
            string[]? isReserveDriver, string[]? reserveForDriver, string[]? pointsAdjustment,
            int[]? deleteRowIds)
        {
            if (string.IsNullOrWhiteSpace(leagueId)) return RedirectToAction("Index", "Admin");
            if (!await _db.Leagues.AnyAsync(l => l.Id == leagueId)) return NotFound();

            var existing = await _db.DriverStandings.AsTracking()
                .Where(s => s.LeagueId == leagueId)
                .ToListAsync();
            var byRowId = existing.ToDictionary(s => s.RowId);
            var deleteSet = (deleteRowIds ?? Array.Empty<int>()).ToHashSet();

            var n = driver?.Length ?? 0;

            // 1) Geplanten Endzustand zusammenstellen — für Duplikat-Prüfung VOR dem Schreiben.
            var planned = new List<(int RowId, string Driver, int? Number, bool IsReserve)>();
            for (int i = 0; i < n; i++)
            {
                var parsedRowId = ParseInt(rowId?.ElementAtOrDefault(i)) ?? 0;
                if (parsedRowId > 0 && deleteSet.Contains(parsedRowId)) continue;

                var name = (driver?.ElementAtOrDefault(i) ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name)) continue; // leere (neue) Zeile überspringen

                var isReserve = string.Equals(isReserveDriver?.ElementAtOrDefault(i), "true", StringComparison.OrdinalIgnoreCase);
                planned.Add((parsedRowId, name, ParseInt(driverNumber?.ElementAtOrDefault(i)), isReserve));
            }

            // 2) Duplikate hart abfangen (idiotensicher) — Stammfahrer-Namen + Nummern.
            var dupName = planned.Where(p => !p.IsReserve)
                .GroupBy(p => p.Driver, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);
            if (dupName is not null)
            {
                TempData["AdminMessage"] = $"Stammfahrer '{dupName.Key}' ist mehrfach eingetragen — bitte eindeutig machen. Nichts gespeichert.";
                return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = leagueId });
            }
            var dupNumber = planned.Where(p => p.Number.HasValue)
                .GroupBy(p => p.Number!.Value)
                .FirstOrDefault(g => g.Count() > 1);
            if (dupNumber is not null)
            {
                TempData["AdminMessage"] = $"Fahrernummer {dupNumber.Key} ist mehrfach vergeben — bitte eindeutig machen. Nichts gespeichert.";
                return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = leagueId });
            }

            // 3) Anwenden: Löschen, Ändern, Anlegen.
            int deleted = 0, updated = 0, created = 0;
            var renames = new List<(string Old, string New)>();
            foreach (var delId in deleteSet)
            {
                if (byRowId.TryGetValue(delId, out var toDelete))
                {
                    _db.DriverStandings.Remove(toDelete);
                    deleted++;
                }
            }

            for (int i = 0; i < n; i++)
            {
                var parsedRowId = ParseInt(rowId?.ElementAtOrDefault(i)) ?? 0;
                if (parsedRowId > 0 && deleteSet.Contains(parsedRowId)) continue;

                var name = (driver?.ElementAtOrDefault(i) ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;

                var isReserve = string.Equals(isReserveDriver?.ElementAtOrDefault(i), "true", StringComparison.OrdinalIgnoreCase);
                var reserveFor = (reserveForDriver?.ElementAtOrDefault(i) ?? string.Empty).Trim();
                if (!isReserve || (reserveFor.Length > 0 && reserveFor.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    reserveFor = string.Empty;

                DriverStanding entity;
                if (parsedRowId > 0 && byRowId.TryGetValue(parsedRowId, out var existingEntity))
                {
                    entity = existingEntity;
                    updated++;
                    var oldName = existingEntity.Driver?.Trim() ?? string.Empty;
                    if (oldName.Length > 0
                        && !string.Equals(oldName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        renames.Add((oldName, name));
                    }
                }
                else
                {
                    entity = new DriverStanding { LeagueId = leagueId };
                    _db.DriverStandings.Add(entity);
                    created++;
                }

                entity.Driver = name;
                entity.Team = (team?.ElementAtOrDefault(i) ?? string.Empty).Trim();
                entity.DriverNumber = ParseInt(driverNumber?.ElementAtOrDefault(i));
                entity.PointsAdjustment = ParseInt(pointsAdjustment?.ElementAtOrDefault(i)) ?? 0;
                entity.IsReserveDriver = isReserve;
                entity.ReserveForDriver = isReserve && reserveFor.Length > 0 ? reserveFor : null;
            }

            // Swap-/Ketten-Umbenennung in einem Submit verhindern: Wenn der neue Name
            // einer Zeile dem alten Namen einer anderen Zeile entspricht, wäre die
            // Referenz-Propagation mehrdeutig (Rename 1 überschreibt den Eingang von
            // Rename 2). Bitte Fahrer einzeln umbenennen.
            var renameTargets = renames.Select(r => r.New).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (renames.Any(r => renameTargets.Contains(r.Old)))
            {
                TempData["AdminMessage"] = "Umbenennung abgebrochen: Der neue Name einer Zeile entspricht dem alten Namen einer anderen Zeile (Tausch/Kette). Bitte Fahrer einzeln umbenennen.";
                return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = leagueId });
            }

            await _db.SaveChangesAsync();

            // Systemweite Umbenennung: nur wenn der alte Name zu einem DriverProfile
            // aufgelöst werden kann (Fahrerkarte, andere Ligen, Renn-Ergebnisse).
            // Läuft VOR RecalculateAsync, damit Standings-Punkte aus bereits
            // umbenannten Finishes abgeleitet werden.
            int renamed = 0;
            if (renames.Count > 0)
            {
                var actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                // Alle Profile VOR der ersten Umbenennung auflösen (Snapshot): Nach der
                // ersten Umbenennung wäre der alte Name bereits überschrieben und würde
                // zum falschen Profil auflösen.
                var resolved = new List<(string DiscordId, string OldName, string NewName)>();
                foreach (var (oldName, newName) in renames)
                {
                    var profile = await _driverProfiles.FindByDriverNameAsync(oldName);
                    if (profile is not null) resolved.Add((profile.DiscordId, oldName, newName));
                }

                foreach (var (discordId, oldName, newName) in resolved)
                {
                    var changed = await _driverProfiles.RenameIngameNameAsync(discordId, newName, actorId);
                    renamed++;
                    await _audit.LogAsync("SaveAllStandingsRename", "DriverProfile", oldName,
                        $"'{oldName}' → '{newName}', {changed} Referenz(en) aktualisiert");
                }
            }

            await RecalculateAsync(leagueId); // Punkte + Positionen neu ableiten
            await _audit.LogAsync("SaveAllStandings", "League", leagueId,
                $"Created={created}, Updated={updated}, Deleted={deleted}");

            var renameNote = renamed > 0 ? $" {renamed} Fahrer systemweit umbenannt." : "";
            TempData["AdminMessage"] = $"Fahrerliste gespeichert — {created} neu, {updated} geändert, {deleted} gelöscht. Punkte & Positionen neu berechnet.{renameNote}";
            return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = leagueId });
        }

        private static int? ParseInt(string? raw)
            => int.TryParse((raw ?? string.Empty).Trim(), out var v) ? v : null;

        // ---- Race Entry ----
        [HttpGet]
        public async Task<IActionResult> EnterRace(string leagueId)
        {
            if (string.IsNullOrEmpty(leagueId))
                return RedirectToAction("Index", "Admin");

            var league = await _db.Leagues
                .Include(l => l.Standings)
                .FirstOrDefaultAsync(l => l.Id == leagueId);

            if (league is null) return NotFound();
            return View("~/Views/Admin/EnterRace.cshtml", league);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveEnteredRace(
            string leagueId, DateTime date, string track, string? fastestLapDriver,
            string[]? positions, string[]? raceTimes, string[]? penaltySeconds,
            string[]? dnfDrivers, string[]? reserveDrivers, string[]? reserveMainDrivers,
            GuestAssignmentInput[]? guestAssignments = null,
            string[]? qualiPositions = null)
        {
            if (string.IsNullOrWhiteSpace(leagueId) || string.IsNullOrWhiteSpace(track))
            {
                TempData["RaceError"] = "Liga und Strecke sind erforderlich.";
                return RedirectToAction(nameof(EnterRace), new { leagueId });
            }

            var league = await _db.Leagues.Include(l => l.Standings).FirstOrDefaultAsync(l => l.Id == leagueId);
            if (league is null) return NotFound();

            if (await TryValidateGuestAssignmentsAsync(positions, guestAssignments, league))
            {
                return RedirectToAction(nameof(EnterRace), new { leagueId });
            }

            var race = new RaceResult
            {
                LeagueId = leagueId,
                Date = date,
                Track = track.Trim(),
                FastestLap = fastestLapDriver?.Trim() ?? string.Empty,
                Winner = string.Empty,
                // Neues Rennen automatisch der aktuellen Saison der Liga zuordnen (falls gesetzt).
                Season = string.IsNullOrWhiteSpace(league.CurrentSeason) ? null : league.CurrentSeason
            };

            _db.RaceResults.Add(race);
            await _db.SaveChangesAsync();

            var resolvedGuests = await ApplyRaceEntriesAsync(race, fastestLapDriver, positions, raceTimes, penaltySeconds, dnfDrivers, reserveDrivers, reserveMainDrivers, guestAssignments, qualiPositions);
            await _db.SaveChangesAsync();
            await RecalculateAsync(leagueId);

            foreach (var g in resolvedGuests)
            {
                await _audit.LogAsync(
                    "GuestTeamResolved",
                    "RaceGuestAssignment",
                    $"{race.RowId}/{g.Guest}",
                    $"Guest={g.Guest} → Main={g.Main}");
            }

            await _audit.LogAsync("SaveEnteredRace", "RaceResult", race.RowId.ToString(),
                $"League={leagueId}, Track={track}, Date={date:yyyy-MM-dd}, Winner={race.Winner}");

            TempData["AdminMessage"] = $"Rennergebnis für {track} gespeichert. Punkte neu berechnet.";
            await _webhookAuto.FireAsync(WebhookEvents.RaceResultSaved, new()
            {
                ["League"]     = leagueId,
                ["Track"]      = track,
                ["Date"]       = date.ToString("dd.MM.yyyy"),
                ["Winner"]     = race.Winner,
                ["FastestLap"] = race.FastestLap ?? "",
            });
            return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = leagueId });
        }

        // ---- Bestehendes Rennen voll bearbeiten ----
        [HttpGet]
        public async Task<IActionResult> EditRace(int rowId)
        {
            var race = await _db.RaceResults
                .Include(r => r.Finishes)
                .Include(r => r.ReserveAssignments)
                .FirstOrDefaultAsync(r => r.RowId == rowId);
            if (race is null) return NotFound();

            var league = await _db.Leagues
                .Include(l => l.Standings)
                .FirstOrDefaultAsync(l => l.Id == race.LeagueId);
            if (league is null) return NotFound();

            race.Finishes = race.Finishes.OrderBy(f => f.Position == 0 ? int.MaxValue : f.Position).ToList();
            ViewBag.ExistingRace = race;
            return View("~/Views/Admin/EnterRace.cshtml", league);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEnteredRace(
            int rowId, string leagueId, DateTime date, string track, string? fastestLapDriver,
            string[]? positions, string[]? raceTimes, string[]? penaltySeconds,
            string[]? dnfDrivers, string[]? reserveDrivers, string[]? reserveMainDrivers,
            GuestAssignmentInput[]? guestAssignments = null,
            string[]? qualiPositions = null)
        {
            if (string.IsNullOrWhiteSpace(leagueId) || string.IsNullOrWhiteSpace(track))
            {
                TempData["RaceError"] = "Liga und Strecke sind erforderlich.";
                return RedirectToAction(nameof(EditRace), new { rowId });
            }

            var race = await _db.RaceResults.AsTracking()
                .Include(r => r.Finishes)
                .Include(r => r.ReserveAssignments)
                .Include(r => r.GuestAssignments)
                .FirstOrDefaultAsync(r => r.RowId == rowId);
            if (race is null) return NotFound();

            var league = await _db.Leagues.Include(l => l.Standings).FirstOrDefaultAsync(l => l.Id == leagueId);
            if (league is null) return NotFound();

            if (await TryValidateGuestAssignmentsAsync(positions, guestAssignments, league))
            {
                return RedirectToAction(nameof(EditRace), new { rowId });
            }

            // Bestehende Detail-Datensätze ersetzen (sauberster Weg für eine vollständige Korrektur).
            _db.RaceFinishes.RemoveRange(race.Finishes);
            _db.RaceReserveAssignments.RemoveRange(race.ReserveAssignments);
            _db.RaceGuestAssignments.RemoveRange(race.GuestAssignments);

            race.Date = date;
            race.Track = track.Trim();
            var resolvedGuests = await ApplyRaceEntriesAsync(race, fastestLapDriver, positions, raceTimes, penaltySeconds, dnfDrivers, reserveDrivers, reserveMainDrivers, guestAssignments, qualiPositions);

            await _db.SaveChangesAsync();
            await RecalculateAsync(leagueId);
            await _audit.LogAsync("UpdateEnteredRace", "RaceResult", race.RowId.ToString(),
                $"League={leagueId}, Track={race.Track}, Date={date:yyyy-MM-dd}, Winner={race.Winner}");

            // Audit für jede persistierte Gast-Zuordnung — NACH dem SaveChanges, damit
            // ein Throw in RecalculateAsync den Audit nicht verschluckt (siehe
            // [[project_audit_persistence]] Konvention).
            foreach (var g in resolvedGuests)
            {
                await _audit.LogAsync(
                    "GuestTeamResolved",
                    "RaceGuestAssignment",
                    $"{race.RowId}/{g.Guest}",
                    $"Guest={g.Guest} → Main={g.Main}");
            }

            TempData["AdminMessage"] = $"Rennergebnis für {race.Track} aktualisiert. Punkte neu berechnet.";
            return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = leagueId });
        }

        /// <summary>Anfrage-DTO für <see cref="ParseRaceCsv"/> (JSON-Body).</summary>
        public sealed record ParseRaceCsvRequest(string? Csv);

        /// <summary>
        /// JSON-Endpoint für den CSV-Rennimport in <c>EnterRace</c>. Parst den eingefügten
        /// Textblock und liefert pro Position Fahrer, Team sowie die aus Siegerzeit + Gap
        /// berechnete <b>Gesamt</b>zeit. Versteht automatisch zwei Formate: den F1-Spiel-Export
        /// und den Semicolon-Export der ERDi-Telemetrie (Gesamtzeit inkl. Strafsekunden,
        /// Grid→Quali, DNF aus resultStatus). Reine Auswertung — schreibt nichts in die DB.
        /// Der Antiforgery-Token kommt per <c>RequestVerificationToken</c>-Header (ASP.NET-Default),
        /// da der Body JSON statt Formular-Daten ist.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult ParseRaceCsv([FromBody] ParseRaceCsvRequest request)
        {
            var result = RaceCsvParser.Parse(request?.Csv);
            return Json(new
            {
                entries = result.Entries
                    .Select(e => new
                    {
                        position = e.Position,
                        driver = e.Driver,
                        team = e.Team,
                        totalTimeMs = e.TotalTimeMs,
                        isDnf = e.IsDnf,
                        lappedText = e.LappedText,
                        qualiPosition = e.QualifyingPosition
                    }),
                skippedLines = result.SkippedLines,
                fastestLapDriver = result.FastestLapDriver,
                error = result.Error
            });
        }

        /// <summary>
        /// Baut Reserve-Zuordnungen, Gast-Zuordnungen und Zieleinläufe für ein (bereits gespeichertes) Rennen aus den
        /// Formular-Arrays auf, setzt Sieger und schnellste Runde. Strafzeit (Sek.) wird zur Rennzeit
        /// addiert; DNF-Fahrer erhalten Position 0. Der Aufrufer ruft anschließend SaveChanges.
        /// </summary>
        /// <remarks>
        /// <para>Audit-Hook: Gibt die tatsächlich persistierten (kanonisierten) Gast-Zuordnungen
        /// zurück, damit der Controller den Audit-Log erst NACH <c>SaveChangesAsync</c> schreiben
        /// kann (siehe <see cref="project_audit_persistence"/>).</para>
        /// </remarks>
        private async Task<List<(string Guest, string Main)>> ApplyRaceEntriesAsync(
            RaceResult race, string? fastestLapDriver,
            string[]? positions, string[]? raceTimes, string[]? penaltySeconds,
            string[]? dnfDrivers, string[]? reserveDrivers, string[]? reserveMainDrivers,
            GuestAssignmentInput[]? guestAssignments = null,
            string[]? qualiPositions = null,
            CancellationToken ct = default)
        {
            var fastestLap = fastestLapDriver?.Trim() ?? string.Empty;
            race.Winner = string.Empty;
            race.FastestLap = fastestLap;
            var resolvedGuests = new List<(string Guest, string Main)>();

            if (reserveDrivers != null && reserveMainDrivers != null)
            {
                for (int i = 0; i < Math.Min(reserveDrivers.Length, reserveMainDrivers.Length); i++)
                {
                    if (!string.IsNullOrWhiteSpace(reserveDrivers[i]) && !string.IsNullOrWhiteSpace(reserveMainDrivers[i]))
                    {
                        _db.RaceReserveAssignments.Add(new RaceReserveAssignment
                        {
                            RaceResultId = race.RowId,
                            ReserveDriver = reserveDrivers[i].Trim(),
                            MainDriver = reserveMainDrivers[i].Trim()
                        });
                    }
                }
            }

            // Gast-Zuordnungen (Cross-League): jeder Datensatz ist ein explizit vom Admin
            // gewähltes (Guest, Main)-Paar — keine implizite Position-Index-Kopplung.
            // Pflicht: jeder Cross-League-Gast MUSS einem Liga-Hauptfahrer ODER dem Sentinel
            // "(kein Hauptfahrer)" zugeordnet werden.
            // Name wird kanonisch über DriverProfileService.ResolveAsync aufgelöst — verhindert
            // Drift zwischen RaceFinish.Driver und DriverProfile.GamerTag (Mick Schumaher → Mick Schumacher).
            // Sentinel-Mapping: "__sentinel__" (HTML-Sentinel aus EnterRace.cshtml) →
            //   StatsService.GuestSentinelNoMain (DB-Sentinel, identisch zu Migrations-Backfill).
            if (guestAssignments != null)
            {
                foreach (var ga in guestAssignments)
                {
                    if (ga is null) continue;
                    var rawGuest = (ga.GuestName ?? string.Empty).Trim();
                    var rawMain  = (ga.MainDriver ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(rawGuest)) continue;
                    if (string.IsNullOrWhiteSpace(rawMain)) continue; // Validation hat das bereits abgefangen

                    var match = await _driverProfiles.ResolveAsync(rawGuest, ct);
                    if (!match.ExactMatch) continue;
                    var guestName = match.ResolvedName;

                    var mainName = rawMain == "__sentinel__"
                        ? StatsService.GuestSentinelNoMain
                        : rawMain;

                    _db.RaceGuestAssignments.Add(new RaceGuestAssignment
                    {
                        RaceResultId = race.RowId,
                        GuestDriver = guestName,
                        MainDriver = mainName
                    });

                    resolvedGuests.Add((guestName, mainName));
                }
            }

            var dnfSet = (dnfDrivers ?? Array.Empty<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (positions != null)
            {
                for (int i = 0; i < positions.Length; i++)
                {
                    var driver = positions[i];
                    if (string.IsNullOrWhiteSpace(driver)) continue;
                    var pos = i + 1;
                    var isDnf = dnfSet.Contains(driver);
                    var timeMs = ParseRaceTimeMs(raceTimes?.ElementAtOrDefault(i));
                    // penaltySeconds/qualiPositions kommen als string[] an: leere
                    // Zeilen posten "" und BLEIBEN im Array. Ein int[] wuerde vom
                    // MVC-Model-Binding komprimiert — Strafsekunden/Quali-Positionen
                    // waeren dann auf den falschen Fahrer verschoben oder komplett weg.
                    var penalty = ParseSignedInt(penaltySeconds?.ElementAtOrDefault(i));
                    // Negative Werte = Minus-Strafe (Zeit-Abzug). Zeit kann nicht negativ werden → mind. 1 ms.
                    if (penalty != 0 && timeMs.HasValue) timeMs = Math.Max(1, timeMs.Value + (penalty * 1000));

                    var qualiRaw = ParseNonNegativeInt(qualiPositions?.ElementAtOrDefault(i));
                    int? qualiPos = qualiRaw > 0 ? qualiRaw : (int?)null;

                    _db.RaceFinishes.Add(new RaceFinish
                    {
                        RaceResultId = race.RowId,
                        Driver = driver.Trim(),
                        Position = isDnf ? 0 : pos,
                        FastestLap = driver.Trim().Equals(fastestLap, StringComparison.OrdinalIgnoreCase),
                        RaceTimeMs = timeMs,
                        QualifyingPosition = qualiPos
                    });

                    if (pos == 1 && !isDnf) race.Winner = driver.Trim();
                }
            }

            return resolvedGuests;
        }

        /// <summary>
        /// Validiert die Gast-Zuordnungen für ein Rennen:
        /// - Jeder <see cref="GuestAssignmentInput"/> muss einen Guest-Namen UND einen
        ///   MainDriver (Liga-Hauptfahrer ODER Sentinel) haben.
        /// - Der Gast-Name MUSS einem registrierten Fahrerprofil exakt entsprechen
        ///   (case-insensitive, trim). Verhindert Tippfehler + halluzinierte Namen,
        ///   die sonst über die Team-Punkte-Berechnung Ghost-Einträge erzeugen.
        /// - Der MainDriver MUSS in der Liga als Stammfahrer (kein Reserve) existieren.
        /// - Der MainDriver darf nicht identisch mit dem Gast sein.
        /// Returns true + Fehlermeldung, falls Validation fehlschlaegt.
        /// </summary>
        private async Task<bool> TryValidateGuestAssignmentsAsync(
            string[]? positions, GuestAssignmentInput[]? guestAssignments, League league,
            CancellationToken ct = default)
        {
            var mainDrivers = new HashSet<string>(
                league.Standings
                    .Where(s => !s.IsReserveDriver && !string.IsNullOrWhiteSpace(s.Driver))
                    .Select(s => s.Driver.Trim()),
                StringComparer.OrdinalIgnoreCase);

            const string sentinelMarker = "__sentinel__";

            if (guestAssignments != null)
            {
                foreach (var ga in guestAssignments)
                {
                    if (ga is null) continue;
                    var guestName = (ga.GuestName ?? string.Empty).Trim();
                    var mainName  = (ga.MainDriver ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(guestName)) continue;

                    // Pflicht: Cross-League-Gast braucht entweder Liga-Hauptfahrer oder
                    // den expliziten Sentinel ("(kein Hauptfahrer)").
                    if (string.IsNullOrWhiteSpace(mainName))
                    {
                        TempData["RaceError"] =
                            $"Gast '{guestName}' braucht einen Liga-Hauptfahrer — oder explizit \"(kein Hauptfahrer)\" als Sentinel setzen.";
                        return true;
                    }

                    // Sentinel: nur prüfen, dass der Name selbst bekannt ist (kein MainDriver-Standing-Check).
                    if (string.Equals(mainName, sentinelMarker, StringComparison.OrdinalIgnoreCase))
                    {
                        var match = await _driverProfiles.ResolveAsync(guestName, ct);
                        if (!match.ExactMatch)
                        {
                            var hint = match.Suggestions.Count > 0
                                ? $" Meinten Sie vielleicht '{match.Suggestions[0].GamerTag}'?"
                                : " Bitte zuerst in der Fahrerverwaltung registrieren.";
                            TempData["RaceError"] = $"Gast '{guestName}' ist kein bekannter Fahrer.{hint}";
                            return true;
                        }
                        continue;
                    }

                    // Strikte Name-Validierung: Gast muss in DriverProfile/GamerTag existieren.
                    var matchFull = await _driverProfiles.ResolveAsync(guestName, ct);
                    if (!matchFull.ExactMatch)
                    {
                        var hint = matchFull.Suggestions.Count > 0
                            ? $" Meinten Sie vielleicht '{matchFull.Suggestions[0].GamerTag}'?"
                            : " Bitte zuerst in der Fahrerverwaltung registrieren.";
                        TempData["RaceError"] = $"Gast '{guestName}' ist kein bekannter Fahrer.{hint}";
                        return true;
                    }

                    if (!mainDrivers.Contains(mainName))
                    {
                        TempData["RaceError"] = $"Gast '{guestName}' wurde dem Hauptfahrer '{mainName}' zugeordnet — '{mainName}' ist in dieser Liga kein Stammfahrer.";
                        return true;
                    }

                    if (string.Equals(mainName, guestName, StringComparison.OrdinalIgnoreCase))
                    {
                        TempData["RaceError"] = $"Gast '{guestName}' kann nicht sich selbst als Hauptfahrer haben.";
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Form-Binding für eine einzelne Gast-Zuordnung. Razor rendert das Feld-Pärchen
        /// als <c>name="guestAssignments[i].GuestName"</c> / <c>name="guestAssignments[i].MainDriver"</c>,
        /// ASP.NET bindet es automatisch auf diese Klasse. Vorteil gegenüber zwei
        /// parallelen <c>string[]</c> (Positions + Mains): die Source-of-Truth ist das
        /// Paar selbst, kein impliziter Index — auch dann konsistent, wenn JS-Reihenfolge
        /// und Form-Position-Index auseinanderlaufen.
        /// </summary>
        public class GuestAssignmentInput
        {
            public string GuestName { get; set; } = string.Empty;
            public string MainDriver { get; set; } = string.Empty;
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRace(int rowId, string leagueId, DateTime date, string track, string? winner = null, string? fastestLap = null)
        {
            var race = await _db.RaceResults.AsTracking().FirstOrDefaultAsync(x => x.RowId == rowId);
            if (race is null) return NotFound();
            race.Date = date;
            race.Track = track?.Trim() ?? race.Track;
            race.Winner = string.IsNullOrWhiteSpace(winner) ? race.Winner : winner.Trim();
            race.FastestLap = fastestLap?.Trim() ?? string.Empty;
            await _db.SaveChangesAsync();
            await RecalculateAsync(leagueId);
            await _audit.LogAsync("SaveRace", "RaceResult", rowId.ToString(), $"League={leagueId}, Track={race.Track}, Winner={race.Winner}");
            return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = leagueId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRace(int rowId, string leagueId)
        {
            var race = await _db.RaceResults
                .Include(r => r.Finishes)
                .Include(r => r.ReserveAssignments)
                .FirstOrDefaultAsync(r => r.RowId == rowId);

            if (race is null) return NotFound();

            var realLeagueId = race.LeagueId;

            // Vollständigen Snapshot sichern, damit die Löschung wirklich rückgängig gemacht werden kann.
            var payload = JsonSerializer.Serialize(new RaceUndoPayload
            {
                Race = new RaceResultSnapshot
                {
                    RowId = race.RowId,
                    LeagueId = race.LeagueId,
                    Date = race.Date,
                    Track = race.Track,
                    Winner = race.Winner,
                    FastestLap = race.FastestLap,
                    Season = race.Season
                },
                Finishes = race.Finishes
                    .Select(f => new RaceFinishSnapshot
                    {
                        Driver = f.Driver,
                        Position = f.Position,
                        FastestLap = f.FastestLap,
                        RaceTimeMs = f.RaceTimeMs,
                        QualifyingPosition = f.QualifyingPosition
                    }).ToList(),
                ReserveAssignments = race.ReserveAssignments
                    .Select(r => new RaceReserveAssignmentSnapshot
                    {
                        ReserveDriver = r.ReserveDriver,
                        MainDriver = r.MainDriver
                    }).ToList()
            });

            _db.RaceUndoEntries.Add(new RaceUndoEntry
            {
                LeagueId = realLeagueId,
                OriginalRaceId = race.RowId,
                Actor = User.Identity?.Name ?? "Admin",
                PayloadJson = payload,
                CreatedAt = DateTime.UtcNow
            });

            _db.RaceFinishes.RemoveRange(race.Finishes);
            _db.RaceReserveAssignments.RemoveRange(race.ReserveAssignments);
            _db.RaceResults.Remove(race);
            await _db.SaveChangesAsync();
            await RecalculateAsync(realLeagueId);
            await _audit.LogAsync("DeleteRace", "RaceResult", rowId.ToString(), $"League={realLeagueId}, Track={race.Track}");
            return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = realLeagueId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UndoDeleteRace(long undoId)
        {
            var undo = await _db.RaceUndoEntries.AsTracking()
                .FirstOrDefaultAsync(x => x.Id == undoId && !x.IsUsed);

            if (undo is null)
            {
                TempData["AdminMessage"] = "Kein Undo-Eintrag gefunden oder bereits verwendet.";
                return RedirectToAction("Index", "Admin");
            }

            var leagueId = undo.LeagueId;

            RaceUndoPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<RaceUndoPayload>(undo.PayloadJson);
            }
            catch (JsonException)
            {
                payload = null;
            }

            if (payload is null)
            {
                TempData["AdminMessage"] = "Undo-Daten konnten nicht gelesen werden — Rennen wurde NICHT wiederhergestellt.";
                return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = leagueId });
            }

            // Rennen aus dem Snapshot neu aufbauen (neue RowId, identischer Inhalt).
            var restored = new RaceResult
            {
                LeagueId = leagueId,
                Date = payload.Race.Date,
                Track = payload.Race.Track,
                Winner = payload.Race.Winner,
                FastestLap = payload.Race.FastestLap,
                Season = payload.Race.Season
            };
            _db.RaceResults.Add(restored);
            await _db.SaveChangesAsync();

            foreach (var f in payload.Finishes)
            {
                _db.RaceFinishes.Add(new RaceFinish
                {
                    RaceResultId = restored.RowId,
                    Driver = f.Driver,
                    Position = f.Position,
                    FastestLap = f.FastestLap,
                    RaceTimeMs = f.RaceTimeMs,
                    QualifyingPosition = f.QualifyingPosition
                });
            }
            foreach (var r in payload.ReserveAssignments)
            {
                if (string.IsNullOrWhiteSpace(r.ReserveDriver) || string.IsNullOrWhiteSpace(r.MainDriver)) continue;
                _db.RaceReserveAssignments.Add(new RaceReserveAssignment
                {
                    RaceResultId = restored.RowId,
                    ReserveDriver = r.ReserveDriver,
                    MainDriver = r.MainDriver
                });
            }

            undo.IsUsed = true;
            undo.UsedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await RecalculateAsync(leagueId);

            await _audit.LogAsync("UndoDeleteRace", "RaceResult", restored.RowId.ToString(),
                $"League={leagueId}, Track={restored.Track}, RestoredFrom=Undo#{undo.Id}");
            TempData["AdminMessage"] = $"Rennen '{restored.Track}' wiederhergestellt. Punkte neu berechnet.";
            return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = leagueId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ValidateReserveData(string leagueId)
        {
            var standings = await _db.DriverStandings
                .Where(s => s.LeagueId == leagueId && s.IsReserveDriver)
                .ToListAsync();

            var issues = new List<string>();
            foreach (var s in standings)
            {
                if (string.IsNullOrWhiteSpace(s.ReserveForDriver))
                    issues.Add($"{s.Driver}: kein Hauptfahrer eingetragen");
            }

            TempData["AdminMessage"] = issues.Count == 0
                ? "Alle Reservefahrer-Daten sind valide."
                : $"Probleme gefunden: {string.Join("; ", issues)}";

            return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = leagueId });
        }

        /// <summary>
        /// Parst die EINGETRAGENE Gesamt-Rennzeit (gesamt gefahrene Zeit des Fahrers,
        /// keine Rundenzeit) in Millisekunden.
        /// Unterstuetzte Formate: H:MM:SS.mmm, M:SS.mmm oder SS.mmm.
        /// </summary>
        /// <summary>
        /// Delegiert an <see cref="RaceTimeParser.ParseMs"/> (Single Source of Truth für
        /// Zeit-Formate, auch vom CSV-Import genutzt — kein Drift-Risiko mehr). Der
        /// int-Cast genügt: reale Rennzeiten liegen weit unter 24 h (= 86,4 Mio. ms
        /// &lt; int.MaxValue).
        /// </summary>
        private static int? ParseRaceTimeMs(string? raw)
            => (int?)RaceTimeParser.ParseMs(raw);

        /// <summary>
        /// Formular-Eingaben (leer = "") tolerance parsen: nichtnumerisch/negativ → 0.
        /// </summary>
        private static int ParseNonNegativeInt(string? raw)
            => int.TryParse(raw, out var v) && v > 0 ? v : 0;

        /// <summary>
        /// Vorzeichenbehaftete Strafsekunden parsen (leer = 0). Negative Werte sind
        /// zulässig und bedeuten eine Minus-Strafe (Zeit-Abzug von der Gesamtzeit).
        /// </summary>
        private static int ParseSignedInt(string? raw)
            => int.TryParse(raw, out var v) ? v : 0;
    }
}
