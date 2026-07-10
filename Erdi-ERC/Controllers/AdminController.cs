using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Helpers;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace <OWNER_HANDLE>_ERC.Controllers
{
    /// <summary>Admin-Dashboard, Audit-Logs und Ewige-Liste-Upload. Liga-CRUD → AdminLeagueManagement, Admins → AdminPermissions, Setups → AdminSetups.</summary>
    [Authorize(Policy = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IAdminAuditService _audit;

        public AdminController(AppDbContext db, IAdminAuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        private async Task<StreamSchedule?> GetNextStreamScheduleAsync()
        {
            var schedules = await _db.StreamSchedules.ToListAsync();
            if (schedules.Count == 0) return null;

            // UtcNow konsistent zum Save-Pfad in AdminCommunityController, der recurring StartAt
            // mit UTC berechnet. Mischen würde sonst zu 1-2h Drift (Sommer-/Winterzeit) führen.
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
            ViewBag.OpenApplications = await _db.ApplicationForms.CountAsync(x => x.Status == ApplicationStatus.Open);
            ViewBag.AcceptedApplications = await _db.ApplicationForms.CountAsync(x => x.Status == ApplicationStatus.Accepted);
            ViewBag.TotalRaces = await _db.RaceResults.CountAsync();
            ViewBag.TotalDrivers = await _db.DriverStandings.Select(s => s.Driver).Distinct().CountAsync();
            ViewBag.AuditCount24h = await _db.AdminAuditLogs.CountAsync(x => x.CreatedAt >= DateTime.UtcNow.AddHours(-24));
            ViewBag.NextStream = await GetNextStreamScheduleAsync();
            ViewBag.RecentEaNameChanges = await _db.AdminAuditLogs
                .Where(x => x.Action == "UpdateEaName")
                .OrderByDescending(x => x.CreatedAt)
                .Take(8)
                .ToListAsync();

            return View(leagues);
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
