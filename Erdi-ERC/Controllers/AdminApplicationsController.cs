using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace <OWNER_HANDLE>_ERC.Controllers
{
    /// <summary>
    /// Admin-Verwaltung des Bewerbungs-Workflows: Dashboard, EINE gefilterte Liste
    /// und Detail mit einheitlicher Aktionsleiste. Annehmen nimmt Liga und Rolle
    /// immer zusammen entgegen — es gibt keinen zweiten Annahme-Pfad mehr.
    /// </summary>
    [Route("admin/applications")]
    [Authorize(Policy = "Admin.Applications")]
    public class AdminApplicationsController : Controller
    {
        private readonly IApplicationWorkflowService _workflow;
        private readonly IApplicationQueryService _queries;
        private readonly IAdminAuditService _audit;
        private readonly AppDbContext _db;
        private readonly ILogger<AdminApplicationsController> _logger;

        public AdminApplicationsController(
            IApplicationWorkflowService workflow,
            IApplicationQueryService queries,
            IAdminAuditService audit,
            AppDbContext db,
            ILogger<AdminApplicationsController> logger)
        {
            _workflow = workflow;
            _queries = queries;
            _audit = audit;
            _db = db;
            _logger = logger;
        }

        private string GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "System";

        private Task<List<League>> GetActiveLeaguesAsync() =>
            _db.Leagues
                .AsNoTracking()
                .Where(l => !l.IsArchived)
                .OrderBy(l => l.SortOrder).ThenBy(l => l.Name)
                .ToListAsync();

        // ── Dashboard ────────────────────────────────────────────────────────────

        /// <summary>Dashboard: Statistiken, Metriken, Kapazitäten und neueste offene Bewerbungen.</summary>
        [HttpGet("")]
        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard(string? sort = null)
        {
            var openApps = await _queries.SearchAsync(new ApplicationSearchFilter
            {
                Status = ApplicationStatus.Open,
                Sort = sort,
                PageSize = 10
            });

            var viewModel = new ApplicationDashboardViewModel
            {
                Statistics = await _queries.GetStatisticsAsync(),
                Metrics = await _queries.GetMetricsAsync(),
                RecentApplications = openApps.Items,
                CurrentSort = sort,
                Capacities = await _queries.GetLeagueCapacitiesAsync()
            };

            return View("~/Views/Admin/Applications/Dashboard.cshtml", viewModel);
        }

        // ── Liste (ersetzt Open/Accepted/ByDivision/Duplicates) ──────────────────

        /// <summary>Die eine Bewerbungsliste: Status-, Liga-, Flag-, Duplikat-Filter + Suche.</summary>
        [HttpGet("list")]
        public async Task<IActionResult> List(
            ApplicationStatus? status = null,
            string? leagueId = null,
            string? search = null,
            string? sort = null,
            bool flagged = false,
            bool duplicates = false,
            bool withoutLeague = false,
            int page = 1)
        {
            var filter = new ApplicationSearchFilter
            {
                Status = status,
                LeagueId = leagueId,
                Search = search,
                Sort = sort,
                FlaggedOnly = flagged,
                DuplicatesOnly = duplicates,
                WithoutLeagueOnly = withoutLeague,
                Page = Math.Max(1, page),
                PageSize = 50
            };

            var viewModel = new ApplicationListViewModel
            {
                Result = await _queries.SearchAsync(filter),
                Statistics = await _queries.GetStatisticsAsync(),
                Leagues = await GetActiveLeaguesAsync(),
                Filter = filter
            };

            return View("~/Views/Admin/Applications/List.cshtml", viewModel);
        }

        // ── Legacy-Redirects (alte Bookmarks/Links) ──────────────────────────────

        [HttpGet("open")]
        public IActionResult Open() => RedirectToAction(nameof(List), new { status = ApplicationStatus.Open });

        [HttpGet("accepted")]
        public IActionResult Accepted() => RedirectToAction(nameof(List), new { status = ApplicationStatus.Accepted });

        [HttpGet("duplicates")]
        public IActionResult Duplicates() => RedirectToAction(nameof(List), new { duplicates = true });

        [HttpGet("metrics")]
        public IActionResult Metrics() => RedirectToAction(nameof(Dashboard));

        [HttpGet("by-division/{division}")]
        public async Task<IActionResult> ByDivision(string division)
        {
            // Alte Namens-URLs auf den Liga-Filter mappen, wo möglich.
            var leagueId = await _db.Leagues
                .Where(l => l.Name == division)
                .Select(l => l.Id)
                .FirstOrDefaultAsync();
            return leagueId is null
                ? RedirectToAction(nameof(List), new { search = division })
                : RedirectToAction(nameof(List), new { leagueId });
        }

        // ── Detail ───────────────────────────────────────────────────────────────

        /// <summary>Detailansicht mit Audit-Verlauf und den verfügbaren Ligen für die Aktionsleiste.</summary>
        [HttpGet("detail/{id}")]
        public async Task<IActionResult> Detail(int id)
        {
            var appWithHistory = await _queries.GetWithHistoryAsync(id);
            if (appWithHistory is null)
                return NotFound();

            ViewData["Leagues"] = await GetActiveLeaguesAsync();
            return View("~/Views/Admin/Applications/Detail.cshtml", appWithHistory);
        }

        // ── Workflow-Aktionen ────────────────────────────────────────────────────

        /// <summary>
        /// Nimmt die Bewerbung an — Liga und Rolle kommen zusammen aus dem Formular.
        /// Ohne Liga-Auswahl greift die beworbene Liga.
        /// </summary>
        [HttpPost("accept/{id}")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(int id, string? leagueId, string? assignedRole)
        {
            var result = await _workflow.AcceptAsync(id, leagueId, assignedRole, GetCurrentUserId());
            SetResultMessage(result);
            return RedirectToAction(nameof(Detail), new { id });
        }

        /// <summary>Lehnt die Bewerbung ab (Grund optional).</summary>
        [HttpPost("reject/{id}")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? reason)
        {
            var result = await _workflow.RejectAsync(id, string.IsNullOrWhiteSpace(reason) ? "Keine Angabe" : reason, GetCurrentUserId());
            SetResultMessage(result);
            return result.Success
                ? RedirectToAction(nameof(List), new { status = ApplicationStatus.Open })
                : RedirectToAction(nameof(Detail), new { id });
        }

        /// <summary>Setzt eine angenommene oder abgelehnte Bewerbung zurück auf "offen".</summary>
        [HttpPost("reopen/{id}")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reopen(int id, string? note)
        {
            var result = await _workflow.ReopenAsync(id, note, GetCurrentUserId());
            SetResultMessage(result);
            return RedirectToAction(nameof(Detail), new { id });
        }

        /// <summary>Zieht einen angenommenen Fahrer in eine andere Liga um.</summary>
        [HttpPost("move-league/{id}")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveLeague(int id, string? leagueId, string? assignedRole)
        {
            if (string.IsNullOrWhiteSpace(leagueId))
            {
                TempData["Error"] = "Bitte eine Liga auswählen.";
                return RedirectToAction(nameof(Detail), new { id });
            }

            var result = await _workflow.MoveToLeagueAsync(id, leagueId, assignedRole, GetCurrentUserId());
            SetResultMessage(result);
            return RedirectToAction(nameof(Detail), new { id });
        }

        /// <summary>Löscht die Bewerbung endgültig.</summary>
        [HttpPost("delete/{id}")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _workflow.DeleteAsync(id, GetCurrentUserId());
            SetResultMessage(result);
            return result.Success
                ? RedirectToAction(nameof(List))
                : RedirectToAction(nameof(Detail), new { id });
        }

        /// <summary>Markiert die Bewerbung zur Überprüfung.</summary>
        [HttpPost("flag/{id}")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Flag(int id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Grund erforderlich";
                return RedirectToAction(nameof(Detail), new { id });
            }

            var result = await _workflow.FlagAsync(id, reason, GetCurrentUserId());
            SetResultMessage(result);
            return RedirectToAction(nameof(Detail), new { id });
        }

        /// <summary>Hebt die Überprüfungs-Markierung auf.</summary>
        [HttpPost("unflag/{id}")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unflag(int id)
        {
            var result = await _workflow.UnflagAsync(id, GetCurrentUserId());
            SetResultMessage(result);
            return RedirectToAction(nameof(Detail), new { id });
        }

        /// <summary>Setzt oder beendet die Probezeit.</summary>
        [HttpPost("trial/{id}")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetTrial(int id, bool onTrial, int? days)
        {
            var result = await _workflow.SetTrialAsync(id, onTrial, days, GetCurrentUserId());
            SetResultMessage(result);
            return RedirectToAction(nameof(Detail), new { id });
        }

        /// <summary>Fügt eine zeitgestempelte Kontakt-/Review-Notiz hinzu (erscheint im Verlauf).</summary>
        [HttpPost("note/{id}")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNote(int id, string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                TempData["Error"] = "Notiz darf nicht leer sein.";
                return RedirectToAction(nameof(Detail), new { id });
            }

            if (!await _db.ApplicationForms.AnyAsync(a => a.Id == id)) return NotFound();

            var trimmed = text.Trim();
            if (trimmed.Length > 1000) trimmed = trimmed[..1000];

            await _audit.LogAndSaveAsync("Notiz", "ApplicationForm", id.ToString(), trimmed);
            TempData["Success"] = "Notiz hinzugefügt.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        // ── Housekeeping & Export ────────────────────────────────────────────────

        /// <summary>Entfernt abgelehnte Bewerbungen, die älter als 30 Tage sind.</summary>
        [HttpPost("cleanup-expired")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CleanupExpired()
        {
            var removed = await _workflow.CleanupExpiredAsync(GetCurrentUserId());
            TempData["Success"] = $"{removed} abgelaufene Bewerbungen entfernt";
            return RedirectToAction(nameof(Dashboard));
        }

        /// <summary>Exportiert Bewerbungen als CSV, optional auf eine Liga gefiltert.</summary>
        [HttpGet("export")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        public async Task<IActionResult> Export(string? leagueId = null)
        {
            try
            {
                var csv = await _queries.ExportCsvAsync(leagueId);
                var fileName = $"applications-{DateTime.Now:yyyy-MM-dd}.csv";
                return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting applications");
                TempData["Error"] = "Fehler beim Export";
                return RedirectToAction(nameof(Dashboard));
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private void SetResultMessage(ApplicationActionResult result)
        {
            if (result.Success)
                TempData["Success"] = result.Message;
            else
                TempData["Error"] = result.Message;
        }
    }

    // ── ViewModels ───────────────────────────────────────────────────────────────

    public class ApplicationDashboardViewModel
    {
        public ApplicationStatistics Statistics { get; set; } = null!;
        public ApplicationMetrics Metrics { get; set; } = null!;
        public List<ApplicationForm> RecentApplications { get; set; } = new();
        public string? CurrentSort { get; set; }

        /// <summary>Kapazitäts-/Warteliste-Übersicht je Liga, die aktuell Bewerbungen annimmt.</summary>
        public List<LeagueCapacityRow> Capacities { get; set; } = new();
    }

    public class ApplicationListViewModel
    {
        public ApplicationSearchResult Result { get; set; } = new();
        public ApplicationStatistics Statistics { get; set; } = null!;

        /// <summary>Aktive Ligen für den Liga-Filter.</summary>
        public List<League> Leagues { get; set; } = new();

        /// <summary>Aktive Filter (für Formular-Zustand und Pagination-Links).</summary>
        public ApplicationSearchFilter Filter { get; set; } = new();
    }
}
