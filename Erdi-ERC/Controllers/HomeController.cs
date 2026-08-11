using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Helpers;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Options;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace <OWNER_HANDLE>_ERC.Controllers
{
    /// <summary>Startseite, statische Seiten (Impressum, FAQ, Regelwerk, About …), Error und Hintergrundmusik-API.</summary>
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly BackgroundMusicOptions _backgroundMusic;
        private readonly IMemoryCache _cache;
        private readonly OverallConstructorsService _overallConstructors;
        private readonly IStreamScheduleQueryService _streamSchedules;
        private readonly ILogger<HomeController> _logger;
        private readonly int[] _f1PointMap;

        public HomeController(
            AppDbContext db,
            IWebHostEnvironment env,
            IOptions<BackgroundMusicOptions> backgroundMusic,
            IOptions<F1ScoringOptions> f1Scoring,
            IMemoryCache cache,
            OverallConstructorsService overallConstructors,
            IStreamScheduleQueryService streamSchedules,
            ILogger<HomeController> logger)
        {
            _db = db;
            _env = env;
            _backgroundMusic = backgroundMusic.Value;
            var configuredMap = f1Scoring.Value.PointMap;
            _f1PointMap = configuredMap is { Length: > 0 }
                ? configuredMap
                : new[] { 25, 21, 18, 16, 14, 12, 10, 8, 7, 6, 5, 4, 3, 2, 1, 0, 0, 0, 0, 0 };
            _cache = cache;
            _overallConstructors = overallConstructors;
            _streamSchedules = streamSchedules;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var currentLeagueIds = await _db.Leagues
                .Where(l => !l.IsArchived)
                .Select(l => l.Id)
                .ToListAsync();

            ViewBag.LeagueCount = await _db.Leagues.CountAsync();
            ViewBag.DriverCount = await _db.DriverStandings
                .Where(s => currentLeagueIds.Contains(s.LeagueId) && !string.IsNullOrWhiteSpace(s.Driver))
                .CountAsync();
            ViewBag.RaceCount = await _db.RaceResults.CountAsync();
            ViewBag.UpcomingCount = await _db.RaceWeekendLegs.CountAsync(l => l.Date >= DateTime.UtcNow.Date);
            ViewBag.NextUpcomingEvent = await _db.RaceWeekendLegs
                .Include(l => l.Weekend)
                .Where(l => l.Date >= DateTime.UtcNow)
                .OrderBy(l => l.Date)
                .Select(l => new
                {
                    Track  = l.Weekend!.Track,
                    Date   = l.Date,
                    Format = l.Weekend!.DistancePercent + "% Race",
                    LeagueId = l.LeagueId
                })
                .FirstOrDefaultAsync();
            ViewBag.NextStream = await _streamSchedules.GetNextStreamScheduleAsync();
            ViewBag.HasTrackSetups = await _db.TrackSetups.AnyAsync();

            // Liga-übergreifende Constructors: Top-3 für den Chip auf der Startseite.
            // Vollberechnung läuft nur einmal pro Request; das ist günstig genug, ohne
            // einen eigenen Cache-Layer, weil die Seite ohnehin aggregiert rendert.
            // Fail-open: Wenn die Aggregation hängt (z.B. defekte League-Daten), blenden
            // wir den Chip einfach aus — die restliche Startseite muss weiterlaufen.
            try
            {
                var overallRows = await _overallConstructors.ComputeAsync();
                ViewBag.OverallConstructorsTop3 = overallRows.Take(3).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OverallConstructors.Top3 konnte nicht berechnet werden — Chip wird ausgeblendet.");
                ViewBag.OverallConstructorsTop3 = new List<OverallConstructorRow>();
            }
            ViewBag.CommunityNews = await _db.CommunityNewsPosts
                .Where(x => x.IsPublished)
                .OrderByDescending(x => x.IsPinned)
                .ThenByDescending(x => x.PublishedAt)
                .Take(4)
                .ToListAsync();

            var leaguePreviewBase = await _db.Leagues
                .OrderBy(l => l.Name)
                .Select(l => new
                {
                    l.Id,
                    l.Name,
                    l.Description,
                    Drivers = l.Standings.Count
                })
                .ToListAsync();

            var nextLegsByLeague = await _db.RaceWeekendLegs
                .Include(l => l.Weekend)
                .Where(l => l.Date >= DateTime.Today)
                .OrderBy(l => l.Date)
                .ToListAsync();

            ViewBag.LeaguePreview = leaguePreviewBase
                .Select(l =>
                {
                    var next = nextLegsByLeague.FirstOrDefault(x => x.LeagueId == l.Id);
                    return new
                    {
                        l.Id,
                        l.Name,
                        l.Description,
                        l.Drivers,
                        NextEvent = next is null
                            ? null
                            : new
                            {
                                Track  = next.Weekend?.Track ?? string.Empty,
                                Date   = next.Date,
                                Format = (next.Weekend?.DistancePercent ?? 100) + "% Race"
                            }
                    };
                })
                .ToList();

            var leaguesForWinners = await _db.Leagues
                .AsNoTracking()
                .Include(l => l.Races).ThenInclude(r => r.Finishes)
                .Include(l => l.Races).ThenInclude(r => r.ReserveAssignments)
                .Include(l => l.Races).ThenInclude(r => r.GuestAssignments)
                .Include(l => l.Standings)
                .OrderBy(l => l.Name)
                .ToListAsync();

            ViewBag.LastWinners = leaguesForWinners
                .Select(l =>
                {
                    var lastRace = l.Races
                        .Where(r => !string.IsNullOrWhiteSpace(r.Winner))
                        .OrderByDescending(r => r.Date)
                        .ThenByDescending(r => r.RowId)
                        .FirstOrDefault();

                    if (lastRace == null)
                    {
                        return new
                        {
                            l.Id,
                            l.Name,
                            Winner = (string?)null,
                            Track = (string?)null,
                            Date = (DateTime?)null,
                            DriverPoints = (int?)null,
                            WinnerTeam = (string?)null,
                            TeamPoints = (int?)null,
                            IsReserveWinner = false,
                            ReserveForDriver = (string?)null,
                            ReserveForInRace = (string?)null,
                            IsGuestWinner = false,
                            GuestForMain = (string?)null
                        };
                    }

                    var winnerStanding = l.Standings.FirstOrDefault(s =>
                        !string.IsNullOrWhiteSpace(s.Driver) &&
                        s.Driver.Trim().Equals(lastRace.Winner!.Trim(), StringComparison.OrdinalIgnoreCase));

                    var raceReserveMain = lastRace.ReserveAssignments.FirstOrDefault(a =>
                        !string.IsNullOrWhiteSpace(a.ReserveDriver) &&
                        a.ReserveDriver.Trim().Equals(lastRace.Winner!.Trim(), StringComparison.OrdinalIgnoreCase))?.MainDriver;

                    // Cross-League-Gast: Winner kommt aus anderer Liga → Marker im View.
                    var raceGuestMain = lastRace.GuestAssignments?.FirstOrDefault(g =>
                        !string.IsNullOrWhiteSpace(g.GuestDriver) &&
                        g.GuestDriver.Trim().Equals(lastRace.Winner!.Trim(), StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(g.MainDriver) &&
                        g.MainDriver != StatsService.GuestSentinelNoMain)?.MainDriver;

                    var effectiveWinnerTeam = RaceTeamHelper.ResolveTeamForRaceDriver(l.Standings, lastRace, lastRace.Winner!);
                    var isReserveWinner = winnerStanding?.IsReserveDriver == true || !string.IsNullOrWhiteSpace(raceReserveMain);
                    var isGuestWinner  = !isReserveWinner && !string.IsNullOrWhiteSpace(raceGuestMain);
                    var droveForMultipleTeams = isReserveWinner && RaceTeamHelper.HasDrivenForMultipleTeams(l, lastRace.Winner!);
                    var teamPoints = droveForMultipleTeams ? null : RaceTeamHelper.ComputeTeamPointsForLeague(l, effectiveWinnerTeam, _f1PointMap);

                    return new
                    {
                        l.Id,
                        l.Name,
                        Winner = (string?)lastRace.Winner,
                        Track = (string?)lastRace.Track,
                        Date = (DateTime?)lastRace.Date,
                        DriverPoints = (int?)winnerStanding?.Points,
                        WinnerTeam = (string?)effectiveWinnerTeam,
                        TeamPoints = teamPoints,
                        IsReserveWinner = isReserveWinner,
                        ReserveForDriver = (string?)(raceReserveMain ?? winnerStanding?.ReserveForDriver),
                        ReserveForInRace = (string?)raceReserveMain,
                        IsGuestWinner = isGuestWinner,
                        GuestForMain = (string?)raceGuestMain
                    };
                })
                .ToList();

            return View();
        }

        public IActionResult Privacy() => View();
        public IActionResult Impressum() => View();
        public IActionResult Widerruf() => View();
        public IActionResult Faq() => View();
        public IActionResult Regelwerk() => View();
        public IActionResult Maintenance() => View("Maintenance");

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private sealed record BackgroundTrack(string Title, string Url);

        [HttpGet]
        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
        [Microsoft.AspNetCore.OutputCaching.OutputCache(PolicyName = "public-2min")]
        public IActionResult GetBackgroundMusicTracks()
        {
            // Server-seitiger Cache: Directory.GetFiles ist sonst pro Page-Load ein Disk-Scan.
            // 5 Min TTL ist sicher: neue Tracks landen über Admin-Upload und werden manuell ergänzt.
            var tracks = _cache.GetOrCreate("bg-music-tracks:v1", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                entry.Size = 1;
                entry.Priority = CacheItemPriority.Low;

                var musicDir = Path.Combine(_env.ContentRootPath, _backgroundMusic.Directory);
                if (!Directory.Exists(musicDir))
                {
                    return Array.Empty<BackgroundTrack>();
                }

                var supportedExtensions = _backgroundMusic.AllowedExtensions;
                return Directory.GetFiles(musicDir)
                    .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .Select(f => new BackgroundTrack(
                        Title: Path.GetFileNameWithoutExtension(f),
                        Url: $"/backgroundmusik/{Uri.EscapeDataString(Path.GetFileName(f))}"))
                    .OrderBy(t => t.Title)
                    .ToArray();
            }) ?? Array.Empty<BackgroundTrack>();

            return Json(tracks);
        }

        [HttpGet]
        public async Task<IActionResult> Stewarding()
        {
            var penalties = await _db.LeaguePenalties
                .Where(x => x.IsPublic)
                .OrderByDescending(x => x.Date)
                .ThenBy(x => x.Driver)
                .ToListAsync();
            return View(penalties);
        }

        // ── About ────────────────────────────────────────────────────────────────
        [HttpGet]
        [Route("about/{slug?}")]
        public async Task<IActionResult> About(string? slug = null)
        {
            AboutMeProfile? profile;

            if (!string.IsNullOrEmpty(slug))
            {
                profile = await _db.AboutMeProfiles
                    .FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublic);
                if (profile == null) return NotFound();
            }
            else
            {
                // Default: erstes öffentliches Profil (Besitzer-Profil)
                profile = await _db.AboutMeProfiles
                    .Where(p => p.IsPublic)
                    .OrderBy(p => p.SortOrder)
                    .FirstOrDefaultAsync();
                if (profile == null) return View("~/Views/Home/About.cshtml", (AboutMeProfile?)null);
            }

            return View("~/Views/Home/About.cshtml", profile);
        }

    }
}
