using Erdi_ERC.Data;
using Erdi_ERC.Helpers;
using Erdi_ERC.Models;
using Erdi_ERC.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Erdi_ERC.Services
{
    public sealed record HomeNextEvent(string Track, DateTime Date, string Format, string LeagueId);
    public sealed record HomeLeagueNextEvent(string Track, DateTime Date, string Format);
    public sealed record HomeLeaguePreview(string Id, string Name, string? Description, int Drivers, HomeLeagueNextEvent? NextEvent);
    public sealed record HomeLastWinner(
        string Id, string Name, string? Winner, string? Track, DateTime? Date,
        int? DriverPoints, string? WinnerTeam, int? TeamPoints,
        bool IsReserveWinner, string? ReserveForDriver, string? ReserveForInRace,
        bool IsGuestWinner, string? GuestForMain);

    /// <summary>Alle ViewBag-Daten von <c>HomeController.Index</c> in einem Objekt.</summary>
    public sealed record HomeIndexData(
        int LeagueCount, int DriverCount, int RaceCount, int UpcomingCount,
        HomeNextEvent? NextUpcomingEvent, StreamSchedule? NextStream, bool HasTrackSetups,
        List<OverallConstructorRow> OverallConstructorsTop3,
        List<CommunityNewsPost> CommunityNews,
        List<HomeLeaguePreview> LeaguePreview,
        List<HomeLastWinner> LastWinners);

    /// <summary>
    /// Lädt alle Daten der Startseite in einem Objekt und cached es 60 s (Key
    /// <c>home-index:v1</c>). Vorher lief jede der ~13 Queries pro Page-Hit — inklusive
    /// des Last-Winner-Blocks, der die komplette Rennhistorie aller Ligen mit vollem
    /// Entity-Graph lud. Die Rennen werden jetzt als schlanke Projektion geladen
    /// (nur Fahrer/Position/Zuordnungen statt voller <see cref="RaceResult"/>-Graph).
    /// <para>Kein <c>Task.WhenAll</c>: ein scoped <see cref="AppDbContext"/>, keine
    /// konkurrierenden Operationen.</para>
    /// </summary>
    public sealed class HomeIndexDataService
    {
        private const string CacheKey = "home-index:v1";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly OverallConstructorsService _overallConstructors;
        private readonly IStreamScheduleQueryService _streamSchedules;
        private readonly ILogger<HomeIndexDataService> _logger;
        private readonly int[] _pointMap;

        public HomeIndexDataService(
            AppDbContext db,
            IMemoryCache cache,
            OverallConstructorsService overallConstructors,
            IStreamScheduleQueryService streamSchedules,
            IOptions<F1ScoringOptions> f1Scoring,
            ILogger<HomeIndexDataService> logger)
        {
            _db = db;
            _cache = cache;
            _overallConstructors = overallConstructors;
            _streamSchedules = streamSchedules;
            var configured = f1Scoring.Value.PointMap;
            _pointMap = configured is { Length: > 0 }
                ? configured
                : new[] { 25, 21, 18, 16, 14, 12, 10, 8, 7, 6, 5, 4, 3, 2, 1, 0, 0, 0, 0, 0 };
            _logger = logger;
        }

        public async Task<HomeIndexData> GetAsync(CancellationToken ct = default)
        {
            if (_cache.TryGetValue<HomeIndexData>(CacheKey, out var cached) && cached is not null)
            {
                return cached;
            }

            var data = await BuildAsync(ct);

            _cache.Set(CacheKey, data, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl,
                Priority = CacheItemPriority.Low,
                Size = 1
            });

            return data;
        }

        private async Task<HomeIndexData> BuildAsync(CancellationToken ct)
        {
            var currentLeagueIds = await _db.Leagues
                .Where(l => !l.IsArchived)
                .Select(l => l.Id)
                .ToListAsync(ct);

            var leagueCount = await _db.Leagues.CountAsync(ct);
            var driverCount = await _db.DriverStandings
                .Where(s => currentLeagueIds.Contains(s.LeagueId) && !string.IsNullOrWhiteSpace(s.Driver))
                .CountAsync(ct);
            var raceCount = await _db.RaceResults.CountAsync(ct);
            var upcomingCount = await _db.RaceWeekendLegs.CountAsync(l => l.Date >= DateTime.UtcNow.Date, ct);

            var nextUpcomingEvent = await _db.RaceWeekendLegs
                .Where(l => l.Date >= DateTime.UtcNow)
                .OrderBy(l => l.Date)
                .Select(l => new HomeNextEvent(
                    l.Weekend!.Track,
                    l.Date,
                    l.Weekend!.DistancePercent + "% Race",
                    l.LeagueId))
                .FirstOrDefaultAsync(ct);

            var nextStream = await _streamSchedules.GetNextStreamScheduleAsync(ct);
            var hasTrackSetups = await _db.TrackSetups.AnyAsync(ct);

            // Fail-open: Wenn die Aggregation hängt (z.B. defekte League-Daten), blenden
            // wir den Chip einfach aus — die restliche Startseite muss weiterlaufen.
            var overallTop3 = new List<OverallConstructorRow>();
            try
            {
                var overallRows = await _overallConstructors.ComputeAsync(ct);
                overallTop3 = overallRows.Take(3).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OverallConstructors.Top3 konnte nicht berechnet werden — Chip wird ausgeblendet.");
            }

            var communityNews = await _db.CommunityNewsPosts
                .Where(x => x.IsPublished)
                .OrderByDescending(x => x.IsPinned)
                .ThenByDescending(x => x.PublishedAt)
                .Take(4)
                .ToListAsync(ct);

            var leaguePreviewBase = await _db.Leagues
                .OrderBy(l => l.Name)
                .Select(l => new
                {
                    l.Id,
                    l.Name,
                    l.Description,
                    Drivers = l.Standings.Count
                })
                .ToListAsync(ct);

            var nextLegsByLeague = await _db.RaceWeekendLegs
                .Include(l => l.Weekend)
                .Where(l => l.Date >= DateTime.Today)
                .OrderBy(l => l.Date)
                .ToListAsync(ct);

            var leaguePreview = leaguePreviewBase
                .Select(l =>
                {
                    var next = nextLegsByLeague.FirstOrDefault(x => x.LeagueId == l.Id);
                    return new HomeLeaguePreview(
                        l.Id, l.Name, l.Description, l.Drivers,
                        next is null
                            ? null
                            : new HomeLeagueNextEvent(
                                next.Weekend?.Track ?? string.Empty,
                                next.Date,
                                (next.Weekend?.DistancePercent ?? 100) + "% Race"));
                })
                .ToList();

            var lastWinners = await BuildLastWinnersAsync(ct);

            return new HomeIndexData(
                leagueCount, driverCount, raceCount, upcomingCount,
                nextUpcomingEvent, nextStream, hasTrackSetups,
                overallTop3, communityNews, leaguePreview, lastWinners);
        }

        // --- Last Winners -------------------------------------------------

        private sealed record RaceRow(
            int RowId, string LeagueId, DateTime Date, string Track, string Winner,
            List<FinishRow> Finishes, List<PairRow> Reserves, List<PairRow> Guests);
        private sealed record FinishRow(string Driver, int Position);
        private sealed record PairRow(string A, string B);

        /// <summary>
        /// Letzter Sieger pro Liga. Statt des Include-Graphs (alle Rennen mit vollen
        /// Finish-Entities) reicht eine schlanke Projektion: Fahrer + Position + die
        /// beiden Zuordnungs-Tabellen. Die Team-Punkte-Summe braucht alle Finishes
        /// (alle Rennen zählen), aber nur diese Spalten — das reduziert das
        /// Transfer-Volumen deutlich. Auflösung läuft danach über
        /// <see cref="RaceTeamHelper"/> mit einem <see cref="RaceTeamLookup"/> pro Liga.
        /// </summary>
        private async Task<List<HomeLastWinner>> BuildLastWinnersAsync(CancellationToken ct)
        {
            var leagues = await _db.Leagues
                .AsNoTracking()
                .OrderBy(l => l.Name)
                .Select(l => new { l.Id, l.Name })
                .ToListAsync(ct);
            if (leagues.Count == 0) return new List<HomeLastWinner>();

            var standings = await _db.DriverStandings
                .AsNoTracking()
                .Select(s => new { s.LeagueId, s.Driver, s.Team, s.Points, s.IsReserveDriver, s.ReserveForDriver })
                .ToListAsync(ct);

            var races = await _db.RaceResults
                .AsNoTracking()
                .Select(r => new RaceRow(
                    r.RowId,
                    r.LeagueId,
                    r.Date,
                    r.Track,
                    r.Winner,
                    r.Finishes.Select(f => new FinishRow(f.Driver, f.Position)).ToList(),
                    r.ReserveAssignments.Select(a => new PairRow(a.ReserveDriver, a.MainDriver)).ToList(),
                    r.GuestAssignments.Select(g => new PairRow(g.GuestDriver, g.MainDriver)).ToList()))
                .ToListAsync(ct);

            var result = new List<HomeLastWinner>(leagues.Count);

            foreach (var league in leagues)
            {
                var leagueRaces = races
                    .Where(r => r.LeagueId == league.Id)
                    .OrderBy(r => r.Date)
                    .ThenBy(r => r.RowId)
                    .ToList();

                var leagueStandings = standings
                    .Where(s => s.LeagueId == league.Id)
                    .Select(s => new DriverStanding
                    {
                        Driver = s.Driver,
                        Team = s.Team,
                        Points = s.Points,
                        IsReserveDriver = s.IsReserveDriver,
                        ReserveForDriver = s.ReserveForDriver
                    })
                    .ToList();

                // Synthetische Entities (nur die Felder, die RaceTeamHelper liest),
                // damit die bewährte Auflösungslogik unverändert weiterläuft.
                var syntheticLeague = new League
                {
                    Id = league.Id,
                    Name = league.Name,
                    Standings = leagueStandings,
                    Races = leagueRaces.Select(ToRaceResult).ToList()
                };

                var lookup = new RaceTeamLookup(syntheticLeague.Standings);

                var lastRace = leagueRaces
                    .Where(r => !string.IsNullOrWhiteSpace(r.Winner))
                    .OrderByDescending(r => r.Date)
                    .ThenByDescending(r => r.RowId)
                    .FirstOrDefault();

                if (lastRace is null)
                {
                    result.Add(new HomeLastWinner(
                        league.Id, league.Name,
                        Winner: null, Track: null, Date: null,
                        DriverPoints: null, WinnerTeam: null, TeamPoints: null,
                        IsReserveWinner: false, ReserveForDriver: null, ReserveForInRace: null,
                        IsGuestWinner: false, GuestForMain: null));
                    continue;
                }

                var lastRaceEntity = ToRaceResult(lastRace);
                var winner = lastRace.Winner.Trim();

                var winnerStanding = leagueStandings.FirstOrDefault(s =>
                    !string.IsNullOrWhiteSpace(s.Driver) &&
                    s.Driver.Trim().Equals(winner, StringComparison.OrdinalIgnoreCase));

                var raceReserveMain = RaceTeamLookup.ReserveMainDriver(lastRaceEntity, winner);

                // Cross-League-Gast: Winner kommt aus anderer Liga → Marker im View.
                var raceGuestMain = RaceTeamLookup.GuestMainDriver(lastRaceEntity, winner);

                var effectiveWinnerTeam = RaceTeamHelper.ResolveTeamForRaceDriver(lookup, lastRaceEntity, lastRace.Winner!);
                var isReserveWinner = winnerStanding?.IsReserveDriver == true || !string.IsNullOrWhiteSpace(raceReserveMain);
                var isGuestWinner = !isReserveWinner && !string.IsNullOrWhiteSpace(raceGuestMain);
                var droveForMultipleTeams = isReserveWinner && RaceTeamHelper.HasDrivenForMultipleTeams(syntheticLeague, lastRace.Winner!);
                var teamPoints = droveForMultipleTeams ? null : RaceTeamHelper.ComputeTeamPointsForLeague(syntheticLeague, effectiveWinnerTeam, _pointMap);

                result.Add(new HomeLastWinner(
                    league.Id, league.Name,
                    Winner: lastRace.Winner,
                    Track: lastRace.Track,
                    Date: lastRace.Date,
                    DriverPoints: winnerStanding?.Points,
                    WinnerTeam: effectiveWinnerTeam,
                    TeamPoints: teamPoints,
                    IsReserveWinner: isReserveWinner,
                    ReserveForDriver: raceReserveMain ?? winnerStanding?.ReserveForDriver,
                    ReserveForInRace: raceReserveMain,
                    IsGuestWinner: isGuestWinner,
                    GuestForMain: raceGuestMain));
            }

            return result;
        }

        private static RaceResult ToRaceResult(RaceRow r) => new()
        {
            RowId = r.RowId,
            LeagueId = r.LeagueId,
            Date = r.Date,
            Track = r.Track,
            Winner = r.Winner,
            Finishes = r.Finishes.Select(f => new RaceFinish { Driver = f.Driver, Position = f.Position }).ToList(),
            ReserveAssignments = r.Reserves.Select(a => new RaceReserveAssignment { ReserveDriver = a.A, MainDriver = a.B }).ToList(),
            GuestAssignments = r.Guests.Select(g => new RaceGuestAssignment { GuestDriver = g.A, MainDriver = g.B }).ToList()
        };
    }
}