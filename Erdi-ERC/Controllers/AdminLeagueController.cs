using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace <OWNER_HANDLE>_ERC.Controllers
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

            var ranked = standings
                .OrderByDescending(s => s.Points)
                .ThenByDescending(s => s.Wins)
                .ThenBy(s => s.Driver, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < ranked.Count; i++)
                ranked[i].Position = i + 1;

            await _db.SaveChangesAsync();
            await _audit.LogAsync("ResortStandings", "League", leagueId, $"Renumbered={ranked.Count}");
            TempData["AdminMessage"] = $"{ranked.Count} Fahrer nach Punkten neu nummeriert.";
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

            await _db.SaveChangesAsync();
            await RecalculateAsync(leagueId); // Punkte + Positionen neu ableiten
            await _audit.LogAsync("SaveAllStandings", "League", leagueId,
                $"Created={created}, Updated={updated}, Deleted={deleted}");

            TempData["AdminMessage"] = $"Fahrerliste gespeichert — {created} neu, {updated} geändert, {deleted} gelöscht. Punkte & Positionen neu berechnet.";
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
            string[]? positions, string[]? raceTimes, int[]? penaltySeconds,
            string[]? dnfDrivers, string[]? reserveDrivers, string[]? reserveMainDrivers,
            string[]? guestMainDrivers = null,
            int[]? qualiPositions = null)
        {
            if (string.IsNullOrWhiteSpace(leagueId) || string.IsNullOrWhiteSpace(track))
            {
                TempData["RaceError"] = "Liga und Strecke sind erforderlich.";
                return RedirectToAction(nameof(EnterRace), new { leagueId });
            }

            var league = await _db.Leagues.Include(l => l.Standings).FirstOrDefaultAsync(l => l.Id == leagueId);
            if (league is null) return NotFound();

            if (TryValidateGuestAssignments(positions, guestMainDrivers, league, out var guestError))
            {
                TempData["RaceError"] = guestError;
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

            ApplyRaceEntries(race, fastestLapDriver, positions, raceTimes, penaltySeconds, dnfDrivers, reserveDrivers, reserveMainDrivers, guestMainDrivers, qualiPositions);
            await _db.SaveChangesAsync();
            await RecalculateAsync(leagueId);

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
            string[]? positions, string[]? raceTimes, int[]? penaltySeconds,
            string[]? dnfDrivers, string[]? reserveDrivers, string[]? reserveMainDrivers,
            string[]? guestMainDrivers = null,
            int[]? qualiPositions = null)
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

            if (TryValidateGuestAssignments(positions, guestMainDrivers, league, out var guestError))
            {
                TempData["RaceError"] = guestError;
                return RedirectToAction(nameof(EditRace), new { rowId });
            }

            // Bestehende Detail-Datensätze ersetzen (sauberster Weg für eine vollständige Korrektur).
            _db.RaceFinishes.RemoveRange(race.Finishes);
            _db.RaceReserveAssignments.RemoveRange(race.ReserveAssignments);
            _db.RaceGuestAssignments.RemoveRange(race.GuestAssignments);

            race.Date = date;
            race.Track = track.Trim();
            ApplyRaceEntries(race, fastestLapDriver, positions, raceTimes, penaltySeconds, dnfDrivers, reserveDrivers, reserveMainDrivers, guestMainDrivers, qualiPositions);

            await _db.SaveChangesAsync();
            await RecalculateAsync(leagueId);
            await _audit.LogAsync("UpdateEnteredRace", "RaceResult", race.RowId.ToString(),
                $"League={leagueId}, Track={race.Track}, Date={date:yyyy-MM-dd}, Winner={race.Winner}");

            TempData["AdminMessage"] = $"Rennergebnis für {race.Track} aktualisiert. Punkte neu berechnet.";
            return RedirectToAction("EditLeague", "AdminLeagueManagement", new { id = leagueId });
        }

        /// <summary>
        /// Baut Reserve-Zuordnungen, Gast-Zuordnungen und Zieleinläufe für ein (bereits gespeichertes) Rennen aus den
        /// Formular-Arrays auf, setzt Sieger und schnellste Runde. Strafzeit (Sek.) wird zur Rennzeit
        /// addiert; DNF-Fahrer erhalten Position 0. Der Aufrufer ruft anschließend SaveChanges.
        /// </summary>
        private void ApplyRaceEntries(
            RaceResult race, string? fastestLapDriver,
            string[]? positions, string[]? raceTimes, int[]? penaltySeconds,
            string[]? dnfDrivers, string[]? reserveDrivers, string[]? reserveMainDrivers,
            string[]? guestMainDrivers = null,
            int[]? qualiPositions = null)
        {
            var fastestLap = fastestLapDriver?.Trim() ?? string.Empty;
            race.Winner = string.Empty;
            race.FastestLap = fastestLap;

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

            // Gast-Zuordnungen (Cross-League): für jeden nicht-leeren gastMainDrivers[i]
            // muss positions[i] einen Fahrer enthalten, der weder in dieser Liga als Standing
            // existiert (sonst kein Gast) noch identisch mit dem MainDriver ist.
            // Pflicht: jeder Cross-League-Gast MUSS einem Liga-Hauptfahrer zugeordnet werden.
            if (guestMainDrivers != null)
            {
                for (int i = 0; i < guestMainDrivers.Length; i++)
                {
                    var mainName = guestMainDrivers.ElementAtOrDefault(i)?.Trim();
                    if (string.IsNullOrWhiteSpace(mainName)) continue;

                    var guestName = positions?.ElementAtOrDefault(i)?.Trim();
                    if (string.IsNullOrWhiteSpace(guestName)) continue;

                    _db.RaceGuestAssignments.Add(new RaceGuestAssignment
                    {
                        RaceResultId = race.RowId,
                        GuestDriver = guestName,
                        MainDriver = mainName
                    });
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
                    var penalty = penaltySeconds?.ElementAtOrDefault(i) ?? 0;
                    if (penalty > 0 && timeMs.HasValue) timeMs = timeMs.Value + (penalty * 1000);

                    var qualiRaw = qualiPositions?.ElementAtOrDefault(i) ?? 0;
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
        }

        /// <summary>
        /// Validiert die Gast-Zuordnungen für ein Rennen:
        /// - Wenn <paramref name="guestMainDrivers"/> an Index i einen Wert hat, muss
        ///   <paramref name="positions"/> an Index i ebenfalls einen Fahrer haben.
        /// - Der MainDriver MUSS in der Liga als Stammfahrer (kein Reserve) existieren.
        /// - Der MainDriver darf nicht identisch mit dem Gast sein.
        /// Returns true + Fehlermeldung, falls Validation fehlschlaegt.
        /// </summary>
        private static bool TryValidateGuestAssignments(
            string[]? positions, string[]? guestMainDrivers, League league,
            out string error)
        {
            error = string.Empty;
            if (guestMainDrivers is null) return false;

            var mainDrivers = new HashSet<string>(
                league.Standings
                    .Where(s => !s.IsReserveDriver && !string.IsNullOrWhiteSpace(s.Driver))
                    .Select(s => s.Driver.Trim()),
                StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < guestMainDrivers.Length; i++)
            {
                var mainName = guestMainDrivers[i]?.Trim();
                if (string.IsNullOrWhiteSpace(mainName)) continue;

                var guestName = positions?.ElementAtOrDefault(i)?.Trim();
                if (string.IsNullOrWhiteSpace(guestName))
                {
                    error = $"Gast-Zuordnung auf Position {i + 1} ohne Fahrer — bitte den Gastnamen eintragen.";
                    return true;
                }

                if (!mainDrivers.Contains(mainName))
                {
                    error = $"Gast '{guestName}' wurde dem Hauptfahrer '{mainName}' zugeordnet — '{mainName}' ist in dieser Liga kein Stammfahrer.";
                    return true;
                }

                if (string.Equals(mainName, guestName, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"Gast '{guestName}' kann nicht sich selbst als Hauptfahrer haben.";
                    return true;
                }
            }

            return false;
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
                $"League={leagueId}, Track={restored.Track}, RestoredFrom=Undo***REMOVED***{undo.Id}");
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
