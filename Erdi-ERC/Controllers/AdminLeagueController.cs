using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace <OWNER_HANDLE>_ERC.Controllers
{
    [Authorize(Policy = "Admin.League")]
    public class AdminLeagueController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IAdminAuditService _audit;
        private readonly IDriverProfileService _driverProfiles;
        private readonly IWebhookAutomationService _webhookAuto;

        public AdminLeagueController(AppDbContext db, IAdminAuditService audit, IDriverProfileService driverProfiles, IWebhookAutomationService webhookAuto)
        {
            _db = db;
            _audit = audit;
            _driverProfiles = driverProfiles;
            _webhookAuto = webhookAuto;
        }

        // ---- Standings ----
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveStanding(int? rowId, string leagueId, int position, string driver, string? team, int? driverNumber, int points, int wins, bool isReserveDriver = false, string? reserveForDriver = null)
        {
            DriverStanding? entity = rowId.HasValue ? await _db.DriverStandings.FindAsync(rowId.Value) : null;
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
                return RedirectToAction("EditLeague", "Admin", new { id = leagueId });
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
                    return RedirectToAction("EditLeague", "Admin", new { id = leagueId });
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
                    return RedirectToAction("EditLeague", "Admin", new { id = leagueId });
                }

                if (reserveForStanding.IsReserveDriver)
                {
                    TempData["AdminMessage"] = $"'{normalizedReserveFor}' ist selbst als Reserve markiert. Bitte einen Stammfahrer wählen.";
                    return RedirectToAction("EditLeague", "Admin", new { id = leagueId });
                }

                if (!string.IsNullOrWhiteSpace(reserveForStanding.Team))
                {
                    normalizedTeam = reserveForStanding.Team.Trim();
                }
            }

            entity.Position = position;
            entity.Driver = normalizedDriver;
            entity.Team = normalizedTeam;
            entity.DriverNumber = driverNumber;
            entity.Points = points;
            entity.Wins = wins;
            entity.IsReserveDriver = isReserveDriver;
            entity.ReserveForDriver = isReserveDriver ? normalizedReserveFor : null;

            await _db.SaveChangesAsync();
            await _audit.LogAsync("SaveStanding", "DriverStanding", entity.RowId.ToString(), 
                $"League={leagueId}, Driver={entity.Driver}, Number={entity.DriverNumber}, Reserve={entity.IsReserveDriver}");

            return RedirectToAction("EditLeague", "Admin", new { id = leagueId });
        }

        [HttpGet]
        public async Task<IActionResult> DriverSuggestions(string q)
        {
            var matches = await _driverProfiles.SuggestAsync(q ?? string.Empty);
            return Json(matches.Select(m => new
            {
                discordId = m.DiscordId,
                platform = m.Platform,
                gamerTag = m.GamerTag,
                discordName = m.DiscordName,
                exact = m.ExactMatch,
                distance = m.Distance
            }));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStanding(int rowId)
        {
            var entity = await _db.DriverStandings.FindAsync(rowId);
            if (entity is null) return NotFound();
            var leagueId = entity.LeagueId;
            _db.DriverStandings.Remove(entity);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("DeleteStanding", "DriverStanding", rowId.ToString(), $"League={leagueId}");
            return RedirectToAction("EditLeague", "Admin", new { id = leagueId });
        }

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
            string[]? positions, string[]? raceTimes, int[]? penaltySeconds,
            string[]? dnfDrivers, string[]? reserveDrivers, string[]? reserveMainDrivers)
        {
            if (string.IsNullOrWhiteSpace(leagueId) || string.IsNullOrWhiteSpace(track))
            {
                TempData["RaceError"] = "Liga und Strecke sind erforderlich.";
                return RedirectToAction(nameof(EnterRace), new { leagueId });
            }

            var league = await _db.Leagues.Include(l => l.Standings).FirstOrDefaultAsync(l => l.Id == leagueId);
            if (league is null) return NotFound();

            var race = new RaceResult
            {
                LeagueId = leagueId,
                Date = date,
                Track = track.Trim(),
                FastestLap = fastestLapDriver?.Trim() ?? string.Empty,
                Winner = string.Empty
            };

            _db.RaceResults.Add(race);
            await _db.SaveChangesAsync();

            // Reserve assignments
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

            // Finishes
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
                    var penalty = penaltySeconds?.ElementAtOrDefault(i) ?? 0;
                    if (penalty > 0 && timeMs.HasValue) timeMs = timeMs.Value + (penalty * 1000);

                    _db.RaceFinishes.Add(new RaceFinish
                    {
                        RaceResultId = race.RowId,
                        Driver = driver.Trim(),
                        Position = isDnf ? 0 : pos,
                        FastestLap = driver.Trim().Equals(fastestLapDriver?.Trim() ?? "", StringComparison.OrdinalIgnoreCase),
                        RaceTimeMs = timeMs
                    });

                    if (pos == 1 && !isDnf) race.Winner = driver.Trim();
                }
            }

            await _db.SaveChangesAsync();

            // Save undo entry
            var finishes = await _db.RaceFinishes.Where(f => f.RaceResultId == race.RowId).ToListAsync();
            var reserves = await _db.RaceReserveAssignments.Where(r => r.RaceResultId == race.RowId).ToListAsync();
            var undoPayload = System.Text.Json.JsonSerializer.Serialize(new
            {
                Race = new { race.RowId, race.LeagueId, race.Date, race.Track, race.Winner, race.FastestLap },
                Finishes = finishes.Select(f => new { f.Driver, f.Position, f.FastestLap, f.RaceTimeMs }),
                Reserves = reserves.Select(r => new { r.ReserveDriver, r.MainDriver })
            });
            _db.RaceUndoEntries.Add(new RaceUndoEntry
            {
                LeagueId = leagueId,
                OriginalRaceId = race.RowId,
                Actor = User.Identity?.Name ?? "Admin",
                PayloadJson = undoPayload,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            await _audit.LogAsync("SaveEnteredRace", "RaceResult", race.RowId.ToString(),
                $"League={leagueId}, Track={track}, Date={date:yyyy-MM-dd}, Winner={race.Winner}");

            TempData["AdminMessage"] = $"Rennergebnis für {track} gespeichert.";
            await _webhookAuto.FireAsync(WebhookEvents.RaceResultSaved, new()
            {
                ["League"]     = leagueId,
                ["Track"]      = track,
                ["Date"]       = date.ToString("dd.MM.yyyy"),
                ["Winner"]     = race.Winner,
                ["FastestLap"] = race.FastestLap ?? "",
            });
            return RedirectToAction("EditLeague", "Admin", new { id = leagueId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRace(int rowId, string leagueId, DateTime date, string track)
        {
            var race = await _db.RaceResults.FindAsync(rowId);
            if (race is null) return NotFound();
            race.Date = date;
            race.Track = track?.Trim() ?? race.Track;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("SaveRace", "RaceResult", rowId.ToString(), $"League={leagueId}, Track={track}");
            return RedirectToAction("EditLeague", "Admin", new { id = leagueId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRace(int rowId, string leagueId)
        {
            var race = await _db.RaceResults
                .Include(r => r.Finishes)
                .Include(r => r.ReserveAssignments)
                .FirstOrDefaultAsync(r => r.RowId == rowId);

            if (race is null) return NotFound();

            _db.RaceFinishes.RemoveRange(race.Finishes);
            _db.RaceReserveAssignments.RemoveRange(race.ReserveAssignments);
            _db.RaceResults.Remove(race);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("DeleteRace", "RaceResult", rowId.ToString(), $"League={leagueId}, Track={race.Track}");
            return RedirectToAction("EditLeague", "Admin", new { id = leagueId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UndoDeleteRace(int rowId, string leagueId)
        {
            var undo = await _db.RaceUndoEntries.AsTracking()
                .FirstOrDefaultAsync(x => x.OriginalRaceId == rowId && x.LeagueId == leagueId && !x.IsUsed);

            if (undo is null)
            {
                TempData["AdminMessage"] = "Kein Undo-Eintrag gefunden.";
                return RedirectToAction("EditLeague", "Admin", new { id = leagueId });
            }

            undo.IsUsed = true;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("UndoDeleteRace", "RaceUndoEntry", undo.Id.ToString(), $"League={leagueId}");
            TempData["AdminMessage"] = "Undo-Eintrag als verwendet markiert. Rennen muss manuell neu eingetragen werden.";
            return RedirectToAction("EditLeague", "Admin", new { id = leagueId });
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

            return RedirectToAction("EditLeague", "Admin", new { id = leagueId });
        }

        private static int? ParseRaceTimeMs(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var clean = raw.Trim().Replace(",", ".");
            // Format: M:SS.mmm or SS.mmm
            if (clean.Contains(':'))
            {
                var parts = clean.Split(':');
                if (parts.Length == 2
                    && int.TryParse(parts[0], out var mins)
                    && double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var secs))
                    return (int)((mins * 60 + secs) * 1000);
            }
            else if (double.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var secs))
            {
                return (int)(secs * 1000);
            }
            return null;
        }
    }
}
