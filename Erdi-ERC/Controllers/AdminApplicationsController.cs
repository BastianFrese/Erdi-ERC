using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using <OWNER_HANDLE>_ERC.Data;
using System.Security.Claims;

namespace <OWNER_HANDLE>_ERC.Controllers
{
    /// <summary>
    /// Admin-seitige Bewerbungs-Routen: Liste mit Filter, Detail, Accept/Reject,
    /// Warteliste pro Liga und Promotion von Wartelisten-Einträgen.
    /// </summary>
    [Authorize(Policy = "Admin.Applications.View")]
    public class AdminApplicationsController : Controller
    {
        private readonly IApplicationService _applications;
        private readonly IStaticDataCache _staticCache;
        private readonly AppDbContext _db;
        private readonly <OWNER_HANDLE>_ERC.Options.DriverMatchingOptions _driverMatching;

        public AdminApplicationsController(
            IApplicationService applications,
            IStaticDataCache staticCache,
            AppDbContext db,
            Microsoft.Extensions.Options.IOptions<<OWNER_HANDLE>_ERC.Options.DriverMatchingOptions> driverMatching)
        {
            _applications = applications;
            _staticCache = staticCache;
            _db = db;
            _driverMatching = driverMatching.Value;
        }

        // ── List ─────────────────────────────────────────────────────────────────

        [HttpGet("/AdminApplications/List")]
        public async Task<IActionResult> List(string? status, string? leagueId, string? season, int page = 1)
        {
            var allLeagues = await _staticCache.GetAllLeaguesAsync();
            ViewBag.Leagues = allLeagues;
            ViewBag.StatusFilter = status;
            ViewBag.LeagueFilter = leagueId;
            ViewBag.SeasonFilter = season;
            ViewBag.Page = page;

            ApplicationStatus? statusFilter = null;
            if (!string.IsNullOrWhiteSpace(status)
                && Enum.TryParse<ApplicationStatus>(status, ignoreCase: true, out var parsed))
            {
                statusFilter = parsed;
            }

            const int PageSize = 25;
            var skip = (Math.Max(1, page) - 1) * PageSize;
            var items = await _applications.ListAsync(statusFilter, leagueId, season, skip, PageSize, HttpContext.RequestAborted);
            ViewBag.Items = items;
            ViewBag.HasMore = items.Count == PageSize;

            return View("~/Views/Admin/Applications/List.cshtml");
        }

        // ── Detail ───────────────────────────────────────────────────────────────

        [HttpGet("/AdminApplications/Detail/{id}")]
        public async Task<IActionResult> Detail(string id)
        {
            var app = await _applications.GetByIdAsync(id, HttpContext.RequestAborted);
            if (app is null) return NotFound();

            ViewBag.App = app;
            var decider = app.DecidedByDiscordId is null
                ? null
                : await _db.AdminUsers.AsNoTracking()
                    .FirstOrDefaultAsync(a => a.DiscordId == app.DecidedByDiscordId, HttpContext.RequestAborted);
            ViewBag.DeciderName = decider?.DisplayName ?? app.DecidedByDiscordId;
            return View("~/Views/Admin/Applications/Detail.cshtml");
        }

        // ── Accept ───────────────────────────────────────────────────────────────

        [HttpPost("/AdminApplications/Accept")]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("forms")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        public async Task<IActionResult> Accept(string id, string? note)
        {
            var admin = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
            var result = await _applications.AcceptAsync(id, admin, note, HttpContext.RequestAborted);

            if (result.Outcome == AcceptRejectOutcome.NotFound)
            {
                TempData["AdminMessage"] = "Bewerbung nicht gefunden.";
            }
            else
            {
                TempData["AdminMessage"] = "Bewerbung angenommen.";
            }
            return RedirectToAction(nameof(List));
        }

        // ── Reject ───────────────────────────────────────────────────────────────

