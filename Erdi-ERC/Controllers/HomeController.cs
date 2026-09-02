using Erdi_ERC.Data;
using Erdi_ERC.Helpers;
using Erdi_ERC.Models;
using Erdi_ERC.Options;
using Erdi_ERC.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Erdi_ERC.Controllers
{
    /// <summary>Startseite, statische Seiten (Impressum, FAQ, Regelwerk, About …), Error und Hintergrundmusik-API.</summary>
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly BackgroundMusicOptions _backgroundMusic;
        private readonly IMemoryCache _cache;
        private readonly HomeIndexDataService _homeIndexData;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            AppDbContext db,
            IWebHostEnvironment env,
            IOptions<BackgroundMusicOptions> backgroundMusic,
            IMemoryCache cache,
            HomeIndexDataService homeIndexData,
            ILogger<HomeController> logger)
        {
            _db = db;
            _env = env;
            _backgroundMusic = backgroundMusic.Value;
            _cache = cache;
            _homeIndexData = homeIndexData;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            // Alle ViewBag-Daten kommen aus dem 60s-gecachten HomeIndexDataService —
            // die ~13 Queries (inkl. Last-Winner-Historie) laufen nur 1x pro Minute.
            var data = await _homeIndexData.GetAsync();

            ViewBag.LeagueCount = data.LeagueCount;
            ViewBag.DriverCount = data.DriverCount;
            ViewBag.RaceCount = data.RaceCount;
            ViewBag.UpcomingCount = data.UpcomingCount;
            ViewBag.NextUpcomingEvent = data.NextUpcomingEvent;
            ViewBag.NextStream = data.NextStream;
            ViewBag.HasTrackSetups = data.HasTrackSetups;
            ViewBag.OverallConstructorsTop3 = data.OverallConstructorsTop3;
            ViewBag.CommunityNews = data.CommunityNews;
            ViewBag.LeaguePreview = data.LeaguePreview;
            ViewBag.LastWinners = data.LastWinners;

            return View();
        }

        public IActionResult Privacy() => View();
        public IActionResult Impressum() => View();
        public IActionResult Widerruf() => View();
        public IActionResult Faq() => View();
        public IActionResult Regelwerk() => View();
        public IActionResult Maintenance() => View("Maintenance");

        /// <summary>Liefert die neueste .exe aus dem downloads-Ordner als Download — kein fester Dateiname.</summary>
        [HttpGet]
        public IActionResult DownloadApp()
        {
            var downloadsPath = Path.Combine(_env.ContentRootPath, "downloads");

            var installer = Directory.EnumerateFiles(downloadsPath, "*.exe")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();

            if (installer is null)
            {
                _logger.LogWarning("Download angefragt, aber keine .exe im Ordner {DownloadsPath}", downloadsPath);
                return NotFound();
            }

            return PhysicalFile(installer.FullName, "application/octet-stream", installer.Name);
        }

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
        public async Task<IActionResult> Stewarding(string? league = null)
        {
            // Liga-Filter-Buttons werden aus den LeagueId-Werten der oeffentlichen
            // Berichte abgeleitet — so taucht auch eine inzwischen archivierte Liga
            // als Button auf, solange es noch oeffentliche Dokumente fuer sie gibt.
            // Namen kommen aus der Leagues-Tabelle (Fallback: rohe LeagueId).
            var query = _db.LeaguePenalties.Where(x => x.IsPublic);
            if (!string.IsNullOrWhiteSpace(league))
            {
                var needle = league.Trim().ToLower();
                query = query.Where(x => x.LeagueId != null && x.LeagueId.ToLower() == needle);
            }

            var penalties = await query
                .OrderByDescending(x => x.Date)
                .ThenBy(x => x.Driver)
                .ToListAsync();

            // Liga-Tabelle fuer Namen — bewusst NICHT auf !IsArchived gefiltert,
            // damit archivierte Ligen mit oeffentlichen Alt-Berichten ihren Namen zeigen.
            var leagueNames = await _db.Leagues
                .Select(l => new { l.Id, l.Name, l.IsArchived })
                .ToListAsync();
            var leagueLookup = leagueNames.ToDictionary(
                x => x.Id,
                x => new { x.Name, x.IsArchived },
                StringComparer.OrdinalIgnoreCase);

            // Eindeutige Liga-IDs aus den oeffentlichen Berichten ableiten,
            // stabil sortiert (zunaechst nach "ist noch aktiv" zuerst, dann alphabetisch).
            var leagueIds = penalties
                .Select(p => p.LeagueId ?? string.Empty)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id =>
                {
                    leagueLookup.TryGetValue(id, out var info);
                    var isArchived = info?.IsArchived ?? false;
                    return isArchived ? 1 : 0;
                })
                .ThenBy(id =>
                {
                    leagueLookup.TryGetValue(id, out var info);
                    return info?.Name ?? id;
                }, StringComparer.OrdinalIgnoreCase)
                .Select(id => new
                {
                    Id         = id,
                    Name       = leagueLookup.TryGetValue(id, out var info) ? info.Name : id,
                    IsArchived = leagueLookup.TryGetValue(id, out var info2) && info2.IsArchived
                })
                .ToList();

            ViewBag.ActiveLeagues = leagueIds;
            ViewBag.LeagueFilter  = league ?? "";
            return View(penalties);
        }

        [HttpGet]
        [Route("Stewarding/Doc/{id:int}")]
        public async Task<IActionResult> StewardingDoc(int id)
        {
            // Öffentliche Detail-Ansicht eines Steward-Dokuments (eigener Tab).
            // Nur oeffentliche Dokumente sind erreichbar — interne bleiben
            // strikt im Admin-Bereich hinter [Authorize].
            var penalty = await _db.LeaguePenalties
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.IsPublic);
            if (penalty is null) return NotFound();

            return View(penalty);
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
