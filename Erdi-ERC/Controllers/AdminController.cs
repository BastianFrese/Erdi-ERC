using Erdi_ERC.Data;
using Erdi_ERC.Helpers;
using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Erdi_ERC.Controllers
{
    /// <summary>Admin-Dashboard, Audit-Logs und Ewige-Liste-Upload. Liga-CRUD → AdminLeagueManagement, Admins → AdminPermissions, Setups → AdminSetups.</summary>
    [Authorize(Policy = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IAdminAuditService _audit;
        private readonly IStreamScheduleQueryService _streamSchedules;
        private readonly ISiteSettingsService _siteSettings;

        public AdminController(AppDbContext db, IAdminAuditService audit, IStreamScheduleQueryService streamSchedules,
            ISiteSettingsService siteSettings)
        {
            _db = db;
            _audit = audit;
            _streamSchedules = streamSchedules;
            _siteSettings = siteSettings;
        }

        // ---- Dashboard / Main Übersicht ----
        [HttpGet]
        [Route("admin")]
        [Route("admin/index")]
        public async Task<IActionResult> Index()
        {
            var leagues = await _db.Leagues
                .OrderBy(l => l.SortOrder)
                .ThenBy(l => l.Name)
                .ToListAsync();

            // "Offen" = wirklich unbearbeitet (früher zählte !IsAccepted auch Abgelehnte mit).
            ViewBag.TotalRaces = await _db.RaceResults.CountAsync();
            ViewBag.TotalDrivers = await _db.DriverStandings.Select(s => s.Driver).Distinct().CountAsync();
            ViewBag.AuditCount24h = await _db.AdminAuditLogs.CountAsync(x => x.CreatedAt >= DateTime.UtcNow.AddHours(-24));
            ViewBag.NextStream = await _streamSchedules.GetNextStreamScheduleAsync();
            ViewBag.RecentEaNameChanges = await _db.AdminAuditLogs
                .Where(x => x.Action == "UpdateEaName")
                .OrderByDescending(x => x.CreatedAt)
                .Take(8)
                .ToListAsync();

            ViewBag.IsSuperAdmin = IsSuperAdmin;
            ViewBag.AppDownloadEnabled = _siteSettings.IsAppDownloadEnabled();
            ViewBag.AppDownloadFileAvailable = _siteSettings.HasInstallerFile();

            return View(leagues);
        }

        // ---- App-Download Toggle (nur Superadmins) ----
        private bool IsSuperAdmin => User.HasClaim("erdi:superadmin", "true");

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAppDownload()
        {
            if (!IsSuperAdmin) return Forbid();

            var newState = !_siteSettings.IsAppDownloadEnabled();
            _siteSettings.SetAppDownloadEnabled(newState, User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unbekannt");
            await _audit.LogAndSaveAsync(newState ? "EnableAppDownload" : "DisableAppDownload", "SiteSettings", "appDownload",
                $"App-Download {(newState ? "aktiviert" : "deaktiviert")}");
            TempData["AdminMessage"] = newState
                ? "App-Download ist jetzt SICHTBAR (Layout-Cache: greift binnen ~30s)."
                : "App-Download ist jetzt AUSGEBLENDET (Layout-Cache: greift binnen ~30s).";
            return RedirectToAction(nameof(Index));
        }

        // ---- Audit Logs ----
        [HttpGet]
        [Authorize(Policy = "Admin.System.AuditLogs")]
        public async Task<IActionResult> AuditLogs()
        {
            var logs = await _db.AdminAuditLogs
                .OrderByDescending(x => x.CreatedAt)
                .Take(500)
                .ToListAsync();

            return View(logs);
        }

        // ---- Ewige Liste Upload ----
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadEwigeListe(IFormFile? workbook)
        {
            if (workbook is null || workbook.Length == 0)
            {
                TempData["AdminMessage"] = "Bitte eine .xlsx Datei auswählen.";
                return RedirectToAction(nameof(Index));
            }

            var ext = Path.GetExtension(workbook.FileName).ToLowerInvariant();
            if (ext != ".xlsx")
            {
                TempData["AdminMessage"] = "Nur .xlsx Dateien sind erlaubt.";
                return RedirectToAction(nameof(Index));
            }

            var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var path = EwigeWorkbookHelper.GetPath(env);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            await workbook.CopyToAsync(stream);

            await _audit.LogAsync("UploadEwigeListe", "EwigeListe", "active.xlsx", $"Size={workbook.Length}");
            TempData["AdminMessage"] = "Ewige Liste erfolgreich aktualisiert.";
            return RedirectToAction(nameof(Index));
        }
    }
}
