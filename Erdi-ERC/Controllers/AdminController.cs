using Erdi_ERC.Data;
using Erdi_ERC.Helpers;
using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
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
        private readonly IOutputCacheStore _outputCache;
        private readonly IDotdVoteService _dotd;
        private readonly ILogger<AdminController> _logger;

        public AdminController(AppDbContext db, IAdminAuditService audit, IStreamScheduleQueryService streamSchedules,
            ISiteSettingsService siteSettings, IOutputCacheStore outputCache, IDotdVoteService dotd,
            ILogger<AdminController> logger)
        {
            _db = db;
            _audit = audit;
            _streamSchedules = streamSchedules;
            _siteSettings = siteSettings;
            _outputCache = outputCache;
            _dotd = dotd;
            _logger = logger;
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
            ViewBag.DotdOpen = await _dotd.HasOpenVoteAsync();
            ViewBag.AppDownloadEnabled = _siteSettings.IsAppDownloadEnabled();
            ViewBag.AppDownloadFileAvailable = _siteSettings.HasInstallerFile();
            ViewBag.MaintenanceMode = _siteSettings.IsMaintenanceMode();
            ViewBag.MaintenanceMessage = _siteSettings.GetMaintenanceMessage();

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

        // ---- Wartungsmodus Toggle (nur Superadmins) ----
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleMaintenanceMode()
        {
            if (!IsSuperAdmin) return Forbid();

            var newState = !_siteSettings.IsMaintenanceMode();
            _siteSettings.SetMaintenanceMode(newState, User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unbekannt");
            await _audit.LogAndSaveAsync(newState ? "EnableMaintenanceMode" : "DisableMaintenanceMode", "SiteSettings", "maintenanceMode",
                $"Wartungsmodus {(newState ? "aktiviert" : "deaktiviert")}");

            // OutputCache leeren: gecachte öffentliche Seiten (bis zu 2 Min) dürfen nach dem
            // Toggle nicht mehr ausgeliefert werden — sonst umgehen sie die Wartungsseite.
            _outputCache.EvictByTagAsync("public", default).GetAwaiter().GetResult();

            TempData["AdminMessage"] = newState
                ? "Wartungsmodus ist jetzt AKTIV — alle Nicht-Admins sehen die Wartungsseite."
                : "Wartungsmodus ist jetzt AUS — die Seite ist wieder öffentlich.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveMaintenanceMessage(string maintenanceMessage)
        {
            if (!IsSuperAdmin) return Forbid();

            var message = (maintenanceMessage ?? string.Empty).Trim();
            if (message.Length > 300)
            {
                TempData["AdminMessage"] = "Wartungsmeldung darf höchstens 300 Zeichen lang sein.";
                return RedirectToAction(nameof(Index));
            }

            _siteSettings.SetMaintenanceMessage(message, User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unbekannt");
            await _audit.LogAndSaveAsync("UpdateMaintenanceMessage", "SiteSettings", "maintenanceMessage", "Wartungsmeldung aktualisiert");
            TempData["AdminMessage"] = "Wartungsmeldung gespeichert.";
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

            // Erst in eine Temp-Datei schreiben und prüfen, ob ClosedXML sie öffnen kann.
            // Die bisherige Arbeitsmappe wird nur ersetzt, wenn die neue lesbar ist — sonst
            // zerstört ein kaputter Upload die Ewige Liste für alle Seitenaufrufe.
            var tempPath = path + ".tmp";
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            {
                await workbook.CopyToAsync(stream);
            }

            try
            {
                using var probe = new ClosedXML.Excel.XLWorkbook(tempPath);
                _ = probe.Worksheets.Count;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.IO.File.Delete(tempPath);
                _logger.LogWarning(ex, "[EwigeListe] Upload verworfen: '{Name}' ist keine lesbare Arbeitsmappe",
                    workbook.FileName);
                TempData["AdminMessage"] = "Die Datei ist keine lesbare .xlsx-Arbeitsmappe. " +
                    "Die bisherige Ewige Liste bleibt unverändert.";
                return RedirectToAction(nameof(Index));
            }

            System.IO.File.Move(tempPath, path, overwrite: true);

            await _audit.LogAsync("UploadEwigeListe", "EwigeListe", "active.xlsx", $"Size={workbook.Length}");
            TempData["AdminMessage"] = "Ewige Liste erfolgreich aktualisiert.";
            return RedirectToAction(nameof(Index));
        }
    }
}
