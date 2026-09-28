using Erdi_ERC.Data;
using Erdi_ERC.Helpers;
using Erdi_ERC.Models;
using Erdi_ERC.Options;
using Erdi_ERC.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Erdi_ERC.Controllers
{
    /// <summary>Öffentliche Renn-Seiten: Standings, Liga-Ergebnisse, Renn-Details, Rennkalender.</summary>
    public class RacesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ApplicationOptions _appOptions;
        private readonly OverallConstructorsService _overallConstructors;

        public RacesController(AppDbContext db, IOptions<ApplicationOptions> appOptions, OverallConstructorsService overallConstructors)
        {
            _db = db;
            _appOptions = appOptions.Value;
            _overallConstructors = overallConstructors;
        }

        [Microsoft.AspNetCore.OutputCaching.OutputCache(PolicyName = "public-2min")]
        public async Task<IActionResult> Results()
        {
            var leagues = await _db.Leagues
                .AsSplitQuery()
                .Include(l => l.Standings)
                .Include(l => l.Races).ThenInclude(r => r.Finishes)
                .Include(l => l.Races).ThenInclude(r => r.ReserveAssignments)
                .OrderBy(l => l.Name)
                .ToListAsync();

            foreach (var l in leagues)
            {
                // Ersatzfahrer erst zeigen, wenn sie in dieser Liga wirklich gefahren sind
                // (siehe ReserveDriverFilter) — sonst stünden sie ab der Kader-Aufnahme
                // mit lauter DNS-Zellen in der Tabelle.
                l.Standings = ReserveDriverFilter.VisibleStandings(l);
                l.Races = l.Races.OrderBy(r => r.Date).ToList();
            }

            // Geplante Renngesamtzahl pro Liga aus dem Rennkalender, damit der
            // "Round X of N"-Header den echten Saisonumfang zeigt statt X von X.
            var calendarRaceCounts = await _db.RaceWeekendLegs
                .AsNoTracking()
                .GroupBy(l => l.LeagueId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, StringComparer.OrdinalIgnoreCase);

            return View(new Erdi10ViewModel
            {
                TwitchChannel = _appOptions.TwitchChannel,
                Leagues = leagues,
                CalendarRaceCounts = calendarRaceCounts
            });
        }

        [HttpGet]
        [Microsoft.AspNetCore.OutputCaching.OutputCache(PolicyName = "public-2min")]
        public async Task<IActionResult> LeagueResults(string leagueId)
        {
            if (string.IsNullOrWhiteSpace(leagueId)) return NotFound();

            var league = await _db.Leagues
                .AsNoTracking()
                .Include(l => l.Races).ThenInclude(r => r.Finishes)
                .Include(l => l.Races).ThenInclude(r => r.ReserveAssignments)
                .Include(l => l.Standings)
                .FirstOrDefaultAsync(l => l.Id == leagueId);

            if (league is null) return NotFound();

            league.Races = league.Races
                .OrderByDescending(r => r.Date)
                .ThenByDescending(r => r.RowId)
                .ToList();

            return View(new LeagueResultsViewModel
            {
                League = league,
                Races = league.Races
            });
        }

        /// <summary>
        /// Liga-übergreifende Constructors-Meisterschaft. Aggregiert die Punkte aller Ligen
        /// mit Opt-in <see cref="League.CountsTowardOverall"/> per <c>F1Team.CssKey</c>.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Constructors()
        {
            var overallRows = await _overallConstructors.ComputeAsync();

            // Liste der berücksichtigten Ligen (für Subtitle / Banner) — Read-only, klein.
            var leagues = await _db.Leagues
                .AsNoTracking()
                .Where(l => l.CountsTowardOverall)
                .OrderBy(l => l.SortOrder)
                .ThenBy(l => l.Name)
                .Select(l => new { l.Id, l.Name })
                .ToListAsync();

            return View(new OverallConstructorsViewModel
            {
                Rows = overallRows,
                OverallLeagueIds = leagues.Select(l => l.Id).ToList(),
                OverallLeagueNames = leagues.Select(l => l.Name).ToList(),
                TotalEvents = overallRows.Sum(r => r.Events)
            });
        }

        [HttpGet]
        [Microsoft.AspNetCore.OutputCaching.OutputCache(PolicyName = "public-2min")]
        public async Task<IActionResult> RaceDetail(string leagueId, int raceId)
        {
            var league = await _db.Leagues
                .Include(l => l.Standings)
                .FirstOrDefaultAsync(l => l.Id == leagueId);

            if (league is null) return NotFound();

            var race = await _db.RaceResults
                .Include(r => r.Finishes)
                .Include(r => r.ReserveAssignments)
                .FirstOrDefaultAsync(r => r.RowId == raceId && r.LeagueId == leagueId);

            if (race is null) return NotFound();

            var orderedFinishes = race.Finishes
                .Where(f => f.Position > 0)
                .OrderBy(f => f.Position)
                .ToList();

            var dnfFinishes = race.Finishes
                .Where(f => f.Position <= 0)
                .OrderBy(f => f.Driver)
                .ToList();

            var leaderMs = orderedFinishes.FirstOrDefault(f => f.RaceTimeMs.HasValue)?.RaceTimeMs;
            int[] pointMap = { 25, 21, 18, 16, 14, 12, 10, 8, 7, 6, 5, 4, 3, 2, 1, 0, 0, 0, 0, 0 };

            var rows = orderedFinishes.Select(f =>
            {
                var basePoints = (f.Position >= 1 && f.Position <= pointMap.Length)
                    ? pointMap[f.Position - 1]
                    : 0;

                return new RaceResultDetailRow
                {
                    Position = f.Position,
                    Driver = f.Driver,
                    Team = RaceTeamHelper.ResolveTeamForRaceDriver(league.Standings, race, f.Driver) ?? "",
                    // Abgebrochene Rennen vergeben nur einen Anteil (siehe RacePointsFactor).
                    Points = RacePointsFactor.Apply(basePoints, race.PointsPercent),
                    RaceTimeMs = f.RaceTimeMs,
                    GapToLeaderMs = leaderMs.HasValue && f.RaceTimeMs.HasValue
                        ? Math.Max(0, f.RaceTimeMs.Value - leaderMs.Value)
                        : null,
                    FastestLap = f.FastestLap
                };
            }).ToList();

            rows.AddRange(dnfFinishes.Select(f => new RaceResultDetailRow
            {
                Position = 0,
                Driver = f.Driver,
                Team = RaceTeamHelper.ResolveTeamForRaceDriver(league.Standings, race, f.Driver) ?? "",
                Points = 0,
                RaceTimeMs = null,
                GapToLeaderMs = null,
                FastestLap = f.FastestLap
            }));

            return View(new RaceResultDetailViewModel
            {
                League = league,
                Race = race,
                Rows = rows
            });
        }

        [HttpGet]
        [Microsoft.AspNetCore.OutputCaching.OutputCache(PolicyName = "public-2min")]
        public async Task<IActionResult> AllRaces()
        {
            var leagues = await _db.Leagues
                .AsNoTracking()
                .AsSplitQuery()
                .Include(l => l.Races).ThenInclude(r => r.Finishes)
                .Include(l => l.Races).ThenInclude(r => r.GuestAssignments)
                .OrderBy(l => l.Name)
                .ToListAsync();

            var vm = new AllRacesViewModel
            {
                Leagues = leagues
                    .Select(l => new AllRacesLeagueGroup
                    {
                        LeagueId = l.Id,
                        LeagueName = l.Name,
                        Races = l.Races
                            .OrderByDescending(r => r.Date)
                            .ThenByDescending(r => r.RowId)
                            .Select(r =>
                            {
                                var podium = r.Finishes
                                    .Where(f => f.Position > 0)
                                    .OrderBy(f => f.Position)
                                    .Take(3)
                                    .ToList();

                                static (bool isGuest, string? forMain) ResolveGuest(
                                    IEnumerable<RaceGuestAssignment> assignments, string? driver)
                                {
                                    if (string.IsNullOrWhiteSpace(driver)) return (false, null);
                                    var hit = assignments.FirstOrDefault(g =>
                                        !string.IsNullOrWhiteSpace(g.GuestDriver)
                                        && g.GuestDriver.Trim().Equals(driver.Trim(), StringComparison.OrdinalIgnoreCase)
                                        && !string.IsNullOrWhiteSpace(g.MainDriver)
                                        && g.MainDriver != StatsService.GuestSentinelNoMain);
                                    return hit is null ? (false, null) : (true, hit.MainDriver);
                                }

                                var (isGuestWinner, winnerForMain) = ResolveGuest(r.GuestAssignments, r.Winner);
                                var (isGuestP2, p2ForMain) = ResolveGuest(r.GuestAssignments, podium.Count > 1 ? podium[1].Driver : null);
                                var (isGuestP3, p3ForMain) = ResolveGuest(r.GuestAssignments, podium.Count > 2 ? podium[2].Driver : null);

                                return new AllRaceItem
                                {
                                    RaceId = r.RowId,
                                    Date = r.Date,
                                    Track = r.Track,
                                    Winner = r.Winner,
                                    WinnerRaceTimeMs = podium.FirstOrDefault()?.RaceTimeMs,
                                    P2 = podium.Count > 1 ? podium[1].Driver : null,
                                    P3 = podium.Count > 2 ? podium[2].Driver : null,
                                    IsGuestWinner = isGuestWinner,
                                    WinnerGuestForMain = winnerForMain,
                                    IsGuestP2 = isGuestP2,
                                    P2GuestForMain = p2ForMain,
                                    IsGuestP3 = isGuestP3,
                                    P3GuestForMain = p3ForMain
                                };
                            })
                            .ToList()
                    })
                    .Where(x => x.Races.Count > 0)
                    .ToList()
            };

            return View(vm);
        }

        [HttpGet]
        [Microsoft.AspNetCore.OutputCaching.OutputCache(PolicyName = "public-2min")]
        public async Task<IActionResult> RaceCalendar()
        {
            var settings = await _db.RaceCalendarSettings.FirstOrDefaultAsync()
                ?? new RaceCalendarSettings();

            var leagues = await _db.Leagues
                .Where(l => !l.IsArchived)
                .OrderBy(l => l.Name)
                .ToListAsync();

            var weekends = await _db.RaceWeekends
                .Include(w => w.Legs)
                .OrderBy(w => w.Order)
                .ToListAsync();

            // Geplante Termine mit bereits eingetragenen Ergebnissen verknüpfen (Liga + Strecke),
            // damit der Kalender pro Leg direkt auf das gefahrene Rennen verlinken kann.
            var legResultMap = new Dictionary<int, int>();
            var legsWithTrack = weekends
                .SelectMany(w => w.Legs.Select(l => new { Leg = l, w.Track }))
                .ToList();
            if (legsWithTrack.Count > 0)
            {
                var legLeagueIds = legsWithTrack.Select(x => x.Leg.LeagueId).Distinct().ToList();
                var candidateResults = await _db.RaceResults
                    .Where(r => legLeagueIds.Contains(r.LeagueId))
                    .Select(r => new { r.RowId, r.LeagueId, r.Track })
                    .ToListAsync();
                foreach (var item in legsWithTrack)
                {
                    var match = candidateResults.FirstOrDefault(r =>
                        string.Equals(r.LeagueId, item.Leg.LeagueId, StringComparison.OrdinalIgnoreCase)
                        && string.Equals((r.Track ?? string.Empty).Trim(), (item.Track ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));
                    if (match != null) legResultMap[item.Leg.Id] = match.RowId;
                }
            }

            ViewBag.CalendarSettings = settings;
            ViewBag.Leagues = leagues;
            ViewBag.Weekends = weekends;
            ViewBag.LegResultMap = legResultMap;
            return View();
        }

        [HttpGet]
        public IActionResult DriverDetail(string leagueId, string driver)
        {
            if (string.IsNullOrWhiteSpace(driver)) return NotFound();

            return RedirectToAction("ByDriverName", "Profile", new { driverName = driver.Trim() });
        }
    }
}
