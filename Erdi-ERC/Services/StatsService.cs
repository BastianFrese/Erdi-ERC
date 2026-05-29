using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using Microsoft.EntityFrameworkCore;

namespace <OWNER_HANDLE>_ERC.Services
{
    public class StatsService : IStatsService
    {
        private static readonly int[] PointMap = { 25, 21, 18, 16, 14, 12, 10, 8, 7, 6, 5, 4, 3, 2, 1, 0, 0, 0, 0, 0 };
        private readonly AppDbContext _db;

        public StatsService(AppDbContext db)
        {
            _db = db;
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

            var races = await _db.RaceResults.AsTracking()
                .Where(r => r.LeagueId == leagueId)
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
                    var points = pointsIndex >= 0 && pointsIndex < PointMap.Length ? PointMap[pointsIndex] : 0;

                    if (!string.IsNullOrWhiteSpace(mappedMainDriver))
                    {
                        standing.ReserveStarts += 1;
                        standing.ReservePointsForMain += points;
                    }

                    standing.Points += points;

                    if (finish.Position == 1)
                    {
                        standing.Wins += 1;
                    }
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

        private static void SyncReserveTeamFromMainIfNeeded(
            DriverStanding standing,
            Dictionary<string, DriverStanding> standingsByDriver)
        {
            if (!standing.IsReserveDriver || string.IsNullOrWhiteSpace(standing.ReserveForDriver))
            {
                return;
            }

            if (standingsByDriver.TryGetValue(standing.ReserveForDriver, out var mainStanding)
                && !string.IsNullOrWhiteSpace(mainStanding.Team)
                && !mainStanding.IsReserveDriver)
            {
                // Reservefahrer zählen in der Teamwertung über den ausgewählten Hauptfahrer.
                standing.Team = mainStanding.Team;
            }
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
