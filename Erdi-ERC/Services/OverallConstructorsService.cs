using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Helpers;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace <OWNER_HANDLE>_ERC.Services
{
    /// <summary>
    /// Aggregiert Punkte aller Ligen, deren Opt-in <see cref="League.CountsTowardOverall"/>
    /// gesetzt ist, zu einer Liga-übergreifenden Constructors-Wertung.
    ///
    /// Team-Schlüssel ist der kanonische <c>F1Team.CssKey</c> aus <see cref="F1TeamsHelper"/>,
    /// damit Legacy-Aliase (z.B. "Sauber", "RB", "Red Bull") und identische Teams über
    /// unterschiedliche Ligen hinweg zuverlässig zusammengeführt werden.
    ///
    /// Punkte gehen pro Rennen an das Team, für das der Fahrer in genau diesem Rennen
    /// angetreten ist (über <see cref="RaceTeamHelper.ResolveTeamForRaceDriver"/>) — kein
    /// "Heimteam-Override", damit Cross-Team-Einsätze (z.B. Gaststarter) sauber zählen.
    /// </summary>
    public class OverallConstructorsService
    {
        private readonly AppDbContext _db;
        private readonly int[] _pointMap;

        public OverallConstructorsService(AppDbContext db, IOptions<F1ScoringOptions> scoringOptions)
        {
            _db = db;
            var configured = scoringOptions.Value.PointMap;
            if (configured is { Length: > 0 })
            {
                _pointMap = configured;
            }
            else
            {
                _pointMap = new[] { 25, 21, 18, 16, 14, 12, 10, 8, 7, 6, 5, 4, 3, 2, 1, 0, 0, 0, 0, 0 };
            }
        }

        /// <summary>
        /// Berechnet die Liga-übergreifende Constructors-Tabelle. Es werden nur Ligen mit
        /// <see cref="League.CountsTowardOverall"/>=true berücksichtigt. Sind keine Ligen
        /// opt-in oder keine Rennen vorhanden, wird eine leere Liste zurückgegeben.
        /// </summary>
        public async Task<List<OverallConstructorRow>> ComputeAsync(CancellationToken cancellationToken = default)
        {
            var leagues = await _db.Leagues
                .AsNoTracking()
                .Where(l => l.CountsTowardOverall)
                .Include(l => l.Standings)
                .Include(l => l.Races).ThenInclude(r => r.Finishes)
                .Include(l => l.Races).ThenInclude(r => r.ReserveAssignments)
                .OrderBy(l => l.SortOrder)
                .ThenBy(l => l.Name)
                .ToListAsync(cancellationToken);

            var buckets = new Dictionary<string, TeamBucket>(StringComparer.OrdinalIgnoreCase);

            foreach (var league in leagues)
            {
                var orderedRaces = league.Races
                    .OrderBy(r => r.Date)
                    .ThenBy(r => r.RowId)
                    .ToList();

                foreach (var race in orderedRaces)
                {
                    foreach (var finish in race.Finishes.Where(f => f.Position > 0))
                    {
                        var resolvedTeamName = RaceTeamHelper.ResolveTeamForRaceDriver(
                            league.Standings, race, finish.Driver);
                        if (string.IsNullOrWhiteSpace(resolvedTeamName))
                        {
                            continue;
                        }

                        var f1Team = F1TeamsHelper.GetTeamByName(resolvedTeamName);
                        var cssKey = f1Team?.CssKey ?? "unknown";
                        var displayName = f1Team?.Name ?? resolvedTeamName.Trim();
                        var primaryColor = f1Team?.PrimaryColor ?? "***REMOVED***888888";
                        var secondaryColor = f1Team?.SecondaryColor ?? "***REMOVED***222222";

                        var points = SafePoints(finish.Position);
                        if (points <= 0)
                        {
                            continue;
                        }

                        if (!buckets.TryGetValue(cssKey, out var bucket))
                        {
                            bucket = new TeamBucket(cssKey, displayName, primaryColor, secondaryColor);
                            buckets[cssKey] = bucket;
                        }

                        bucket.AddRace(league.Id, points, finish.Position);
                    }
                }
            }

            return buckets.Values
                .OrderByDescending(b => b.Points)
                .ThenByDescending(b => b.Wins)
                .ThenByDescending(b => b.SecondPlaces)
                .ThenByDescending(b => b.ThirdPlaces)
                .ThenBy(b => b.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select((b, idx) => b.ToRow(idx + 1))
                .ToList();
        }

        private int SafePoints(int position)
        {
            var idx = position - 1;
            return idx >= 0 && idx < _pointMap.Length ? _pointMap[idx] : 0;
        }

        private sealed class TeamBucket
        {
            public string CssKey { get; }
            public string DisplayName { get; }
            public string PrimaryColor { get; }
            public string SecondaryColor { get; }
            public int Points { get; private set; }
            public int Wins { get; private set; }
            public int SecondPlaces { get; private set; }
            public int ThirdPlaces { get; private set; }
            public int Top5 { get; private set; }
            public int Top10 { get; private set; }
            public int BestPosition { get; private set; } = int.MaxValue;
            public int Events { get; private set; }
            public Dictionary<string, LeagueAggregate> PerLeague { get; } = new(StringComparer.OrdinalIgnoreCase);

            public TeamBucket(string cssKey, string displayName, string primaryColor, string secondaryColor)
            {
                CssKey = cssKey;
                DisplayName = displayName;
                PrimaryColor = primaryColor;
                SecondaryColor = secondaryColor;
            }

            public void AddRace(string leagueId, int points, int position)
            {
                Points += points;
                Events++;

                if (position == 1) Wins++;
                if (position == 2) SecondPlaces++;
                if (position == 3) ThirdPlaces++;
                if (position <= 5) Top5++;
                if (position <= 10) Top10++;
                if (position > 0 && position < BestPosition) BestPosition = position;

                if (!PerLeague.TryGetValue(leagueId, out var la))
                {
                    la = new LeagueAggregate();
                    PerLeague[leagueId] = la;
                }
                la.Add(points, position);
            }

            public OverallConstructorRow ToRow(int position)
            {
                var podium = Wins + SecondPlaces + ThirdPlaces;
                return new OverallConstructorRow
                {
                    Position = position,
                    CssKey = CssKey,
                    DisplayName = DisplayName,
                    PrimaryColor = PrimaryColor,
                    SecondaryColor = SecondaryColor,
                    Points = Points,
                    Wins = Wins,
                    SecondPlaces = SecondPlaces,
                    ThirdPlaces = ThirdPlaces,
                    Top5 = Top5,
                    Top10 = Top10,
                    BestPosition = BestPosition == int.MaxValue ? 0 : BestPosition,
                    Podiums = podium,
                    Events = Events,
                    LeaguesRaced = PerLeague.Count,
                    PerLeague = PerLeague
                        .Select(kv => new OverallConstructorLeagueBreakdown
                        {
                            LeagueId = kv.Key,
                            Points = kv.Value.Points,
                            Events = kv.Value.Events,
                            Wins = kv.Value.Wins,
                            BestPosition = kv.Value.BestPosition == int.MaxValue ? 0 : kv.Value.BestPosition
                        })
                        .OrderByDescending(x => x.Points)
                        .ThenBy(x => x.LeagueId, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };
            }
        }

        private sealed class LeagueAggregate
        {
            public int Points { get; private set; }
            public int Events { get; private set; }
            public int Wins { get; private set; }
            public int BestPosition { get; private set; } = int.MaxValue;

            public void Add(int points, int position)
            {
                Points += points;
                Events++;
                if (position == 1) Wins++;
                if (position > 0 && position < BestPosition) BestPosition = position;
            }
        }
    }
}
