using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace <OWNER_HANDLE>_ERC.Services
{
    public class StatsService : IStatsService
    {
        private static readonly int[] DefaultPointMap = { 25, 21, 18, 16, 14, 12, 10, 8, 7, 6, 5, 4, 3, 2, 1, 0, 0, 0, 0, 0 };
        private readonly AppDbContext _db;
        private readonly int[] _pointMap;

        public StatsService(AppDbContext db, IOptions<F1ScoringOptions> scoringOptions)
        {
            _db = db;
            var configured = scoringOptions.Value.PointMap;
            _pointMap = configured is { Length: > 0 } ? configured : DefaultPointMap;
        }

        public async Task RebuildAllLeagueStandingsAsync(CancellationToken cancellationToken = default)
        {
            var leagueIds = await _db.Leagues
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            foreach (var leagueId in leagueIds)
            {
                await RebuildLeagueStandingsAsync(leagueId, cancellationToken);
            }
        }

        public async Task RebuildLeagueStandingsAsync(string leagueId, CancellationToken cancellationToken = default)
        {
            var standings = await _db.DriverStandings.AsTracking()
                .Where(x => x.LeagueId == leagueId)
                .ToListAsync(cancellationToken);

            // Aktuelle Saison der Liga: ist sie gesetzt, zählt die Tabelle nur Rennen dieser Saison.
            // Null/leer = alle Rennen (rückwärtskompatibles Standardverhalten).
            var currentSeason = await _db.Leagues
                .Where(l => l.Id == leagueId)
                .Select(l => l.CurrentSeason)
                .FirstOrDefaultAsync(cancellationToken);

            var racesQuery = _db.RaceResults.AsTracking()
                .Where(r => r.LeagueId == leagueId);
            if (!string.IsNullOrWhiteSpace(currentSeason))
                racesQuery = racesQuery.Where(r => r.Season == currentSeason);

            var races = await racesQuery
                .Include(r => r.Finishes)
                .Include(r => r.ReserveAssignments)
                .OrderBy(r => r.Date)
                .ThenBy(r => r.RowId)
                .ToListAsync(cancellationToken);

            foreach (var race in races)
            {
                foreach (var finish in race.Finishes)
                {
                    finish.Driver = Normalize(finish.Driver);
                }

                foreach (var assignment in race.ReserveAssignments)
                {
                    assignment.ReserveDriver = Normalize(assignment.ReserveDriver);
                    assignment.MainDriver = Normalize(assignment.MainDriver);
                }
            }

            NormalizeAndConsolidateStandings(standings);

            foreach (var standing in standings)
            {
                standing.Points = 0;
                standing.Wins = 0;
                standing.ReserveStarts = 0;
                standing.ReservePointsForMain = 0;
            }

            var standingsByDriver = standings
                .Where(s => !string.IsNullOrWhiteSpace(s.Driver))
                .GroupBy(s => s.Driver, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // Punkte je Fahrer pro Rennen sammeln, damit Streichresultate (Drop-Scores)
            // nach der vollständigen Saison angewendet werden können.
            var racePointsByStanding = new Dictionary<DriverStanding, List<int>>();

            foreach (var race in races)
            {
                var raceReserveToMain = race.ReserveAssignments
                    .Where(a => !string.IsNullOrWhiteSpace(a.ReserveDriver) && !string.IsNullOrWhiteSpace(a.MainDriver))
                    .GroupBy(a => a.ReserveDriver, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().MainDriver, StringComparer.OrdinalIgnoreCase);

                var orderedFinishes = race.Finishes
                    .Where(f => f.Position > 0 && !string.IsNullOrWhiteSpace(f.Driver))
                    .OrderBy(f => f.Position)
                    .ToList();

                foreach (var finish in orderedFinishes)
                {
                    var driverName = Normalize(finish.Driver);
                    if (string.IsNullOrWhiteSpace(driverName))
                    {
                        continue;
                    }

                    var standing = GetOrCreateStanding(leagueId, standings, standingsByDriver, driverName);

                    var mappedMainDriver = GetMappedMainDriverForRace(standing, raceReserveToMain, standingsByDriver);
                    if (!string.IsNullOrWhiteSpace(mappedMainDriver)
                        && standingsByDriver.TryGetValue(mappedMainDriver, out var mainStanding)
                        && !string.IsNullOrWhiteSpace(mainStanding.Team)
                        && string.IsNullOrWhiteSpace(standing.Team))
                    {
                        standing.Team = mainStanding.Team;
                    }

                    var pointsIndex = finish.Position - 1;
                    var points = pointsIndex >= 0 && pointsIndex < _pointMap.Length ? _pointMap[pointsIndex] : 0;

                    if (!string.IsNullOrWhiteSpace(mappedMainDriver))
                    {
                        standing.ReserveStarts += 1;
                        standing.ReservePointsForMain += points;
                    }

                    if (!racePointsByStanding.TryGetValue(standing, out var raceScores))
                    {
                        raceScores = new List<int>();
                        racePointsByStanding[standing] = raceScores;
                    }
                    raceScores.Add(points);

                    if (finish.Position == 1)
                    {
                        standing.Wins += 1;
                    }
                }
            }

            // Drop-Scores (Streichresultate): die N schwächsten Rennergebnisse je Fahrer werden
            // aus der Wertung genommen, falls die Liga das konfiguriert hat. 0/null = alle zählen.
            var dropWorst = await _db.Leagues
                .Where(l => l.Id == leagueId)
                .Select(l => l.DropWorstResults)
                .FirstOrDefaultAsync(cancellationToken);

            foreach (var standing in standings)
            {
                if (!racePointsByStanding.TryGetValue(standing, out var raceScores) || raceScores.Count == 0)
                {
                    standing.Points = 0;
                    continue;
                }

                var ordered = raceScores.OrderByDescending(p => p).ToList();
                var keep = dropWorst.HasValue && dropWorst.Value > 0
                    ? Math.Max(0, ordered.Count - dropWorst.Value)
                    : ordered.Count;
                standing.Points = ordered.Take(keep).Sum();
            }

            // Manuelle Korrektur (Bonus/Malus) auf die aus den Rennen abgeleiteten Punkte addieren.
            // Sie überlebt jede Neuberechnung, weil PointsAdjustment oben nicht zurückgesetzt wird.
            foreach (var standing in standings)
            {
                standing.Points += standing.PointsAdjustment;
            }

            // Stewarding-Strafen vom Typ "Punkteabzug" automatisch auf die Tabelle anwenden,
            // damit Stewards keine zusätzliche manuelle Korrektur (PointsAdjustment) pflegen müssen.
            // Andere Strafarten (Zeitstrafe/Grid/Verwarnung) wirken im Rennen selbst, nicht hier.
            var penaltyPointsByDriver = (await _db.LeaguePenalties
                    .Where(p => p.LeagueId == leagueId && p.PenaltyType == "Punkteabzug")
                    .ToListAsync(cancellationToken))
                .GroupBy(p => Normalize(p.Driver), StringComparer.OrdinalIgnoreCase)
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .ToDictionary(g => g.Key, g => g.Sum(p => Math.Abs(p.Points)), StringComparer.OrdinalIgnoreCase);

            foreach (var standing in standings)
            {
                if (penaltyPointsByDriver.TryGetValue(standing.Driver, out var deduction))
                {
                    standing.Points -= deduction;
                }
            }

            var ranked = standings
                .OrderByDescending(s => s.Points)
                .ThenByDescending(s => s.Wins)
                .ThenBy(s => s.Driver)
                .ToList();

            for (int i = 0; i < ranked.Count; i++)
            {
                ranked[i].Position = i + 1;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        private DriverStanding GetOrCreateStanding(
            string leagueId,
            List<DriverStanding> standings,
            Dictionary<string, DriverStanding> standingsByDriver,
            string driverName)
        {
            if (standingsByDriver.TryGetValue(driverName, out var existing))
            {
                return existing;
            }

            var standing = new DriverStanding
            {
                LeagueId = leagueId,
                Driver = driverName,
                Team = string.Empty,
                ReserveForDriver = null
            };

            _db.DriverStandings.Add(standing);
            standings.Add(standing);
            standingsByDriver[driverName] = standing;
            return standing;
        }

        private static string Normalize(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private static string? NormalizeOptional(string? value)
        {
            var normalized = value?.Trim();
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }

        private void NormalizeAndConsolidateStandings(List<DriverStanding> standings)
        {
            foreach (var standing in standings)
            {
                standing.Driver = Normalize(standing.Driver);
                standing.Team = Normalize(standing.Team);
                standing.ReserveForDriver = NormalizeOptional(standing.ReserveForDriver);

                if (!standing.IsReserveDriver)
                {
                    standing.ReserveForDriver = null;
                }
                else if (!string.IsNullOrWhiteSpace(standing.ReserveForDriver)
                    && standing.Driver.Equals(standing.ReserveForDriver, StringComparison.OrdinalIgnoreCase))
                {
                    standing.ReserveForDriver = null;
                }
            }

            var duplicateGroups = standings
                .Where(s => !string.IsNullOrWhiteSpace(s.Driver))
                .GroupBy(s => s.Driver, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in duplicateGroups)
            {
                var ordered = group
                    .OrderBy(s => s.IsReserveDriver ? 1 : 0)
                    .ThenBy(s => s.RowId)
                    .ToList();

                var primary = ordered[0];

                foreach (var duplicate in ordered.Skip(1))
                {
                    MergeStandingMetadata(primary, duplicate);
                    _db.DriverStandings.Remove(duplicate);
                    standings.Remove(duplicate);
                }
            }
        }

        private static void MergeStandingMetadata(DriverStanding target, DriverStanding source)
        {
            if (string.IsNullOrWhiteSpace(target.Team) && !string.IsNullOrWhiteSpace(source.Team))
            {
                target.Team = source.Team;
            }

            if (target.IsReserveDriver && string.IsNullOrWhiteSpace(target.ReserveForDriver) && !string.IsNullOrWhiteSpace(source.ReserveForDriver))
            {
                target.ReserveForDriver = source.ReserveForDriver;
            }
        }

        private static string? GetMappedMainDriverForRace(
            DriverStanding standing,
            Dictionary<string, string> raceReserveToMain,
            Dictionary<string, DriverStanding> standingsByDriver)
        {
            if (raceReserveToMain.TryGetValue(standing.Driver, out var mappedMain)
                && !string.IsNullOrWhiteSpace(mappedMain)
                && standingsByDriver.TryGetValue(mappedMain, out var mappedMainStanding)
                && !mappedMainStanding.IsReserveDriver)
            {
                return mappedMainStanding.Driver;
            }

            if (standing.IsReserveDriver
                && !string.IsNullOrWhiteSpace(standing.ReserveForDriver)
                && standingsByDriver.TryGetValue(standing.ReserveForDriver, out var fallbackMainStanding)
                && !fallbackMainStanding.IsReserveDriver)
            {
                return fallbackMainStanding.Driver;
            }

            return null;
        }
    }
}
