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
    /// Verwaltung des Bewerbungs-Workflows mit strukturierter, benutzerfreundlicher Navigation.
    /// </summary>
    [Route("admin/applications")]
    [Authorize(Policy = "Admin.Applications")]
    public class AdminApplicationsController : Controller
    {
        private readonly IApplicationManagementService _appService;
        private readonly IAdminAuditService _audit;
        private readonly ILogger<AdminApplicationsController> _logger;
        private readonly AppDbContext _db;

        public AdminApplicationsController(
            IApplicationManagementService appService,
            IAdminAuditService audit,
            ILogger<AdminApplicationsController> logger,
            AppDbContext db)
        {
            _appService = appService;
            _audit = audit;
            _logger = logger;
            _db = db;
        }

        private string GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "System";

        // ---- Dashboard & Übersicht ----

        /// <summary>
        /// Bewerbungs-Dashboard mit Statistiken und schnellen Aktionen.
        /// </summary>
        [HttpGet("")]
        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard(string? sort = null)
        {
            var stats = await _appService.GetStatisticsAsync();
            var metrics = await _appService.GetMetricsAsync();
            var openApps = await _appService.GetAllApplicationsAsync(acceptedFilter: false, excludeRejected: true, pageSize: 10, sort: sort);

            var viewModel = new ApplicationDashboardViewModel
            {
                Statistics = stats,
                Metrics = metrics,
                RecentApplications = openApps,
                CurrentSort = sort
            };

            return View("~/Views/Admin/Applications/Dashboard.cshtml", viewModel);
        }

        /// <summary>
        /// Vollständige Liste aller Bewerbungen (mit Pagination und Filterung).
        /// </summary>
        [HttpGet("list")]
        public async Task<IActionResult> List(string? division = null, bool? accepted = null, int page = 1, string? sort = null)
        {
            var apps = await _appService.GetAllApplicationsAsync(division, accepted, pageSize: 50, pageNumber: page, sort: sort);
            var stats = await _appService.GetStatisticsAsync();

            // Filter-Optionen: alle Divisionen aus vorhandenen Bewerbungen plus alle aktiven Ligen,
            // damit auch frisch angelegte Ligen (ohne Bewerbung) filterbar sind.
            var appDivisions = await _db.ApplicationForms.Select(a => a.Division).Distinct().ToListAsync();
            var leagueNames = await _db.Leagues.Where(l => !l.IsArchived).Select(l => l.Name).ToListAsync();
            var availableDivisions = appDivisions
                .Concat(leagueNames)
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var viewModel = new ApplicationListViewModel
            {
                Applications = apps,
                Statistics = stats,
                CurrentDivision = division,
                CurrentAcceptedFilter = accepted,
                CurrentPage = page,
                CurrentSort = sort,
                AvailableDivisions = availableDivisions
            };

            return View("~/Views/Admin/Applications/List.cshtml", viewModel);
        }

        // ---- Filterte Ansichten ----

        /// <summary>
        /// Bewerbungen nach Division (Main, Second, Rookie, Community).
        /// </summary>
        [HttpGet("by-division/{division}")]
        public async Task<IActionResult> ByDivision(string division, int page = 1)
        {
            var apps = await _appService.GetAllApplicationsAsync(division, pageSize: 50, pageNumber: page);
            var stats = await _appService.GetStatisticsAsync();

            var viewModel = new ApplicationListViewModel
            {
                Applications = apps,
                Statistics = stats,
                CurrentDivision = division,
                CurrentPage = page
            };

            return View("~/Views/Admin/Applications/ByDivision.cshtml", viewModel);
        }

        /// <summary>
        /// Nur offene (nicht akzeptierte) Bewerbungen.
        /// </summary>
        [HttpGet("open")]
        public async Task<IActionResult> Open(int page = 1, string? sort = null)
        {
            var apps = await _appService.GetAllApplicationsAsync(acceptedFilter: false, excludeRejected: true, pageSize: 50, pageNumber: page, sort: sort);
            var stats = await _appService.GetStatisticsAsync();

            var viewModel = new ApplicationListViewModel
            {
                Applications = apps,
                Statistics = stats,
                CurrentPage = page,
                CurrentSort = sort
            };

            return View("~/Views/Admin/Applications/Open.cshtml", viewModel);
        }

        /// <summary>
        /// Nur akzeptierte Bewerbungen (für Audit und Rückgängigmachung).
        /// </summary>
        [HttpGet("accepted")]
        public async Task<IActionResult> Accepted(int page = 1)
        {
            var apps = await _appService.GetAllApplicationsAsync(acceptedFilter: true, pageSize: 50, pageNumber: page);
            var stats = await _appService.GetStatisticsAsync();

            var viewModel = new ApplicationListViewModel
            {
                Applications = apps,
                Statistics = stats,
                CurrentPage = page
            };

            return View("~/Views/Admin/Applications/Accepted.cshtml", viewModel);
        }

        // ---- Detail-Ansicht ----

        /// <summary>
        /// Detailansicht einer Bewerbung mit vollem Audit-Trail.
        /// Liefert außerdem die verfügbaren Ligen für das Rollen-Zuweisungs-Modal.
        /// </summary>
        [HttpGet("detail/{id}")]
        public async Task<IActionResult> Detail(int id)
        {
            var appWithHistory = await _appService.GetApplicationWithHistoryAsync(id);
            if (appWithHistory is null)
                return NotFound();

            // Load leagues for dropdown (archivierte Ligen sind keine gültigen Ziele)
            var leagues = await _db.Leagues
                .Where(l => !l.IsArchived)
                .OrderBy(l => l.Name)
                .ToListAsync();
            ViewData["Leagues"] = leagues;

            return View("~/Views/Admin/Applications/Detail.cshtml", appWithHistory);
        }

        // ---- Workflow-Aktionen ----

        /// <summary>
        /// Akzeptiert eine Bewerbung und erstellt das Fahrer-Profil.
        /// </summary>
        [HttpPost("accept/{id}")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(int id, string? leagueId = null)
        {
            var result = await _appService.AcceptApplicationAsync(id, GetCurrentUserId(), overrideLeagueId: leagueId);

            if (result.Success)
            {
                await _audit.LogAsync("AdminAction", "ApplicationForm", id.ToString(),
                    $"Accepted application");
                TempData["Success"] = result.Message;
            }
            else
            {
                TempData["Error"] = result.Message;
            }

            return RedirectToAction("Detail", new { id });
        }

        /// <summary>
        /// Lehnt eine Bewerbung ab (mit Grund optional).
        /// </summary>
        [HttpPost("reject/{id}")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? reason)
        {
            // Make reason optional to simplify the rejection flow
            if (string.IsNullOrWhiteSpace(reason))
            {
                reason = "Keine Angabe";
            }

            var result = await _appService.RejectApplicationAsync(id, reason, GetCurrentUserId());

            if (result.Success)
            {
                TempData["Success"] = result.Message;
                return RedirectToAction("Open");
            }

            TempData["Error"] = result.Message;
            return RedirectToAction("Detail", new { id });
        }

        /// <summary>
        /// Macht die Akzeptanz einer Bewerbung rückgängig und entfernt zugewiesene Rolle.
        /// </summary>
        [HttpPost("unaccept/{id}")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unaccept(int id, string? reason)
        {
            // Accept unaccept without requiring reason to simplify admin flow
            if (string.IsNullOrWhiteSpace(reason))
                reason = "Keine Angabe";

            _logger.LogInformation("Unaccept called for Application {Id} by {User}. Reason: {Reason}", id, GetCurrentUserId(), reason);

            var result = await _appService.UnacceptApplicationAsync(id, reason, GetCurrentUserId());

            if (result.Success)
            {
                await _audit.LogAsync("AdminAction", "ApplicationForm", id.ToString(),
                    $"Unaccepted application");
                TempData["Success"] = result.Message;
                _logger.LogInformation("Unaccept succeeded for Application {Id}", id);
            }
            else
            {
                _logger.LogWarning("Unaccept failed for Application {Id}: {Message}", id, result.Message);
                TempData["Error"] = result.Message;
            }

            return RedirectToAction("Detail", new { id });
        }

        /// <summary>
        /// Löscht eine Bewerbung.
        /// </summary>
        [HttpPost("delete/{id}")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _appService.DeleteApplicationAsync(id, GetCurrentUserId());

            if (result.Success)
            {
                await _audit.LogAsync("AdminAction", "ApplicationForm", id.ToString(),
                    $"Deleted application");
                TempData["Success"] = result.Message;
                return RedirectToAction("Open");
            }

            TempData["Error"] = result.Message;
            return RedirectToAction("Detail", new { id });
        }

        /// <summary>
        /// Kennzeichnet eine Bewerbung zur Überprüfung.
        /// </summary>
        [HttpPost("flag/{id}")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Flag(int id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Grund erforderlich";
                return RedirectToAction("Detail", new { id });
            }

            var result = await _appService.FlagForReviewAsync(id, reason, GetCurrentUserId());

            if (result.Success)
            {
                TempData["Success"] = result.Message;
            }
            else
            {
                TempData["Error"] = result.Message;
            }

            return RedirectToAction("Detail", new { id });
        }

        // ---- Batch-Operationen ----

        /// <summary>
        /// Akzeptiert mehrere Bewerbungen gleichzeitig.
        /// </summary>
        [HttpPost("accept-multiple")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptMultiple(int[] applicationIds)
        {
            if (applicationIds is null || applicationIds.Length == 0)
            {
                TempData["Error"] = "Keine Bewerbungen ausgewählt";
                return RedirectToAction("Open");
            }

            var result = await _appService.AcceptMultipleAsync(applicationIds, GetCurrentUserId());

            TempData["Success"] = $"Verarbeitet: {result.SuccessCount} erfolgreich, {result.FailureCount} Fehler";
            return RedirectToAction("Open");
        }

        // ---- Automatisierung ----

        /// <summary>
        /// Entfernt abgelaufene Bewerbungen (über 48h nach Akzeptanz).
        /// </summary>
        [HttpPost("cleanup-expired")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CleanupExpired()
        {
            var removed = await _appService.RemoveExpiredApplicationsAsync();
            TempData["Success"] = $"{removed} abgelaufene Bewerbungen entfernt";
            return RedirectToAction("Dashboard");
        }

        // ---- Reporting & Export ----

        /// <summary>
        /// Exportiert Bewerbungen als CSV.
        /// </summary>
        [HttpGet("export")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        public async Task<IActionResult> Export(string? division = null)
        {
            try
            {
                var csv = await _appService.ExportAsCSVAsync(division);
                var fileName = $"applications-{DateTime.Now:yyyy-MM-dd}.csv";
                return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting applications");
                TempData["Error"] = "Fehler beim Export";
                return RedirectToAction("Dashboard");
            }
        }

        /// <summary>
        /// Zeigt Bewerbungs-Metriken an.
        /// </summary>
        [HttpGet("metrics")]
        [Authorize(Policy = "Admin.Applications.Metrics")]
        public async Task<IActionResult> Metrics()
        {
            var metrics = await _appService.GetMetricsAsync();
            var stats = await _appService.GetStatisticsAsync();

            var viewModel = new ApplicationMetricsViewModel
            {
                Metrics = metrics,
                Statistics = stats
            };

            return View("~/Views/Admin/Applications/Metrics.cshtml", viewModel);
        }

        // ---- Review-Status & Rollen-Zuweisung ----

        /// <summary>
        /// Setzt eine Bewerbung als abgelehnt oder entfernt die Ablehnung.
        /// </summary>
        [HttpPost("set-rejected/{id}")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetRejected(int id, bool rejected, string? note)
        {
            if (rejected)
            {
                var reason = string.IsNullOrWhiteSpace(note) ? "Keine Angabe" : note;
                var result = await _appService.RejectApplicationAsync(id, reason, GetCurrentUserId());
                if (result.Success)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction("Open");
                }

                TempData["Error"] = result.Message;
                return RedirectToAction("Detail", new { id });
            }

            // Ablehnung aufheben: Flag zurücksetzen + optionale Notiz übernehmen.
            var app = await GetApplicationOrNotFound(id);
            if (app == null) return NotFound();
            app.IsRejected = false;
            app.RejectedAt = null;
            if (!string.IsNullOrWhiteSpace(note))
                app.ReviewNote = note;
            await _appService.UpdateApplicationDirectAsync(app, GetCurrentUserId());
            TempData["Success"] = "Ablehnung entfernt (Notiz gesetzt).";
            return RedirectToAction("Detail", new { id });
        }

        /// <summary>
        /// Weist einer angenommenen Bewerbung eine finale Fahrer-Rolle zu
        /// (Stammfahrer, Ersatzfahrer, Academy).
        /// </summary>
        [HttpPost("assign-role/{id}")]
        [Authorize(Policy = "Admin.Applications.Manage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRole(int id, string? assignedRole, string? leagueId)
        {
            if (string.IsNullOrWhiteSpace(assignedRole))
            {
                TempData["Error"] = "Bitte eine Rolle auswählen.";
                return RedirectToAction("Detail", new { id });
            }

            var app = await _appService.GetApplicationByIdAsync(id);
            if (app == null) return NotFound();

            // Rolle zuerst persistieren, damit der Accept-Flow sie für die
            // Reserve-Einstufung des Liga-Eintrags berücksichtigt.
            app.AssignedRole = assignedRole;
            await _appService.UpdateApplicationDirectAsync(app, GetCurrentUserId());

            if (!app.IsAccepted)
            {
                // Annahme inkl. Liga-Eintrag: gewählte Liga übersteuert die beworbene.
                var acceptResult = await _appService.AcceptApplicationAsync(id, GetCurrentUserId(), overrideLeagueId: leagueId);
                if (!acceptResult.Success)
                {
                    TempData["Error"] = "Annahme fehlgeschlagen: " + acceptResult.Message;
                    return RedirectToAction("Detail", new { id });
                }
            }
            else if (!string.IsNullOrWhiteSpace(leagueId))
            {
                // Bereits angenommen: in die gewählte Liga umziehen.
                var assignResult = await _appService.AssignToLeagueAsync(id, leagueId, assignedRole, GetCurrentUserId());
                if (!assignResult.Success)
                {
                    TempData["Error"] = "Liga-Zuweisung fehlgeschlagen: " + assignResult.Message;
                    return RedirectToAction("Detail", new { id });
                }
            }

            await _audit.LogAsync("AdminAction", "ApplicationForm", id.ToString(),
                $"Rolle zugewiesen: {assignedRole}, League={leagueId}");

            TempData["Success"] = $"Rolle \"{assignedRole}\" zugewiesen.";
            return RedirectToAction("Detail", new { id });
        }

        // ── Private Helpers ──────────────────────────────────────────────────────
        private async Task<ApplicationForm?> GetApplicationOrNotFound(int id)
            => await _appService.GetApplicationByIdAsync(id);
    }

    // ---- ViewModels ----

    public class ApplicationDashboardViewModel
    {
        public ApplicationStatistics Statistics { get; set; } = null!;
        public ApplicationMetrics Metrics { get; set; } = null!;
        public List<ApplicationForm> RecentApplications { get; set; } = new();
        public string? CurrentSort { get; set; }
    }

    public class ApplicationListViewModel
    {
        public List<ApplicationForm> Applications { get; set; } = new();
        public ApplicationStatistics Statistics { get; set; } = null!;
        public string? CurrentDivision { get; set; }
        public bool? CurrentAcceptedFilter { get; set; }
        public int CurrentPage { get; set; } = 1;
        public string? CurrentSort { get; set; }
        public List<string> AvailableDivisions { get; set; } = new();
    }

    public class ApplicationMetricsViewModel
    {
        public ApplicationMetrics Metrics { get; set; } = null!;
        public ApplicationStatistics Statistics { get; set; } = null!;
    }
}