        [HttpPost("/AdminApplications/Reject")]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("forms")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        public async Task<IActionResult> Reject(string id, string? note)
        {
            var admin = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
            var result = await _applications.RejectAsync(id, admin, note, HttpContext.RequestAborted);

            if (result.Outcome == AcceptRejectOutcome.NotFound)
            {
                TempData["AdminMessage"] = "Bewerbung nicht gefunden.";
            }
            else
            {
                TempData["AdminMessage"] = "Bewerbung abgelehnt.";
            }
            return RedirectToAction(nameof(List));
        }

        // ── Waitlist ─────────────────────────────────────────────────────────────

        [HttpGet("/AdminApplications/Waitlist")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        public async Task<IActionResult> Waitlist(string leagueId, string? season)
        {
            var allLeagues = await _staticCache.GetAllLeaguesAsync();
            ViewBag.Leagues = allLeagues;
            ViewBag.SelectedLeagueId = leagueId;
            ViewBag.SeasonFilter = season;

            if (string.IsNullOrWhiteSpace(leagueId))
            {
                ViewBag.Entries = new List<WaitlistEntry>();
                return View("~/Views/Admin/Applications/Waitlist.cshtml");
            }

            var entries = await _applications.ListWaitlistAsync(leagueId, season, HttpContext.RequestAborted);
            ViewBag.Entries = entries;
            return View("~/Views/Admin/Applications/Waitlist.cshtml");
        }

        // ── Promote from Waitlist ────────────────────────────────────────────────

        [HttpPost("/AdminApplications/PromoteFromWaitlist")]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("forms")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        public async Task<IActionResult> PromoteFromWaitlist(string waitlistEntryId, string leagueId)
        {
            var admin = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
            var result = await _applications.PromoteFromWaitlistAsync(waitlistEntryId, admin, HttpContext.RequestAborted);

            if (result.Outcome == PromoteOutcome.Ok)
            {
                TempData["AdminMessage"] = "Wartelisten-Eintrag promoviert.";
            }
            else
            {
                TempData["AdminMessage"] = result.Error ?? "Promotion fehlgeschlagen.";
            }
            return RedirectToAction(nameof(Waitlist), new { leagueId });
        }

        // ── Saison-Übersicht (Admin) ─────────────────────────────────────────────

        [HttpGet("/AdminApplications/Seasons")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        public async Task<IActionResult> Seasons(string? season)
        {
            // Default: aktuelle Season aus der ersten Liga mit CurrentSeason.
            var allLeagues = await _staticCache.GetAllLeaguesAsync();
            if (string.IsNullOrWhiteSpace(season))
            {
                season = allLeagues.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l.CurrentSeason))?.CurrentSeason
                    ?? "current";
            }
            ViewBag.Season = season;
            ViewBag.AvailableSeasons = allLeagues
                .SelectMany(l => new[] { l.CurrentSeason, l.NextSeason }
                    .Where(s => !string.IsNullOrWhiteSpace(s)))
                .Distinct()
                .OrderByDescending(s => s)
                .ToList();
            ViewBag.Rows = await _applications.GetSeasonSummaryAsync(season, HttpContext.RequestAborted);
            return View("~/Views/Admin/Applications/Seasons.cshtml");
        }

