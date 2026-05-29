using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace <OWNER_HANDLE>_ERC.Controllers
{
    [Authorize(Policy = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IStatsService _statsService;
        private readonly IAdminAuditService _audit;
        private readonly IConfiguration _config;
        private readonly IStaticDataCache _staticCache;

        public AdminController(AppDbContext db, IStatsService statsService, IAdminAuditService audit, IConfiguration config, IStaticDataCache staticCache)
        {
            _db = db;
            _statsService = statsService;
            _audit = audit;
            _config = config;
            _staticCache = staticCache;
        }

        private async Task<StreamSchedule?> GetNextStreamScheduleAsync()
        {
            var schedules = await _db.StreamSchedules.ToListAsync();
            if (schedules.Count == 0) return null;

            // UtcNow konsistent zum Save-Pfad in AdminCommunityController, der recurring StartAt
            // mit UTC berechnet. Mischen würde sonst zu 1-2h Drift (Sommer-/Winterzeit) führen.
            var now = DateTime.UtcNow;

            return schedules
                .Select(x =>
                {
                    var nextStart = x.IsRecurring && x.DayOfWeek.HasValue && x.TimeOfDay.HasValue
                        ? ComputeNextOccurrence(x.DayOfWeek.Value, x.TimeOfDay.Value, now)
                        : x.StartAt;

                    return new StreamSchedule
                    {
                        Id = x.Id,
                        Title = x.Title,
                        Url = x.Url,
                        DurationMinutes = x.DurationMinutes,
                        IsRecurring = x.IsRecurring,
                        DayOfWeek = x.DayOfWeek,
                        TimeOfDay = x.TimeOfDay,
                        StartAt = nextStart,
                        CreatedAt = x.CreatedAt
                    };
                })
                .Where(x => x.StartAt >= now)
                .OrderBy(x => x.StartAt)
                .FirstOrDefault();
        }

        private static DateTime ComputeNextOccurrence(int dayOfWeek, TimeSpan timeOfDay, DateTime from)
        {
            var daysUntil = ((dayOfWeek - (int)from.DayOfWeek) + 7) % 7;
            var candidate = from.Date.AddDays(daysUntil).Add(timeOfDay);
            if (candidate < from)
            {
                candidate = candidate.AddDays(7);
            }

            return candidate;
        }

        // ---- Dashboard / Main Übersicht ----
        [HttpGet]
        [Route("admin")]
        [Route("admin/index")]
        public async Task<IActionResult> Index()
        {
            var leagues = await _db.Leagues
                .OrderBy(l => l.Name)
                .ToListAsync();

            ViewBag.OpenApplications = await _db.ApplicationForms.CountAsync(x => !x.IsAccepted);
            ViewBag.AcceptedApplications = await _db.ApplicationForms.CountAsync(x => x.IsAccepted);
            ViewBag.TotalRaces = await _db.RaceResults.CountAsync();
            ViewBag.TotalDrivers = await _db.DriverStandings.Select(s => s.Driver).Distinct().CountAsync();
            ViewBag.AuditCount24h = await _db.AdminAuditLogs.CountAsync(x => x.CreatedAt >= DateTime.UtcNow.AddHours(-24));
            ViewBag.NextStream = await GetNextStreamScheduleAsync();
            ViewBag.RecentEaNameChanges = await _db.AdminAuditLogs
                .Where(x => x.Action == "UpdateEaName")
                .OrderByDescending(x => x.CreatedAt)
                .Take(8)
                .ToListAsync();

            return View(leagues);
        }

        // ---- Audit Logs ----
        [HttpGet]
        [Authorize(Policy = "Admin.System.AuditLogs")]
        public async Task<IActionResult> AuditLogs()
        {
            var logs = await _db.AdminAuditLogs
                .OrderByDescending(x => x.CreatedAt)
                .Take(500)
                .ToListAsync();

            return View(logs);
        }

        // ---- Leagues (CRUD) ----
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLeague(string id, string name, string? description)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                return BadRequest("Id und Name sind erforderlich.");

            if (await _db.Leagues.AnyAsync(l => l.Id == id))
                return BadRequest("Liga-Id existiert bereits.");

            _db.Leagues.Add(new League { Id = id.Trim(), Name = name.Trim(), Description = description?.Trim() ?? "" });
            await _db.SaveChangesAsync();
            _staticCache.InvalidateLeagues();
            await _audit.LogAsync("CreateLeague", "League", id.Trim(), $"Name={name?.Trim()}");
            return RedirectToAction(nameof(EditLeague), new { id });
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

            return View(league);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateLeague(string id, string name, string? description)
        {
            var league = await _db.Leagues.FindAsync(id);
            if (league is null) return NotFound();
            league.Name = name?.Trim() ?? league.Name;
            league.Description = description?.Trim() ?? "";
            await _db.SaveChangesAsync();
            _staticCache.InvalidateLeagues();
            await _audit.LogAsync("UpdateLeague", "League", id, $"Name={league.Name}");
            return RedirectToAction(nameof(EditLeague), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLeague(string id)
        {
            var league = await _db.Leagues.FindAsync(id);
            if (league is null) return NotFound();
            _db.Leagues.Remove(league);
            await _db.SaveChangesAsync();
            _staticCache.InvalidateLeagues();
            await _audit.LogAsync("DeleteLeague", "League", id, $"Name={league.Name}");
            return RedirectToAction(nameof(Index));
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
            var league = await _db.Leagues.FindAsync(id);
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
                return RedirectToAction(nameof(Index));
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

            if (raceResults.Count > 0) _db.RaceResults.RemoveRange(raceResults);
            if (standings.Count > 0) _db.DriverStandings.RemoveRange(standings);
            if (legsForLeague.Count > 0) _db.RaceWeekendLegs.RemoveRange(legsForLeague);
            if (penalties.Count > 0) _db.LeaguePenalties.RemoveRange(penalties);
            if (undoEntries.Count > 0) _db.RaceUndoEntries.RemoveRange(undoEntries);

            await _db.SaveChangesAsync();

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
                return RedirectToAction(nameof(Index));
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
            return RedirectToAction(nameof(Index));
        }

        // ---- Helpers ----
        private string GetEwigeWorkbookPath()
        {
            var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
            return Path.Combine(env.ContentRootPath, "data", "ewige", "active.xlsx");
        }

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
            var workbookPath = GetEwigeWorkbookPath();

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

        // ---- Admins & Berechtigungen (nur Superadmins) ----
        [HttpGet]
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> Admins()
        {
            var admins = await _db.AdminUsers
                .Include(x => x.Permissions)
                .OrderBy(x => x.DisplayName)
                .ToListAsync();
            return View(admins);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> AddAdmin(string discordId, string? displayName, bool isSuperAdmin = false)
        {
            if (string.IsNullOrWhiteSpace(discordId))
            {
                TempData["AdminMessage"] = "Discord-Id ist erforderlich.";
                return RedirectToAction(nameof(Admins));
            }

            var trimmedId = discordId.Trim();
            if (!await _db.AdminUsers.AnyAsync(x => x.DiscordId == trimmedId))
            {
                _db.AdminUsers.Add(new AdminUser
                {
                    DiscordId = trimmedId,
                    DisplayName = displayName?.Trim(),
                    AddedAt = DateTime.UtcNow,
                    IsSuperAdmin = isSuperAdmin
                });
                await _db.SaveChangesAsync();
                await _audit.LogAsync("AddAdmin", "AdminUser", trimmedId,
                    $"DisplayName={displayName?.Trim()}, SuperAdmin={isSuperAdmin}");
                TempData["AdminMessage"] = "Admin hinzugefügt.";
            }
            else
            {
                TempData["AdminMessage"] = "Discord-Id ist bereits registriert.";
            }

            return RedirectToAction(nameof(Admins));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> UpdateAdmin(string discordId, string? displayName, bool isSuperAdmin = false)
        {
            var admin = await _db.AdminUsers.FindAsync(discordId);
            if (admin is null)
            {
                TempData["AdminMessage"] = "Admin nicht gefunden.";
                return RedirectToAction(nameof(Admins));
            }

            admin.DisplayName = displayName?.Trim();
            admin.IsSuperAdmin = isSuperAdmin;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("UpdateAdmin", "AdminUser", discordId,
                $"DisplayName={displayName?.Trim()}, SuperAdmin={isSuperAdmin}");
            TempData["AdminMessage"] = "Admin aktualisiert.";
            return RedirectToAction(nameof(Admins));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> SetPermissions(string discordId, List<string> permissions)
        {
            var admin = await _db.AdminUsers
                .Include(x => x.Permissions)
                .FirstOrDefaultAsync(x => x.DiscordId == discordId);

            if (admin is null)
            {
                TempData["AdminMessage"] = "Admin nicht gefunden.";
                return RedirectToAction(nameof(Admins));
            }

            // Superadmins brauchen keine expliziten Einzelberechtigungen
            if (admin.IsSuperAdmin)
            {
                TempData["AdminMessage"] = "Superadmins haben automatisch alle Rechte. Einzelne Berechtigungen werden nicht gespeichert.";
                return RedirectToAction(nameof(Admins));
            }

            var validPerms = AdminPermissions.All.Select(p => p.Key).ToHashSet();
            var requested = permissions.Where(p => validPerms.Contains(p)).ToHashSet();

            // Entfernen
            var toRemove = admin.Permissions.Where(p => !requested.Contains(p.Permission)).ToList();
            _db.AdminUserPermissions.RemoveRange(toRemove);

            // Hinzufügen
            var existing = admin.Permissions.Select(p => p.Permission).ToHashSet();
            foreach (var perm in requested.Where(p => !existing.Contains(p)))
            {
                _db.AdminUserPermissions.Add(new AdminUserPermission
                {
                    DiscordId = discordId,
                    Permission = perm
                });
            }

            await _db.SaveChangesAsync();
            await _audit.LogAsync("SetPermissions", "AdminUser", discordId,
                $"Permissions={string.Join(",", requested)}");
            TempData["AdminMessage"] = "Berechtigungen gespeichert.";
            return RedirectToAction(nameof(Admins));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> RemoveAdmin(string discordId)
        {
            var admin = await _db.AdminUsers.FindAsync(discordId);
            if (admin is not null)
            {
                _db.AdminUsers.Remove(admin);
                await _db.SaveChangesAsync();
                await _audit.LogAsync("RemoveAdmin", "AdminUser", discordId, $"DisplayName={admin.DisplayName}");
                TempData["AdminMessage"] = "Admin entfernt.";
            }

            return RedirectToAction(nameof(Admins));
        }

        // ---- Track Setups (Admin) ----
        [HttpGet]
        [Authorize(Policy = "Admin.System.Setups")]
        public async Task<IActionResult> TrackSetups()
        {
            var setups = await _db.TrackSetups
                .OrderBy(x => x.Track)
                .ThenByDescending(x => x.CreatedAt)
                .ToListAsync();

            ViewBag.RoleMappings = await _db.SetupAccessRoleMappings.ToListAsync();

            var setupOptions = _config.GetSection("DiscordSetupAccess");
            ViewBag.SetupAccessGuildId = setupOptions["GuildId"];
            ViewBag.SetupAccessGuildConfigured = !string.IsNullOrWhiteSpace(setupOptions["GuildId"]);
            ViewBag.SetupAccessMappingsReady = true;
            ViewBag.SetupAccessMappingsCount = await _db.SetupAccessRoleMappings.CountAsync();
            ViewBag.SetEditorConfig = <OWNER_HANDLE>_ERC.Models.SetupGameSpec.GetEditorConfig();
            ViewBag.SetupMetricConfig = <OWNER_HANDLE>_ERC.Models.SetupGameSpec.GetMetricConfig();
            ViewBag.F1Tracks = <OWNER_HANDLE>_ERC.Models.F1RaceCatalog.Tracks;
            ViewBag.F1RaceLengths = <OWNER_HANDLE>_ERC.Models.F1RaceCatalog.Lengths;

            return View(setups);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTrackSetup(int? id, string track, string title, int requiredAccessTier, string? requiredRoleLabel, string? setupInfo, string setupText, string? strategy)
        {
            if (string.IsNullOrWhiteSpace(track) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(setupText))
            {
                TempData["AdminMessage"] = "Strecke, Titel und Setup-Daten sind Pflicht.";
                return RedirectToAction(nameof(TrackSetups));
            }

            TrackSetup? entity = id.HasValue && id.Value > 0
                ? await _db.TrackSetups.FindAsync(id.Value)
                : null;

            if (entity is null)
            {
                entity = new TrackSetup { CreatedAt = DateTime.UtcNow };
                _db.TrackSetups.Add(entity);
            }

            entity.Track = track.Trim();
            entity.Title = title.Trim();
            entity.RequiredAccessTier = requiredAccessTier;
            entity.RequiredRoleLabel = string.IsNullOrWhiteSpace(requiredRoleLabel) ? null : requiredRoleLabel.Trim();
            entity.SetupInfo = string.IsNullOrWhiteSpace(setupInfo) ? null : setupInfo.Trim();
            entity.SetupText = setupText.Trim();
            entity.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await _audit.LogAsync("SaveTrackSetup", "TrackSetup", entity.Id.ToString(), $"Track={entity.Track}, Tier={entity.RequiredAccessTier}");
            TempData["AdminMessage"] = "Setup gespeichert.";
            return RedirectToAction(nameof(TrackSetups));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTrackSetup(int id)
        {
            var setup = await _db.TrackSetups.FindAsync(id);
            if (setup is not null)
            {
                _db.TrackSetups.Remove(setup);
                await _db.SaveChangesAsync();
                await _audit.LogAsync("DeleteTrackSetup", "TrackSetup", id.ToString(), $"Track={setup.Track}");
                TempData["AdminMessage"] = "Setup gelöscht.";
            }

            return RedirectToAction(nameof(TrackSetups));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSetupAccessRoleMapping(int? id, int tier, string roleId, string? label)
        {
            if (string.IsNullOrWhiteSpace(roleId))
            {
                TempData["AdminMessage"] = "Role ID ist erforderlich.";
                return RedirectToAction(nameof(TrackSetups));
            }

            if (tier < 3 || tier > 5)
            {
                TempData["AdminMessage"] = "Tier muss zwischen 3 und 5 liegen (Twitch-Sub-Tiers).";
                return RedirectToAction(nameof(TrackSetups));
            }

            SetupAccessRoleMapping? entity = id.HasValue && id.Value > 0
                ? await _db.SetupAccessRoleMappings.FindAsync(id.Value)
                : null;

            if (entity is null)
            {
                entity = new SetupAccessRoleMapping { CreatedAt = DateTime.UtcNow };
                _db.SetupAccessRoleMappings.Add(entity);
            }

            entity.Tier = tier;
            entity.RoleId = roleId.Trim();
            entity.Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();

            await _db.SaveChangesAsync();
            await _audit.LogAsync("SaveSetupAccessRoleMapping", "SetupAccessRoleMapping", entity.Id.ToString(), $"Tier={tier}, RoleId={roleId}");
            TempData["AdminMessage"] = "Role-Mapping gespeichert.";
            return RedirectToAction(nameof(TrackSetups));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSetupAccessRoleMapping(int id)
        {
            var mapping = await _db.SetupAccessRoleMappings.FindAsync(id);
            if (mapping is not null)
            {
                _db.SetupAccessRoleMappings.Remove(mapping);
                await _db.SaveChangesAsync();
                await _audit.LogAsync("DeleteSetupAccessRoleMapping", "SetupAccessRoleMapping", id.ToString(), $"Tier={mapping.Tier}");
                TempData["AdminMessage"] = "Mapping gelöscht.";
            }

            return RedirectToAction(nameof(TrackSetups));
        }

        [HttpGet]
        [HttpPost]
        public IActionResult TrackSetupStrategy(string? trackKey, string? lengthKey, string? strategyPlan)
        {
            var track  = F1RaceCatalog.Tracks.FirstOrDefault(t => t.Key == trackKey);
            var length = F1RaceCatalog.Lengths.FirstOrDefault(l => l.Key == lengthKey);

            if (track == null || length == null)
                return Json(new { error = "Strecke oder Renn-Länge nicht gefunden." });

            var plan = F1RaceCatalog.BuildPlan(track, length);
            var text = string.IsNullOrWhiteSpace(strategyPlan) ? plan.ToHumanReadable() : strategyPlan.Trim();

            return Json(new
            {
                text,
                trackName   = plan.TrackName,
                lengthLabel = plan.LengthLabel,
                totalLaps   = plan.TotalLaps,
                stops       = plan.Stops
            });
        }

        // ---- Ewige Liste Upload ----
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadEwigeListe(IFormFile? workbook)
        {
            if (workbook is null || workbook.Length == 0)
            {
                TempData["AdminMessage"] = "Bitte eine .xlsx Datei auswählen.";
                return RedirectToAction(nameof(Index));
            }

            var ext = Path.GetExtension(workbook.FileName).ToLowerInvariant();
            if (ext != ".xlsx")
            {
                TempData["AdminMessage"] = "Nur .xlsx Dateien sind erlaubt.";
                return RedirectToAction(nameof(Index));
            }

            var path = GetEwigeWorkbookPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            await workbook.CopyToAsync(stream);

            await _audit.LogAsync("UploadEwigeListe", "EwigeListe", "active.xlsx", $"Size={workbook.Length}");
            TempData["AdminMessage"] = "Ewige Liste erfolgreich aktualisiert.";
            return RedirectToAction(nameof(Index));
        }
    }
}
