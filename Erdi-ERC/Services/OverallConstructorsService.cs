using Erdi_ERC.Data;
using Erdi_ERC.Helpers;
using Erdi_ERC.Models;
using Erdi_ERC.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Erdi_ERC.Services
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
    ///
    /// Fahrer ohne auflösbares Team laufen als „Ohne Team" in einem eigenen Bucket mit.
    /// Das ist dieselbe Regel wie auf der Ligaseite und im Overlay-Export (siehe
    /// <see cref="ConstructorTeamHelper"/>) — sonst zeigen zwei öffentliche Tabellen für
    /// dieselben Rennen unterschiedliche Summen.
    /// </summary>
    public class OverallConstructorsService
    {
        private const string CacheKey = "overall-constructors:v1";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Schlüssel des „Ohne Team"-Buckets. Bewusst eigener Wert: der Bucket ist kein
        /// F1-Team, und der Fallback <c>"unknown"</c> würde ihn mit echten unbekannten
        /// Teamnamen zusammenwerfen. Der Schlüssel landet in HTML-Ids
        /// (<c>Views/Races/Constructors.cshtml</c>), muss also ohne Leerzeichen auskommen.
        /// </summary>
        private const string NoTeamCssKey = "no-team";

        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly int[] _pointMap;

        public OverallConstructorsService(AppDbContext db, IMemoryCache cache, IOptions<F1ScoringOptions> scoringOptions)
        {
            _db = db;
            _cache = cache;
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
        /// Ergebnis wird 2 Min. gecacht — der Full-Graph-Load läuft sonst bei jedem
        /// Home-Hit und jedem /Races/Constructors-Aufruf.
        /// </summary>
        public async Task<List<OverallConstructorRow>> ComputeAsync(CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue<List<OverallConstructorRow>>(CacheKey, out var cached) && cached is not null)
            {
                return cached;
            }

            var result = await ComputeUncachedAsync(cancellationToken);

            _cache.Set(CacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl,
                Priority = CacheItemPriority.Low,
                Size = 1
            });

            return result;
        }

        private async Task<List<OverallConstructorRow>> ComputeUncachedAsync(CancellationToken cancellationToken)
        {
            var leagues = await _db.Leagues
                .AsNoTracking()
                .AsSplitQuery()
                .Where(l => l.CountsTowardOverall)
                .Include(l => l.Standings)
                .Include(l => l.Races).ThenInclude(r => r.Finishes)
                .Include(l => l.Races).ThenInclude(r => r.ReserveAssignments)
                .Include(l => l.Races).ThenInclude(r => r.GuestAssignments)
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

                // Ein Lookup pro Liga statt linearer Standings-Scan pro Finish.
                var lookup = new RaceTeamLookup(league.Standings);

                foreach (var race in orderedRaces)
                {
                    foreach (var finish in race.Finishes.Where(f => f.Position > 0))
                    {
                        var resolvedTeamName = RaceTeamHelper.ResolveTeamForRaceDriver(
                            lookup, race, finish.Driver);

                        // Leere Auflösung früher: stillschweigend verworfen. Die Ligaseite
                        // zählt diese Punkte aber mit — seit dem Fix laufen sie auch hier
                        // als „Ohne Team" mit, statt aus der Gesamtwertung zu verschwinden.
                        var teamName = ConstructorTeamHelper.LabelFor(resolvedTeamName);
                        var isNoTeamBucket = ConstructorTeamHelper.IsNoTeamBucket(teamName);

                        var f1Team = isNoTeamBucket ? null : F1TeamsHelper.GetTeamByName(teamName);
                        var cssKey = isNoTeamBucket ? NoTeamCssKey : (f1Team?.CssKey ?? "unknown");
                        var displayName = f1Team?.Name ?? teamName;
                        var primaryColor = f1Team?.PrimaryColor ?? "#888888";
                        var secondaryColor = f1Team?.SecondaryColor ?? "#222222";

                        // Abgebrochene Rennen vergeben nur einen Anteil (siehe RacePointsFactor);
                        // der Faktor muss auch hier greifen, sonst weicht die Gesamtwertung
                        // von den Liga-Teamwertungen ab.
                        var points = SafePoints(finish.Position, race.PointsPercent);
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

            // Kein IsVisibleConstructor-Filter nötig (anders als auf der Ligaseite): ein Bucket
            // entsteht hier erst durch ein Finish mit Punkten, kann also gar nicht bei 0 stehen.
            return buckets.Values
                .OrderByDescending(b => b.Points)
                .ThenByDescending(b => b.Wins)
                .ThenByDescending(b => b.SecondPlaces)
                .ThenByDescending(b => b.ThirdPlaces)
                .ThenBy(b => b.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select((b, idx) => b.ToRow(idx + 1))
                .ToList();
        }

        private decimal SafePoints(int position, int pointsPercent)
        {
            var idx = position - 1;
            var basePoints = idx >= 0 && idx < _pointMap.Length ? _pointMap[idx] : 0;
            return RacePointsFactor.Apply(basePoints, pointsPercent);
        }

        private sealed class TeamBucket
        {
            public string CssKey { get; }
            public string DisplayName { get; }
            public string PrimaryColor { get; }
            public string SecondaryColor { get; }
            public decimal Points { get; private set; }
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

            public void AddRace(string leagueId, decimal points, int position)
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
            public decimal Points { get; private set; }
            public int Events { get; private set; }
            public int Wins { get; private set; }
            public int BestPosition { get; private set; } = int.MaxValue;

            public void Add(decimal points, int position)
            {
                Points += points;
                Events++;
                if (position == 1) Wins++;
                if (position > 0 && position < BestPosition) BestPosition = position;
            }
        }
    }
}
