using ClosedXML.Excel;
using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Helpers;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Options;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

namespace <OWNER_HANDLE>_ERC.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ITrackSetupAccessPolicy _trackSetupAccessPolicy;
        private readonly ApplicationOptions _appOptions;
        private readonly DiscordGuildOptions _discordGuilds;
        private readonly DiscordSetupAccessOptions _setupAccessOptions;
        private readonly BackgroundMusicOptions _backgroundMusic;
        private readonly ILogger<HomeController> _logger;
        private readonly IWebhookAutomationService _webhookAuto;
        private readonly IMemoryCache _cache;
        private readonly IHttpClientFactory _httpClientFactory;

        public HomeController(
            AppDbContext db,
            IWebHostEnvironment env,
            ITrackSetupAccessPolicy trackSetupAccessPolicy,
            IOptions<ApplicationOptions> appOptions,
            IOptions<DiscordGuildOptions> discordGuilds,
            IOptions<DiscordSetupAccessOptions> setupAccessOptions,
            IOptions<BackgroundMusicOptions> backgroundMusic,
            ILogger<HomeController> logger,
            IWebhookAutomationService webhookAuto,
            IMemoryCache cache,
            IHttpClientFactory httpClientFactory)
        {
            _db = db;
            _env = env;
            _trackSetupAccessPolicy = trackSetupAccessPolicy;
            _appOptions = appOptions.Value;
            _discordGuilds = discordGuilds.Value;
            _setupAccessOptions = setupAccessOptions.Value;
            _backgroundMusic = backgroundMusic.Value;
            _logger = logger;
            _webhookAuto = webhookAuto;
            _cache = cache;
            _httpClientFactory = httpClientFactory;
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
            ViewBag.NextStream = await GetNextStreamScheduleAsync();
            ViewBag.HasTrackSetups = await _db.TrackSetups.AnyAsync();
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
                .Include(l => l.Races).ThenInclude(r => r.Finishes)
                .Include(l => l.Races).ThenInclude(r => r.ReserveAssignments)
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
                            ReserveForInRace = (string?)null
                        };
                    }

                    var winnerStanding = l.Standings.FirstOrDefault(s =>
                        !string.IsNullOrWhiteSpace(s.Driver) &&
                        s.Driver.Trim().Equals(lastRace.Winner!.Trim(), StringComparison.OrdinalIgnoreCase));

                    var raceReserveMain = lastRace.ReserveAssignments.FirstOrDefault(a =>
                        !string.IsNullOrWhiteSpace(a.ReserveDriver) &&
                        a.ReserveDriver.Trim().Equals(lastRace.Winner!.Trim(), StringComparison.OrdinalIgnoreCase))?.MainDriver;

                    var effectiveWinnerTeam = ResolveTeamForRaceDriver(l.Standings, lastRace, lastRace.Winner!);
                    var isReserveWinner = winnerStanding?.IsReserveDriver == true || !string.IsNullOrWhiteSpace(raceReserveMain);
                    var droveForMultipleTeams = isReserveWinner && HasDrivenForMultipleTeams(l, lastRace.Winner!);
                    var teamPoints = droveForMultipleTeams ? null : ComputeTeamPointsForLeague(l, effectiveWinnerTeam);

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
                        ReserveForInRace = (string?)raceReserveMain
                    };
                })
                .ToList();

            return View();
        }

        public IActionResult Privacy() => View();
        public IActionResult Impressum() => View();
        public IActionResult Faq() => View();
        public IActionResult Regelwerk() => View();

        public async Task<IActionResult> Events()
        {
            var events = await _db.RealLifeEvents
                .OrderByDescending(e => e.IsUpcoming)
                .ThenBy(e => e.IsUpcoming ? e.Date : DateTime.MaxValue)
                .ThenByDescending(e => e.Date)
                .ToListAsync();
            return View(events);
        }

        public async Task<IActionResult> EventDetail(int id)
        {
            var ev = await _db.RealLifeEvents
                .Include(e => e.Images.OrderBy(i => i.UploadedAt))
                .FirstOrDefaultAsync(e => e.Id == id);
            if (ev is null) return NotFound();
            return View(ev);
        }

        public async Task<IActionResult> <OWNER_HANDLE>10()
        {
            var leagues = await _db.Leagues
                .Include(l => l.Standings)
                .Include(l => l.Races).ThenInclude(r => r.Finishes)
                .Include(l => l.Races).ThenInclude(r => r.ReserveAssignments)
                .OrderBy(l => l.Name)
                .ToListAsync();

            foreach (var l in leagues)
            {
                l.Standings = l.Standings.OrderBy(s => s.Position).ToList();
                l.Races = l.Races.OrderByDescending(r => r.Date).ToList();
            }

            var upcomingLegs = await _db.RaceWeekendLegs
                .Include(l => l.Weekend)
                .Where(l => l.Date >= DateTime.Today)
                .OrderBy(l => l.Date)
                .ToListAsync();

            ViewBag.UpcomingLegsByLeague = upcomingLegs
                .GroupBy(l => l.LeagueId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return View(new <OWNER_HANDLE>10ViewModel
            {
                TwitchChannel = _appOptions.TwitchChannel,
                Leagues = leagues
            });
        }

        public async Task<IActionResult> Results()
        {
            var leagues = await _db.Leagues
                .Include(l => l.Standings)
                .Include(l => l.Races).ThenInclude(r => r.Finishes)
                .Include(l => l.Races).ThenInclude(r => r.ReserveAssignments)
                .OrderBy(l => l.Name)
                .ToListAsync();

            foreach (var l in leagues)
            {
                l.Standings = l.Standings.OrderBy(s => s.Position).ToList();
                l.Races = l.Races.OrderBy(r => r.Date).ToList();
            }

            return View(new <OWNER_HANDLE>10ViewModel
            {
                TwitchChannel = _appOptions.TwitchChannel,
                Leagues = leagues
            });
        }

        [HttpGet]
        public async Task<IActionResult> Apply()
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                ViewBag.ReturnUrl = Url.Action(nameof(Apply));
                return View("LoginRequired");
            }

            var model = new ApplicationForm
            {
                DiscordName = User.Identity?.Name ?? ""
            };

            var (joinedCommunity, joinedLeague, guildError) = await CheckDiscordGuildMembershipAsync();

            if (guildError == "Discord-Login abgelaufen. Bitte erneut anmelden.")
            {
                await HttpContext.SignOutAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
                return Challenge(
                    new Microsoft.AspNetCore.Authentication.AuthenticationProperties
                    {
                        RedirectUri = Url.Action("LoginCallback", "Account", new { returnUrl = Url.Action(nameof(Apply)) })
                    }, "Discord");
            }

            if (guildError != null)
            {
                ViewBag.DiscordWarning = guildError;
                return View(model);
            }

            model.JoinedCommunityDiscord = joinedCommunity;
            model.JoinedLeagueDiscord = joinedLeague;

            var missing = new List<string>();
            if (!joinedCommunity) missing.Add("Community Discord");
            if (!joinedLeague) missing.Add("Liga Discord");

            if (missing.Count > 0)
            {
                ViewBag.DiscordWarning = $"Bitte trete noch folgenden Servern bei: {string.Join(" und ", missing)}.";
            }

            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("forms")]
        public async Task<IActionResult> Apply(ApplicationForm model)
        {
            model.DiscordName = User.Identity?.Name ?? "Unknown";
            model.DiscordId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            ModelState.Remove(nameof(model.DiscordName));
            ModelState.Remove(nameof(model.DiscordId));
            ModelState.Remove(nameof(model.JoinedCommunityDiscord));
            ModelState.Remove(nameof(model.JoinedLeagueDiscord));

            model.GamingName = model.GamingName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(model.GamingName))
            {
                ModelState.AddModelError(nameof(model.GamingName), "Bitte gib deinen EA Namen an.");
            }

            // Server-seitige Re-Verifikation: Vom Client gemeldete Guild-Flags ignorieren
            // und immer frisch gegen die Discord API prüfen, damit niemand die Bewerbung
            // ohne Mitgliedschaft auf den richtigen Discord-Servern abschicken kann.
            var (joinedCommunity, joinedLeague, guildError) = await CheckDiscordGuildMembershipAsync();
            if (guildError != null)
            {
                ViewBag.DiscordWarning = guildError;
                model.JoinedCommunityDiscord = false;
                model.JoinedLeagueDiscord = false;
                return View(model);
            }

            model.JoinedCommunityDiscord = joinedCommunity;
            model.JoinedLeagueDiscord = joinedLeague;

            if (!joinedCommunity || !joinedLeague)
            {
                var missing = new List<string>();
                if (!joinedCommunity) missing.Add("Community Discord");
                if (!joinedLeague) missing.Add("Liga Discord");
                ModelState.AddModelError(string.Empty,
                    $"Bewerbung nicht möglich. Bitte trete zuerst folgenden Servern bei: {string.Join(" und ", missing)}.");
                ViewBag.DiscordWarning = $"Bitte trete noch folgenden Servern bei: {string.Join(" und ", missing)}.";
                return View(model);
            }

            if (ModelState.IsValid)
            {
                model.SubmittedAt = DateTime.UtcNow;
                _db.ApplicationForms.Add(model);
                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fehler beim Speichern der Bewerbung für {DiscordId}", model.DiscordId);
                    ModelState.AddModelError(string.Empty, "Die Bewerbung konnte leider nicht gespeichert werden. Bitte versuche es in wenigen Minuten erneut.");
                    return View(model);
                }
                TempData["SuccessMessage"] = "Danke! Deine Anmeldung wurde erfolgreich gesendet.";
                await _webhookAuto.FireAsync(WebhookEvents.ApplicationReceived, new()
                {
                    ["DiscordName"] = model.DiscordName,
                    ["DiscordId"]   = model.DiscordId ?? "",
                    ["GamingName"]  = model.GamingName,
                    ["Role"]        = model.Role,
                    ["SubmittedAt"] = model.SubmittedAt.ToString("dd.MM.yyyy HH:mm"),
                });
                return RedirectToAction(nameof(Apply));
            }
            return View(model);
        }

        private async Task<(bool JoinedCommunity, bool JoinedLeague, string? Error)> CheckDiscordGuildMembershipAsync()
        {
            var accessToken = await HttpContext.GetTokenAsync(
                Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme,
                "access_token");

            if (string.IsNullOrEmpty(accessToken))
            {
                return (false, false, "Discord-Login abgelaufen. Bitte erneut anmelden.");
            }

            // IHttpClientFactory verhindert Socket-Exhaustion durch HttpClient-Pooling
            // (sonst neuer Socket pro Request → kann unter Last DNS/TIME_WAIT-Probleme machen).
            var client = _httpClientFactory.CreateClient("DiscordApi");
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v10/users/@me/guilds");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue(_appOptions.UserAgentName, _appOptions.UserAgentVersion));

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(req);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Discord guild lookup network error");
                return (false, false, "Discord-Server konnte nicht erreicht werden. Bitte versuche es später erneut.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return (false, false, "Discord-Login abgelaufen. Bitte erneut anmelden.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return (false, false, "Discord-Server konnte nicht erreicht werden. Bitte versuche es später erneut.");
            }

            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(content);
            bool community = false, league = false;
            foreach (var guild in doc.RootElement.EnumerateArray())
            {
                var id = guild.GetProperty("id").GetString();
                if (id == _discordGuilds.CommunityGuildId) community = true;
                if (id == _discordGuilds.LeagueGuildId) league = true;
            }
            return (community, league, null);
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
        public async Task<IActionResult> LeagueResults(string leagueId)
        {
            if (string.IsNullOrWhiteSpace(leagueId)) return NotFound();

            var league = await _db.Leagues
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

        [HttpGet]
        public async Task<IActionResult> RaceDetail(string leagueId, int raceId)
        {
            if (string.IsNullOrWhiteSpace(leagueId) || raceId <= 0) return NotFound();

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
                    Team = ResolveTeamForRaceDriver(league.Standings, race, f.Driver) ?? "",
                    Points = basePoints,
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
                Team = ResolveTeamForRaceDriver(league.Standings, race, f.Driver) ?? "",
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
        public async Task<IActionResult> EwigeListe()
        {
            var vm = new EwigeListeViewModel();
            vm.Sheets.AddRange(await BuildLiveEwigeSheetsAsync());

            var filePath = GetEwigeWorkbookPath();
            if (!System.IO.File.Exists(filePath))
            {
                var fallbackPath = Path.Combine(_env.ContentRootPath, "ERC Ewige Tabelle.xlsx");
                if (System.IO.File.Exists(fallbackPath))
                {
                    filePath = fallbackPath;
                }
            }

            if (System.IO.File.Exists(filePath))
            {
                using var workbook = new XLWorkbook(filePath);
                foreach (var ws in workbook.Worksheets)
                {
                    var usedRange = ws.RangeUsed();
                    var sheetVm = new EwigeListeSheetViewModel { Name = ws.Name };

                    if (usedRange != null)
                    {
                        var firstRow = usedRange.RangeAddress.FirstAddress.RowNumber;
                        var lastRow = usedRange.RangeAddress.LastAddress.RowNumber;
                        var firstCol = usedRange.RangeAddress.FirstAddress.ColumnNumber;
                        var lastCol = usedRange.RangeAddress.LastAddress.ColumnNumber;

                        for (int r = firstRow; r <= lastRow; r++)
                        {
                            var row = new List<string>();
                            for (int c = firstCol; c <= lastCol; c++)
                            {
                                row.Add(ws.Cell(r, c).GetFormattedString());
                            }
                            sheetVm.Rows.Add(row);
                        }
                    }

                    vm.Sheets.Add(sheetVm);
                }
            }

            AppendGlobalDriverOverviewSheets(vm.Sheets);

            if (vm.Sheets.Count == 0)
            {
                vm.ErrorMessage = "Es wurden weder Live-Daten noch eine Excel-Daten für die Ewige Liste gefunden.";
            }

            return View(vm);
        }

        private string GetEwigeWorkbookPath()
        {
            return Path.Combine(_env.ContentRootPath, "data", "ewige", "active.xlsx");
        }

        private sealed class DriverAggregate
        {
            public string DisplayName { get; set; } = string.Empty;
            public string LastTeam { get; set; } = string.Empty;
            public int TotalPoints { get; set; }
            public int Entries { get; set; }
            public HashSet<string> Seasons { get; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private static void AppendGlobalDriverOverviewSheets(List<EwigeListeSheetViewModel> sheets)
        {
            if (sheets.Count == 0)
            {
                return;
            }

            var aggregateByDriver = new Dictionary<string, DriverAggregate>(StringComparer.OrdinalIgnoreCase);

            foreach (var sheet in sheets)
            {
                if (sheet.Rows.Count == 0)
                {
                    continue;
                }

                var header = sheet.Rows[0];
                var driverCol = FindColumnIndex(header, "Driver", "Fahrer");
                var pointsCol = FindColumnIndex(header, "Points", "Punkte", "Gesamtpunkte");
                var teamCol = FindColumnIndex(header, "Team");

                if (driverCol < 0 || pointsCol < 0)
                {
                    continue;
                }

                for (int r = 1; r < sheet.Rows.Count; r++)
                {
                    var row = sheet.Rows[r];
                    if (driverCol >= row.Count)
                    {
                        continue;
                    }

                    var driverName = (row[driverCol] ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(driverName))
                    {
                        continue;
                    }

                    var points = pointsCol < row.Count ? ParseIntCell(row[pointsCol]) : 0;
                    var team = teamCol >= 0 && teamCol < row.Count
                        ? (row[teamCol] ?? string.Empty).Trim()
                        : string.Empty;

                    if (!aggregateByDriver.TryGetValue(driverName, out var aggregate))
                    {
                        aggregate = new DriverAggregate { DisplayName = driverName };
                        aggregateByDriver[driverName] = aggregate;
                    }

                    aggregate.TotalPoints += points;
                    aggregate.Entries += 1;
                    aggregate.Seasons.Add(sheet.Name);

                    if (!string.IsNullOrWhiteSpace(team))
                    {
                        aggregate.LastTeam = team;
                    }
                }
            }

            if (aggregateByDriver.Count == 0)
            {
                return;
            }

            var values = aggregateByDriver.Values.ToList();

            var uniqueSheet = new EwigeListeSheetViewModel
            {
                Name = "Alle Fahrer (gesamt)",
                Rows = new List<List<string>>
                {
                    new() { "Fahrer", "Letztes Team", "Saisons", "Einträge" }
                }
            };

            foreach (var item in values.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                uniqueSheet.Rows.Add(new List<string>
                {
                    item.DisplayName,
                    item.LastTeam,
                    item.Seasons.Count.ToString(),
                    item.Entries.ToString()
                });
            }

            var pointsSheet = new EwigeListeSheetViewModel
            {
                Name = "Fahrer Gesamtpunkte (alle Seasons)",
                Rows = new List<List<string>>
                {
                    new() { "Position", "Fahrer", "Gesamtpunkte", "Saisons", "Letztes Team" }
                }
            };

            var rank = 1;
            foreach (var item in values
                .OrderByDescending(x => x.TotalPoints)
                .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                pointsSheet.Rows.Add(new List<string>
                {
                    rank.ToString(),
                    item.DisplayName,
                    item.TotalPoints.ToString(),
                    item.Seasons.Count.ToString(),
                    item.LastTeam
                });
                rank++;
            }

            sheets.RemoveAll(s => string.Equals(s.Name, uniqueSheet.Name, StringComparison.OrdinalIgnoreCase)
                               || string.Equals(s.Name, pointsSheet.Name, StringComparison.OrdinalIgnoreCase));

            sheets.Insert(0, pointsSheet);
            sheets.Insert(0, uniqueSheet);
        }

        private static int FindColumnIndex(List<string> header, params string[] names)
        {
            for (int i = 0; i < header.Count; i++)
            {
                var value = (header[i] ?? string.Empty).Trim();
                for (int n = 0; n < names.Length; n++)
                {
                    if (string.Equals(value, names[n], StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static int ParseIntCell(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            var trimmed = value.Trim();
            if (int.TryParse(trimmed, out var direct))
            {
                return direct;
            }

            if (int.TryParse(trimmed, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.GetCultureInfo("de-DE"), out var de))
            {
                return de;
            }

            var onlyNumber = new string(trimmed.Where(c => char.IsDigit(c) || c == '-' || c == '+').ToArray());
            return int.TryParse(onlyNumber, out var cleaned) ? cleaned : 0;
        }

        private async Task<List<EwigeListeSheetViewModel>> BuildLiveEwigeSheetsAsync()
        {
            var result = new List<EwigeListeSheetViewModel>();

            var leagues = await _db.Leagues
                .Include(l => l.Standings)
                .Include(l => l.Races).ThenInclude(r => r.Finishes)
                .Include(l => l.Races).ThenInclude(r => r.ReserveAssignments)
                .OrderBy(l => l.Name)
                .ToListAsync();

            foreach (var league in leagues)
            {
                var standings = league.Standings
                    .Where(s => !string.IsNullOrWhiteSpace(s.Driver))
                    .ToList();

                if (standings.Count == 0) continue;

                var leagueLabel = league.IsArchived
                    ? (string.IsNullOrWhiteSpace(league.ArchivedName) ? league.Name : league.ArchivedName!)
                    : $"{league.Name} (Aktuell)";

                var orderedRaces = league.Races
                    .OrderBy(r => r.Date)
                    .ThenBy(r => r.RowId)
                    .ToList();

                var raceColumns = orderedRaces
                    .Select(r => new
                    {
                        r.RowId,
                        Abbr = TrackAbbrForEwige(r.Track)
                    })
                    .ToList();

                var driverRows = standings
                    .Select(s =>
                    {
                        var driverName = s.Driver.Trim();
                        var reserveFor = s.IsReserveDriver && !string.IsNullOrWhiteSpace(s.ReserveForDriver)
                            ? s.ReserveForDriver.Trim()
                            : "";

                        var reserveForTeam = !string.IsNullOrWhiteSpace(reserveFor)
                            ? standings.FirstOrDefault(x =>
                                !string.IsNullOrWhiteSpace(x.Driver) &&
                                x.Driver.Trim().Equals(reserveFor, StringComparison.OrdinalIgnoreCase))?.Team
                            : null;

                        var displayTeam = s.IsReserveDriver
                            ? ""
                            : (s.Team ?? "").Trim();

                        var finishesByRace = orderedRaces
                            .Select(r => r.Finishes.FirstOrDefault(f =>
                                !string.IsNullOrWhiteSpace(f.Driver) &&
                                f.Driver.Trim().Equals(driverName, StringComparison.OrdinalIgnoreCase)))
                            .ToList();

                        var p1 = finishesByRace.Count(f => f?.Position == 1);
                        var p2 = finishesByRace.Count(f => f?.Position == 2);
                        var p3 = finishesByRace.Count(f => f?.Position == 3);

                        var raceValues = finishesByRace
                            .Select(f =>
                            {
                                if (f is null) return "DNS";
                                if (f.Position <= 0) return "DNF";
                                return f.Position.ToString();
                            })
                            .ToList();

                        return new
                        {
                            Driver = s.Driver,
                            Team = displayTeam,
                            s.Points,
                            Wins = s.Wins,
                            Podiums = p1 + p2 + p3,
                            RaceValues = raceValues,
                            ReserveFor = reserveFor
                        };
                    })
                    .OrderByDescending(x => x.Points)
                    .ThenByDescending(x => x.Wins)
                    .ThenBy(x => x.Driver)
                    .ToList();

                var driverHeader = new List<string> { "Driver", "Team", "Reserve For" };
                driverHeader.AddRange(raceColumns.Select(x => x.Abbr));
                driverHeader.AddRange(new[] { "Points", "Podiums", "Wins" });

                var driverSheet = new EwigeListeSheetViewModel
                {
                    Name = $"{leagueLabel} - Fahrer",
                    Rows = new List<List<string>> { driverHeader }
                };

                foreach (var row in driverRows)
                {
                    var cells = new List<string>
                    {
                        row.Driver,
                        row.Team,
                        row.ReserveFor
                    };

                    cells.AddRange(row.RaceValues);
                    cells.Add(row.Points.ToString());
                    cells.Add(row.Podiums.ToString());
                    cells.Add(row.Wins.ToString());
                    driverSheet.Rows.Add(cells);
                }

                result.Add(driverSheet);

                var teamNames = standings
                    .Select(s => string.IsNullOrWhiteSpace(s.Team) ? "Ohne Team" : s.Team.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToList();

                var teamRows = teamNames
                    .Select(teamName =>
                    {
                        var raceValues = orderedRaces
                            .Select(r =>
                            {
                                var finishes = r.Finishes
                                    .Where(f =>
                                    {
                                        var resolvedTeam = ResolveTeamForRaceDriver(standings, r, f.Driver);
                                        return !string.IsNullOrWhiteSpace(resolvedTeam)
                                            && resolvedTeam.Equals(teamName, StringComparison.OrdinalIgnoreCase);
                                    })
                                    .ToList();

                                if (finishes.Count == 0) return "DNS";

                                var bestPos = finishes
                                    .Where(f => f.Position > 0)
                                    .Select(f => f.Position)
                                    .DefaultIfEmpty(0)
                                    .Min();

                                if (bestPos <= 0) return "DNF";
                                return bestPos.ToString();
                            })
                            .ToList();

                        var allFinishes = orderedRaces
                            .SelectMany(r => r.Finishes.Select(f => new { Finish = f, Race = r }))
                            .Where(x =>
                            {
                                var resolvedTeam = ResolveTeamForRaceDriver(standings, x.Race, x.Finish.Driver);
                                return !string.IsNullOrWhiteSpace(resolvedTeam)
                                    && resolvedTeam.Equals(teamName, StringComparison.OrdinalIgnoreCase);
                            })
                            .Select(x => x.Finish)
                            .ToList();

                        var p1 = allFinishes.Count(f => f.Position == 1);
                        var p2 = allFinishes.Count(f => f.Position == 2);
                        var p3 = allFinishes.Count(f => f.Position == 3);

                        return new
                        {
                            Team = teamName,
                            Points = ComputeTeamPointsForLeague(league, teamName) ?? 0,
                            Wins = p1,
                            Podiums = p1 + p2 + p3,
                            RaceValues = raceValues
                        };
                    })
                    .OrderByDescending(x => x.Points)
                    .ThenByDescending(x => x.Wins)
                    .ThenBy(x => x.Team)
                    .ToList();

                var teamHeader = new List<string> { "Team" };
                teamHeader.AddRange(raceColumns.Select(x => x.Abbr));
                teamHeader.AddRange(new[] { "Points", "Podiums", "Wins" });

                var teamSheet = new EwigeListeSheetViewModel
                {
                    Name = $"{leagueLabel} - Teams",
                    Rows = new List<List<string>> { teamHeader }
                };

                foreach (var row in teamRows)
                {
                    var cells = new List<string> { row.Team };
                    cells.AddRange(row.RaceValues);
                    cells.Add(row.Points.ToString());
                    cells.Add(row.Podiums.ToString());
                    cells.Add(row.Wins.ToString());
                    teamSheet.Rows.Add(cells);
                }

                result.Add(teamSheet);
            }

            return result;
        }

        private async Task<StreamSchedule?> GetNextStreamScheduleAsync()
        {
            var schedules = await _db.StreamSchedules.ToListAsync();
            if (schedules.Count == 0) return null;

            // UtcNow konsistent zum Save-Pfad, der recurring StartAt mit UTC berechnet.
            var now = DateTime.UtcNow;

            return schedules
                .Select(x =>
                {
                    var nextStart = x.IsRecurring && x.DayOfWeek.HasValue && x.TimeOfDay.HasValue
                        ? ComputeNextOccurrence(x.DayOfWeek.Value, x.TimeOfDay.Value, now)
                        : x.StartAt;

                    return new StreamSchedule
                    {
                        Id = x.Id,
                        Title = x.Title,
                        Url = x.Url,
                        DurationMinutes = x.DurationMinutes,
                        IsRecurring = x.IsRecurring,
                        DayOfWeek = x.DayOfWeek,
                        TimeOfDay = x.TimeOfDay,
                        StartAt = nextStart,
                        CreatedAt = x.CreatedAt
                    };
                })
                .Where(x => x.StartAt >= now)
                .OrderBy(x => x.StartAt)
                .FirstOrDefault();
        }

        private static DateTime ComputeNextOccurrence(int dayOfWeek, TimeSpan timeOfDay, DateTime from)
        {
            var daysUntil = ((dayOfWeek - (int)from.DayOfWeek) + 7) % 7;
            var candidate = from.Date.AddDays(daysUntil).Add(timeOfDay);
            if (candidate < from)
            {
                candidate = candidate.AddDays(7);
            }

            return candidate;
        }

        private static bool HasDrivenForMultipleTeams(League league, string driverName)
        {
            if (string.IsNullOrWhiteSpace(driverName)) return false;

            var normalizedDriver = driverName.Trim();

            var teams = league.Races
                .OrderBy(r => r.Date)
                .ThenBy(r => r.RowId)
                .Where(r => r.Finishes.Any(f => !string.IsNullOrWhiteSpace(f.Driver)
                                                && f.Driver.Trim().Equals(normalizedDriver, StringComparison.OrdinalIgnoreCase)))
                .Select(r => ResolveTeamForRaceDriver(league.Standings, r, normalizedDriver))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return teams.Count > 1;
        }

        private static int? ComputeTeamPointsForLeague(League league, string? teamName)
        {
            if (string.IsNullOrWhiteSpace(teamName)) return null;

            int[] pointMap = { 25, 21, 18, 16, 14, 12, 10, 8, 7, 6, 5, 4, 3, 2, 1, 0, 0, 0, 0, 0 };
            var normalizedTeam = teamName.Trim();
            var total = 0;

            foreach (var race in league.Races.OrderBy(r => r.Date).ThenBy(r => r.RowId))
            {
                foreach (var finish in race.Finishes.Where(f => f.Position > 0))
                {
                    var resolvedTeam = ResolveTeamForRaceDriver(league.Standings, race, finish.Driver);
                    if (string.IsNullOrWhiteSpace(resolvedTeam) || !resolvedTeam.Equals(normalizedTeam, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var idx = finish.Position - 1;
                    if (idx >= 0 && idx < pointMap.Length)
                    {
                        total += pointMap[idx];
                    }
                }
            }

            return total;
        }

        private static string? ResolveTeamForRaceDriver(IEnumerable<DriverStanding> standings, RaceResult race, string? driverName)
        {
            if (string.IsNullOrWhiteSpace(driverName)) return null;
            var normalizedDriver = driverName.Trim();

            var raceMainDriver = race.ReserveAssignments
                .FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.ReserveDriver)
                                     && a.ReserveDriver.Trim().Equals(normalizedDriver, StringComparison.OrdinalIgnoreCase))?.MainDriver;

            if (!string.IsNullOrWhiteSpace(raceMainDriver))
            {
                var mainTeam = standings.FirstOrDefault(s =>
                    !string.IsNullOrWhiteSpace(s.Driver) &&
                    s.Driver.Trim().Equals(raceMainDriver.Trim(), StringComparison.OrdinalIgnoreCase))?.Team;

                if (!string.IsNullOrWhiteSpace(mainTeam))
                {
                    return mainTeam.Trim();
                }
            }

            var standing = standings.FirstOrDefault(s =>
                !string.IsNullOrWhiteSpace(s.Driver) &&
                s.Driver.Trim().Equals(normalizedDriver, StringComparison.OrdinalIgnoreCase));

            if (standing is null) return null;

            if (!string.IsNullOrWhiteSpace(standing.Team))
            {
                return standing.Team.Trim();
            }

            if (standing.IsReserveDriver && !string.IsNullOrWhiteSpace(standing.ReserveForDriver))
            {
                var fallbackTeam = standings.FirstOrDefault(s =>
                    !string.IsNullOrWhiteSpace(s.Driver) &&
                    s.Driver.Trim().Equals(standing.ReserveForDriver.Trim(), StringComparison.OrdinalIgnoreCase))?.Team;

                return string.IsNullOrWhiteSpace(fallbackTeam) ? null : fallbackTeam.Trim();
            }

            return null;
        }

        private static string TrackAbbrForEwige(string? track)
        {
            if (string.IsNullOrWhiteSpace(track)) return "RND";

            var t = track.ToLowerInvariant();
            if (t.Contains("australi")) return "AUS";
            if (t.Contains("imola") || t.Contains("emilia")) return "IMO";
            if (t.Contains("silverstone") || t.Contains("britain") || t.Contains("großbritannien")) return "GBR";
            if (t.Contains("spa") || t.Contains("belg")) return "SPA";
            if (t.Contains("hungary") || t.Contains("ungarn")) return "HUN";
            if (t.Contains("japan") || t.Contains("suzuka")) return "JAP";
            if (t.Contains("qatar") || t.Contains("katar") || t.Contains("losail")) return "QAT";
            if (t.Contains("saudi") || t.Contains("jeddah") || t.Contains("ksa")) return "SAU";
            if (t.Contains("mexico") || t.Contains("mexiko")) return "MEX";
            if (t.Contains("brazil") || t.Contains("brasili") || t.Contains("interlagos")) return "BRA";

            var letters = new string(track.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            return letters.Length >= 3 ? letters[..3] : letters.PadRight(3, 'X');
        }

        private static string? BuildYouTubeEmbedUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return null;
            }

            if (uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
            {
                var videoId = uri.AbsolutePath.Trim('/');
                return string.IsNullOrWhiteSpace(videoId) ? null : $"https://www.youtube.com/embed/{videoId}";
            }

            if (uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
            {
                if (uri.AbsolutePath.StartsWith("/embed/", StringComparison.OrdinalIgnoreCase))
                {
                    return url;
                }

                var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
                if (query.TryGetValue("v", out var videoId) && !string.IsNullOrWhiteSpace(videoId))
                {
                    return $"https://www.youtube.com/embed/{videoId.ToString()}";
                }
            }

            return null;
        }

        [HttpGet]
        public async Task<IActionResult> AllRaces()
        {
            var leagues = await _db.Leagues
                .Include(l => l.Races).ThenInclude(r => r.Finishes)
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

                                return new AllRaceItem
                                {
                                    RaceId = r.RowId,
                                    Date = r.Date,
                                    Track = r.Track,
                                    Winner = r.Winner,
                                    WinnerRaceTimeMs = podium.FirstOrDefault()?.RaceTimeMs,
                                    P2 = podium.Count > 1 ? podium[1].Driver : null,
                                    P3 = podium.Count > 2 ? podium[2].Driver : null
                                };
                            })
                            .ToList()
                    })
                    .Where(x => x.Races.Count > 0)
                    .ToList()
            };

            return View(vm);
        }

        /// <summary>
        /// <summary>
        /// Sandbox-Modus: Liefert ein Setup-Objekt als JSON zurück, damit der Client
        /// lokal (im Browser/Session) damit experimentieren kann – ohne DB-Persistenz.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SetupSandbox(int id)
        {
            var setup = await _db.TrackSetups.FindAsync(id);
            if (setup == null) return NotFound();

            // Tier aus Claims (gleiche Logik wie TrackSetups-Action)
            var sbTierClaim = User.FindFirst("erdi:setup-tier")?.Value;
            var sbRoleClaim = User.FindFirst("erdi:setup-role")?.Value;
            var sbTier = int.TryParse(sbTierClaim, out var parsedSbTier) ? parsedSbTier : 0;
            if (!_trackSetupAccessPolicy.CanView(setup, sbTier, sbRoleClaim))
                return Forbid();

            return View("~/Views/Home/SetupSandbox.cshtml", setup);
        }

        [HttpGet]
        public async Task<IActionResult> TrackSetups(string? track = null)
        {
            var normalizedTrack = track?.Trim();
            var selectedTrack = string.IsNullOrWhiteSpace(normalizedTrack)
                ? null
                : normalizedTrack;

            var tracks = await _db.TrackSetups
                .Select(x => x.Track)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            var setupTierClaim = User.FindFirst("erdi:setup-tier")?.Value;
            var setupRoleClaim = User.FindFirst("erdi:setup-role")?.Value;
            var communityGuildClaim = User.FindFirst("erdi:on-community-guild")?.Value;
            var joinedAtClaim = User.FindFirst("erdi:guild-joined-at")?.Value;
            var tenurePendingClaim = User.FindFirst("erdi:tenure-pending")?.Value;
            var currentTier = int.TryParse(setupTierClaim, out var parsedTier) ? parsedTier : 0;
            var isOnCommunityGuild = string.Equals(communityGuildClaim, "true", StringComparison.OrdinalIgnoreCase);
            var isTenurePending = string.Equals(tenurePendingClaim, "true", StringComparison.OrdinalIgnoreCase);
            DateTimeOffset? guildJoinedAt = DateTimeOffset.TryParse(joinedAtClaim, out var parsedJoinedAt) ? parsedJoinedAt : null;

            var query = _db.TrackSetups.AsQueryable();
            if (!string.IsNullOrWhiteSpace(selectedTrack))
            {
                query = query.Where(x => x.Track == selectedTrack);
            }

            var setups = await query
                .OrderBy(x => x.Track)
                .ThenByDescending(x => x.RequiredAccessTier)
                .ThenByDescending(x => x.UpdatedAt)
                .ToListAsync();

            var visibleSetups = setups.Where(x => _trackSetupAccessPolicy.CanView(x, currentTier, setupRoleClaim)).ToList();
            var visibleSetupIds = visibleSetups.Select(x => x.Id).ToList();
            var comments = await _db.SetupComments
                .Where(x => visibleSetupIds.Contains(x.TrackSetupId))
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
            var likes = await _db.SetupLikes
                .Where(x => visibleSetupIds.Contains(x.TrackSetupId))
                .ToListAsync();
            var currentDiscordId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

            ViewBag.SetupTracks = tracks;
            ViewBag.SelectedTrack = selectedTrack;
            ViewBag.SetupTier = currentTier;
            ViewBag.SetupRole = setupRoleClaim;
            ViewBag.IsOnCommunityGuild = isOnCommunityGuild;
            ViewBag.IsTenurePending = isTenurePending;
            ViewBag.GuildJoinedAt = guildJoinedAt;
            ViewBag.TenureRequiredDays = _setupAccessOptions.MinGuildTenureDays;
            ViewBag.VisibleSetups = visibleSetups;
            ViewBag.HiddenSetups = setups.Where(x => !_trackSetupAccessPolicy.CanView(x, currentTier, setupRoleClaim)).ToList();
            ViewBag.SetupCategoryNames = SetupGameSpec.GetCategoryDisplayNames();
            ViewBag.SetupFieldNames = SetupGameSpec.GetFieldDisplayNames();
            ViewBag.SetupEditorConfig = SetupGameSpec.GetEditorConfig();
            ViewBag.SetupMetricConfig = SetupGameSpec.GetMetricConfig();
            ViewBag.SetupComments = comments
                .GroupBy(x => x.TrackSetupId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAt).ToList());
            ViewBag.SetupLikeCounts = likes
                .GroupBy(x => x.TrackSetupId)
                .ToDictionary(g => g.Key, g => g.Count());
            ViewBag.LikedSetupIds = likes
                .Where(x => x.DiscordId == currentDiscordId)
                .Select(x => x.TrackSetupId)
                .ToHashSet();

            return View();
        }

        [HttpGet]
        public IActionResult DriverDetail(string leagueId, string driver)
        {
            if (string.IsNullOrWhiteSpace(driver)) return NotFound();

            return RedirectToAction("ByDriverName", "Profile", new { driverName = driver.Trim() });
        }

        [HttpGet]
        public async Task<IActionResult> CommunityNews()
        {
            var vm = new CommunityNewsViewModel
            {
                Posts = await _db.CommunityNewsPosts
                    .Where(x => x.IsPublished)
                    .OrderByDescending(x => x.IsPinned)
                    .ThenByDescending(x => x.PublishedAt)
                    .ToListAsync()
            };
            return View(vm);
        }

        /// <summary>
        /// Öffentliche, screenshot-fähige Saison-Kalender-Übersicht über alle Ligen.
        /// Wird im Admin-Bereich konfiguriert (Hintergrundbild, Saison-Titel).
        /// </summary>
        [HttpGet]
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

            ViewBag.CalendarSettings = settings;
            ViewBag.Leagues = leagues;
            ViewBag.Weekends = weekends;
            return View();
        }

        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSetupComment(int setupId, string message, string? track = null)
        {
            var discordId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var discordName = User.Identity?.Name ?? "Community User";
            if (string.IsNullOrWhiteSpace(discordId)) return Forbid();

            var normalized = message?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                _db.SetupComments.Add(new SetupComment
                {
                    TrackSetupId = setupId,
                    AuthorDiscordId = discordId,
                    AuthorName = discordName,
                    Message = normalized.Length > 600 ? normalized[..600] : normalized,
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(TrackSetups), new { track });
        }

        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSetupLike(int setupId, string? track = null)
        {
            var discordId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var discordName = User.Identity?.Name ?? "Community User";
            if (string.IsNullOrWhiteSpace(discordId)) return Forbid();

            var existing = await _db.SetupLikes.FirstOrDefaultAsync(x => x.TrackSetupId == setupId && x.DiscordId == discordId);
            if (existing is null)
            {
                _db.SetupLikes.Add(new SetupLike
                {
                    TrackSetupId = setupId,
                    DiscordId = discordId,
                    DiscordName = discordName,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                _db.SetupLikes.Remove(existing);
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(TrackSetups), new { track });
        }

        [HttpGet]
        public async Task<IActionResult> Teams()
        {
            var standings = await _db.DriverStandings
                .Where(x => !string.IsNullOrWhiteSpace(x.Team))
                .ToListAsync();

            var races = await _db.RaceResults
                .Include(x => x.Finishes)
                .ToListAsync();

            var vm = new TeamListPageViewModel
            {
                Teams = standings
                    .GroupBy(x => x.Team.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g =>
                    {
                        var teamInfo = F1TeamsHelper.GetTeamByName(g.Key);
                        var drivers = g.Where(x => !string.IsNullOrWhiteSpace(x.Driver))
                            .Select(x => x.Driver.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
                        var podiums = races.Sum(r => r.Finishes.Count(f => f.Position is >= 1 and <= 3 && drivers.Contains(f.Driver.Trim())));
                        return new TeamSummaryViewModel
                        {
                            TeamName = g.Key,
                            CssKey = teamInfo?.CssKey,
                            PrimaryColor = teamInfo?.PrimaryColor ?? "***REMOVED***e10600",
                            SecondaryColor = teamInfo?.SecondaryColor ?? "***REMOVED***ffffff",
                            Drivers = drivers.Count,
                            Points = g.Sum(x => x.Points),
                            Wins = g.Sum(x => x.Wins),
                            Podiums = podiums
                        };
                    })
                    .OrderByDescending(x => x.Points)
                    .ThenBy(x => x.TeamName)
                    .ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Team(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return NotFound();
            var normalized = name.Trim();

            var standings = await _db.DriverStandings
                .Where(x => x.Team == normalized)
                .OrderByDescending(x => x.Points)
                .ThenByDescending(x => x.Wins)
                .ToListAsync();
            if (standings.Count == 0) return NotFound();

            var profiles = await _db.DriverProfiles
                .Include(x => x.GamerTags)
                .ToListAsync();
            var races = await _db.RaceResults
                .Include(x => x.Finishes)
                .Where(x => x.Finishes.Any())
                .OrderByDescending(x => x.Date)
                .Take(8)
                .ToListAsync();

            var teamInfo = F1TeamsHelper.GetTeamByName(normalized);
            var driverNames = standings.Select(x => x.Driver.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var vm = new TeamDetailPageViewModel
            {
                TeamName = normalized,
                CssKey = teamInfo?.CssKey,
                PrimaryColor = teamInfo?.PrimaryColor ?? "***REMOVED***e10600",
                SecondaryColor = teamInfo?.SecondaryColor ?? "***REMOVED***ffffff",
                TotalPoints = standings.Sum(x => x.Points),
                TotalWins = standings.Sum(x => x.Wins),
                TotalPodiums = races.Sum(r => r.Finishes.Count(f => f.Position is >= 1 and <= 3 && driverNames.Contains(f.Driver.Trim()))),
                Drivers = standings.Select(s => new TeamDriverCardViewModel
                {
                    Driver = s.Driver,
                    DiscordId = profiles.FirstOrDefault(p => string.Equals(p.DisplayName, s.Driver, StringComparison.OrdinalIgnoreCase)
                        || p.GamerTags.Any(t => string.Equals(t.GamerTag, s.Driver, StringComparison.OrdinalIgnoreCase)))?.DiscordId,
                    Points = s.Points,
                    Wins = s.Wins,
                    IsReserveDriver = s.IsReserveDriver,
                    ReserveForDriver = s.ReserveForDriver
                }).ToList(),
                RecentRaces = races.Select(r => new TeamRaceCardViewModel
                {
                    Date = r.Date,
                    LeagueId = r.LeagueId,
                    Track = r.Track,
                    Winner = r.Winner,
                    TeamFinishesInTop10 = r.Finishes.Count(f => f.Position is >= 1 and <= 10 && driverNames.Contains(f.Driver.Trim()))
                }).ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> HallOfFame()
        {
            var standings = await _db.DriverStandings.ToListAsync();
            var races = await _db.RaceResults
                .Include(x => x.Finishes)
                .Include(x => x.ReserveAssignments)
                .ToListAsync();

            var reserveWins = races
                .SelectMany(r => r.Finishes.Where(f => f.Position == 1).Select(f => new { Finish = f, Race = r }))
                .Where(x => x.Race.ReserveAssignments.Any(a => string.Equals(a.ReserveDriver, x.Finish.Driver, StringComparison.OrdinalIgnoreCase)))
                .GroupBy(x => x.Finish.Driver, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { Driver = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .FirstOrDefault();

            var vm = new HallOfFamePageViewModel
            {
                Records = new List<HallOfFameRecordViewModel>()
                {
                    new()
                    {
                        Title = "Meiste Siege",
                        Driver = standings.OrderByDescending(x => x.Wins).ThenBy(x => x.Driver).FirstOrDefault()?.Driver ?? "-",
                        Value = standings.OrderByDescending(x => x.Wins).FirstOrDefault()?.Wins.ToString() ?? "0",
                        Subtitle = "Über alle aktiven Standings hinweg"
                    },
                    new()
                    {
                        Title = "Meiste Punkte",
                        Driver = standings.OrderByDescending(x => x.Points).ThenBy(x => x.Driver).FirstOrDefault()?.Driver ?? "-",
                        Value = standings.OrderByDescending(x => x.Points).FirstOrDefault()?.Points.ToString() ?? "0",
                        Subtitle = "Gesamtausbeute in der ERC"
                    },
                    new()
                    {
                        Title = "Beste Reservefahrer",
                        Driver = reserveWins?.Driver ?? "-",
                        Value = reserveWins?.Count.ToString() ?? "0",
                        Subtitle = "Siege als Reservefahrer"
                    },
                    new()
                    {
                        Title = "Meiste Podien",
                        Driver = races.SelectMany(r => r.Finishes)
                            .Where(f => f.Position is >= 1 and <= 3)
                            .GroupBy(f => f.Driver, StringComparer.OrdinalIgnoreCase)
                            .OrderByDescending(g => g.Count())
                            .ThenBy(g => g.Key)
                            .Select(g => g.Key)
                            .FirstOrDefault() ?? "-",
                        Value = races.SelectMany(r => r.Finishes)
                            .Count(f => f.Position is >= 1 and <= 3 && string.Equals(f.Driver,
                                races.SelectMany(rr => rr.Finishes)
                                    .Where(ff => ff.Position is >= 1 and <= 3)
                                    .GroupBy(ff => ff.Driver, StringComparer.OrdinalIgnoreCase)
                                    .OrderByDescending(g => g.Count())
                                    .ThenBy(g => g.Key)
                                    .Select(g => g.Key)
                                    .FirstOrDefault(), StringComparison.OrdinalIgnoreCase)).ToString(),
                        Subtitle = "Podestplätze insgesamt"
                    }
                }
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ReserveExchange(string? leagueId = null, string? platform = null, string? pace = null)
        {
            var profiles = await _db.DriverProfiles
                .Include(x => x.GamerTags)
                .ToListAsync();
            var standings = await _db.DriverStandings.ToListAsync();

            var entries = standings
                .Where(x => x.IsReserveDriver || !string.IsNullOrWhiteSpace(x.ReserveForDriver))
                .Select(s =>
                {
                    var profile = profiles.FirstOrDefault(p => string.Equals(p.DisplayName, s.Driver, StringComparison.OrdinalIgnoreCase)
                        || p.GamerTags.Any(t => string.Equals(t.GamerTag, s.Driver, StringComparison.OrdinalIgnoreCase)));
                    var profilePlatform = profile?.PreferredPlatform
                        ?? profile?.GamerTags.FirstOrDefault(t => t.IsPrimary)?.Platform
                        ?? profile?.GamerTags.FirstOrDefault()?.Platform
                        ?? "Unbekannt";
                    var paceBucket = s.Points >= 100 ? "Schnell" : s.Points >= 40 ? "Mittel" : "Entwicklung";
                    return new ReserveExchangeEntryViewModel
                    {
                        Driver = s.Driver,
                        LeagueId = s.LeagueId,
                        Team = s.Team,
                        ReserveForDriver = s.ReserveForDriver,
                        Platform = profilePlatform,
                        InputDevice = profile?.InputDevice ?? "Unbekannt",
                        FavoriteTrack = profile?.FavoriteTrack ?? "-",
                        Bio = profile?.Bio ?? string.Empty,
                        Points = s.Points,
                        Wins = s.Wins,
                        PaceBucket = paceBucket,
                        ProfileDiscordId = profile?.DiscordId
                    };
                })
                .ToList();

            if (!string.IsNullOrWhiteSpace(leagueId)) entries = entries.Where(x => x.LeagueId == leagueId).ToList();
            if (!string.IsNullOrWhiteSpace(platform)) entries = entries.Where(x => x.Platform == platform).ToList();
            if (!string.IsNullOrWhiteSpace(pace)) entries = entries.Where(x => x.PaceBucket == pace).ToList();

            var vm = new ReserveExchangePageViewModel
            {
                SelectedLeagueId = leagueId,
                SelectedPlatform = platform,
                SelectedPace = pace,
                LeagueIds = standings.Select(x => x.LeagueId).Distinct().OrderBy(x => x).ToList(),
                Platforms = profiles.Select(x => x.PreferredPlatform)
                    .Concat(profiles.SelectMany(x => x.GamerTags.Select(t => t.Platform)))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToList()!,
                Entries = entries.OrderByDescending(x => x.Points).ThenBy(x => x.Driver).ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> CommunityVotes()
        {
            var polls = await _db.CommunityVotePolls
                .Include(x => x.Options)
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
            var pollIds = polls.Select(x => x.Id).ToList();
            var responses = await _db.CommunityVoteResponses
                .Where(x => pollIds.Contains(x.PollId))
                .ToListAsync();
            var currentDiscordId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

            var vm = new CommunityVotesPageViewModel
            {
                Polls = polls.Select(p => new CommunityVotePollCardViewModel
                {
                    Poll = p,
                    VoteCounts = responses.Where(x => x.PollId == p.Id)
                        .GroupBy(x => x.OptionId)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    TotalVotes = responses.Count(x => x.PollId == p.Id),
                    CurrentOptionId = responses.FirstOrDefault(x => x.PollId == p.Id && x.DiscordId == currentDiscordId)?.OptionId
                }).ToList()
            };

            return View(vm);
        }

        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("forms")]
        public async Task<IActionResult> VoteCommunityPoll(int pollId, int optionId)
        {
            var discordId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var discordName = User.Identity?.Name ?? "Community User";
            if (string.IsNullOrWhiteSpace(discordId)) return Forbid();

            var existing = await _db.CommunityVoteResponses.AsTracking()
                .FirstOrDefaultAsync(x => x.PollId == pollId && x.DiscordId == discordId);
            if (existing is null)
            {
                _db.CommunityVoteResponses.Add(new CommunityVoteResponse
                {
                    PollId = pollId,
                    OptionId = optionId,
                    DiscordId = discordId,
                    DiscordName = discordName,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.OptionId = optionId;
                existing.DiscordName = discordName;
                existing.CreatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(CommunityVotes));
        }

        [HttpGet]
        public async Task<IActionResult> Highlights()
        {
            var vm = new HighlightsPageViewModel
            {
                Highlights = await _db.RaceHighlightClips
                    .Where(x => x.IsApproved)
                    .OrderByDescending(x => x.CreatedAt)
                    .ToListAsync()
            };
            return View(vm);
        }

        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("forms")]
        public async Task<IActionResult> SubmitHighlight(string title, string url, string? category, string? raceLabel)
        {
            var discordId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var discordName = User.Identity?.Name ?? "Community User";
            if (string.IsNullOrWhiteSpace(discordId)) return Forbid();

            var normalizedTitle = title?.Trim() ?? string.Empty;
            var normalizedUrl = url?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalizedTitle) && !string.IsNullOrWhiteSpace(normalizedUrl))
            {
                _db.RaceHighlightClips.Add(new RaceHighlightClip
                {
                    Title = normalizedTitle.Length > 160 ? normalizedTitle[..160] : normalizedTitle,
                    Url = normalizedUrl.Length > 512 ? normalizedUrl[..512] : normalizedUrl,
                    Category = string.IsNullOrWhiteSpace(category) ? "Highlight" : category.Trim()[..Math.Min(category.Trim().Length, 96)],
                    RaceLabel = string.IsNullOrWhiteSpace(raceLabel) ? null : raceLabel.Trim()[..Math.Min(raceLabel.Trim().Length, 160)],
                    SubmittedByDiscordId = discordId,
                    SubmittedByName = discordName,
                    IsApproved = true,
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Highlights));
        }

        [HttpGet]
        public async Task<IActionResult> DriverLevels()
        {
            var profiles = await _db.DriverProfiles
                .Include(x => x.GamerTags)
                .ToListAsync();
            var races = await _db.RaceResults
                .Include(x => x.Finishes)
                .ToListAsync();
            var setupComments = await _db.SetupComments.ToListAsync();
            var setupLikes = await _db.SetupLikes.ToListAsync();
            var wallMessages = await _db.ProfileWallMessages.ToListAsync();

            var entries = profiles.Select(profile =>
            {
                var names = profile.GamerTags.Select(t => t.GamerTag.Trim())
                    .Append(profile.DisplayName?.Trim() ?? string.Empty)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var profileRaces = races.SelectMany(r => r.Finishes)
                    .Where(f => names.Contains(f.Driver.Trim()))
                    .ToList();
                var raceCount = profileRaces.Count;
                var wins = profileRaces.Count(x => x.Position == 1);
                var podiums = profileRaces.Count(x => x.Position is >= 1 and <= 3);
                var communityScore = setupComments.Count(x => x.AuthorDiscordId == profile.DiscordId)
                    + setupLikes.Count(x => x.DiscordId == profile.DiscordId)
                    + wallMessages.Count(x => x.AuthorDiscordId == profile.DiscordId);
                var xp = (raceCount * 20) + (wins * 25) + (podiums * 10) + (communityScore * 5);
                var level = Math.Max(1, (xp / 100) + 1);
                var nextLevelThreshold = level * 100;
                return new DriverLevelEntryViewModel
                {
                    Driver = profile.DisplayName ?? profile.DiscordName,
                    DiscordId = profile.DiscordId,
                    Level = level,
                    Xp = xp,
                    XpToNext = Math.Max(0, nextLevelThreshold - xp),
                    Races = raceCount,
                    Wins = wins,
                    Podiums = podiums,
                    CommunityScore = communityScore
                };
            })
            .OrderByDescending(x => x.Level)
            .ThenByDescending(x => x.Xp)
            .ThenBy(x => x.Driver)
            .ToList();

            return View(new DriverLevelsPageViewModel { Entries = entries });
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
