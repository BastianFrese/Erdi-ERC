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

        public AdminApplicationsController(
            IApplicationService applications,
            IStaticDataCache staticCache,
            AppDbContext db)
        {
            _applications = applications;
            _staticCache = staticCache;
            _db = db;
        }

        // ── List ─────────────────────────────────────────────────────────────────

        [HttpGet("/AdminApplications/List")]
        public async Task<IActionResult> List(string? status, string? leagueId, int page = 1)
        {
            var allLeagues = await _staticCache.GetAllLeaguesAsync();
            ViewBag.Leagues = allLeagues;
            ViewBag.StatusFilter = status;
            ViewBag.LeagueFilter = leagueId;
            ViewBag.Page = page;

            ApplicationStatus? statusFilter = null;
            if (!string.IsNullOrWhiteSpace(status)
                && Enum.TryParse<ApplicationStatus>(status, ignoreCase: true, out var parsed))
            {
                statusFilter = parsed;
            }

            const int PageSize = 25;
            var skip = (Math.Max(1, page) - 1) * PageSize;
            var items = await _applications.ListAsync(statusFilter, leagueId, skip, PageSize, HttpContext.RequestAborted);
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
        public async Task<IActionResult> Waitlist(string leagueId)
        {
            var allLeagues = await _staticCache.GetAllLeaguesAsync();
            ViewBag.Leagues = allLeagues;
            ViewBag.SelectedLeagueId = leagueId;

            if (string.IsNullOrWhiteSpace(leagueId))
            {
                ViewBag.Entries = new List<WaitlistEntry>();
                return View("~/Views/Admin/Applications/Waitlist.cshtml");
            }

            var entries = await _applications.ListWaitlistAsync(leagueId, HttpContext.RequestAborted);
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
    }
}