        [HttpGet("/AdminApplications/LeagueSeasons/{leagueId}")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        public async Task<IActionResult> LeagueSeasons(string leagueId)
        {
            var allLeagues = await _staticCache.GetAllLeaguesAsync();
            var league = allLeagues.FirstOrDefault(l => l.Id == leagueId);
            if (league is null) return NotFound();

            var seasons = new[] { league.CurrentSeason, league.NextSeason }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();

            var perSeason = new Dictionary<string, IReadOnlyList<Application>>();
            foreach (var s in seasons)
            {
                perSeason[s!] = await _applications.ListAsync(null, leagueId, s, 0, 500, HttpContext.RequestAborted);
            }

            ViewBag.League = league;
            ViewBag.Seasons = seasons!;
            ViewBag.PerSeason = perSeason;
            return View("~/Views/Admin/Applications/LeagueSeasons.cshtml");
        }

        [HttpPost("/AdminApplications/CloseSeason")]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("forms")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        public async Task<IActionResult> CloseSeason(string leagueId, string fromSeason, string toSeason, string mode)
        {
            var admin = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
            if (!Enum.TryParse<SeasonCloseMode>(mode, ignoreCase: true, out var parsed))
            {
                TempData["AdminMessage"] = "Unbekannter Modus.";
                return RedirectToAction(nameof(LeagueSeasons), new { leagueId });
            }

            var result = await _applications.CloseSeasonAsync(leagueId, fromSeason, toSeason, parsed, admin, HttpContext.RequestAborted);
            if (result.Outcome == CloseSeasonOutcome.Ok)
            {
                TempData["AdminMessage"] = $"Saison {fromSeason} geschlossen: {result.MovedApplications} Bewerbungen verschoben, {result.RejectedApplications} abgelehnt, {result.RemovedWaitlistEntries} Wartelisten gelöscht.";
            }
            else
            {
                TempData["AdminMessage"] = result.Error ?? "Saison-Schließen fehlgeschlagen.";
            }
            return RedirectToAction(nameof(LeagueSeasons), new { leagueId });
        }

        // ── Manuelle Registrierung (ohne Bewerbung) ──────────────────────────────

        [HttpGet("/AdminApplications/ManualRegister")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        public async Task<IActionResult> ManualRegister(string? leagueId)
        {
            await FillManualRegisterViewBagAsync();
            return View("~/Views/Admin/Applications/ManualRegister.cshtml",
                new ManualRegisterInput { LeagueId = leagueId ?? string.Empty });
        }

        [HttpPost("/AdminApplications/ManualRegister")]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("forms")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        public async Task<IActionResult> ManualRegister(ManualRegisterInput input)
        {
            if (!ModelState.IsValid)
            {
                await FillManualRegisterViewBagAsync();
                return View("~/Views/Admin/Applications/ManualRegister.cshtml", input);
            }

            var admin = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
            var cmd = new ManualRegisterCommand(
                DiscordId: input.DiscordId.Trim(),
                DiscordName: input.DiscordName.Trim(),
                GamerTag: input.GamerTag.Trim(),
                Platform: input.Platform,
                LeagueId: input.LeagueId,
                Role: input.Role);

            var result = await _applications.ManualRegisterAsync(cmd, admin, HttpContext.RequestAborted);

            if (result.Outcome == ManualRegisterOutcome.Ok)
            {
                TempData["AdminMessage"] = $"{input.DiscordName} wurde als {input.Role} registriert.";
                return RedirectToAction(nameof(ManualRegister), new { leagueId = input.LeagueId });
            }

            ModelState.AddModelError(string.Empty, result.Error ?? "Registrierung fehlgeschlagen.");
            await FillManualRegisterViewBagAsync();
            return View("~/Views/Admin/Applications/ManualRegister.cshtml", input);
        }

        private async Task FillManualRegisterViewBagAsync()
        {
            ViewBag.Leagues = await _staticCache.GetAllLeaguesAsync();
            ViewBag.AllowedPlatforms = _driverMatching.AllowedPlatforms;
        }
    }

    /// <summary>Eingabe-Modell für die manuelle Fahrer-Registrierung.</summary>
    public class ManualRegisterInput
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Discord-ID ist erforderlich.")]
        [System.ComponentModel.DataAnnotations.RegularExpression(@"^\d{5,25}$", ErrorMessage = "Discord-ID muss numerisch sein (5–25 Ziffern).")]
        public string DiscordId { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Discord-Name ist erforderlich.")]
        [System.ComponentModel.DataAnnotations.MaxLength(64)]
        public string DiscordName { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "EA-Gamer-Tag ist erforderlich.")]
        [System.ComponentModel.DataAnnotations.MaxLength(64)]
        public string GamerTag { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Plattform ist erforderlich.")]
        public string Platform { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Liga ist erforderlich.")]
        public string LeagueId { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        public string Role { get; set; } = "Stammfahrer";
    }
}
