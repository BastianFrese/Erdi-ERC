using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace <OWNER_HANDLE>_ERC.Controllers
{
    [Authorize(Policy = "Admin.Drivers")]
    public class AdminAchievementsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IAdminAuditService _audit;
        private readonly IStaticDataCache _staticCache;

        public AdminAchievementsController(AppDbContext db, IAdminAuditService audit, IStaticDataCache staticCache)
        {
            _db = db;
            _audit = audit;
            _staticCache = staticCache;
        }

        // ---- Custom Achievements ----
        [HttpGet]
        [Authorize(Policy = "Admin.Drivers.Achievements")]
        public async Task<IActionResult> Index(int? editId)
        {
            var items = await _db.CustomAchievements
                .OrderByDescending(x => x.AwardedAt)
                .ToListAsync();

            ViewBag.Leagues = await _staticCache.GetAllLeaguesAsync();
            ViewBag.KnownDrivers = await _db.DriverStandings
                .Select(s => s.Driver)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();
            ViewBag.EditItem = editId.HasValue
                ? items.FirstOrDefault(x => x.Id == editId.Value)
                : null;

            return View("~/Views/Admin/Achievements.cshtml", items);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin.Drivers.Achievements")]
        public async Task<IActionResult> Save(CustomAchievement model)
        {
            if (model is null
                || string.IsNullOrWhiteSpace(model.Driver)
                || string.IsNullOrWhiteSpace(model.Title)
                || string.IsNullOrWhiteSpace(model.Description))
            {
                TempData["AdminMessage"] = "Driver, Titel und Beschreibung sind Pflicht.";
                return RedirectToAction(nameof(Index));
            }

            var entity = model.Id > 0
                ? await _db.CustomAchievements.FirstOrDefaultAsync(x => x.Id == model.Id)
                : null;

            entity = new CustomAchievement { AwardedAt = DateTime.UtcNow };
            _db.CustomAchievements.Add(entity);

            entity.LeagueId = string.IsNullOrWhiteSpace(model.LeagueId) ? null : model.LeagueId.Trim();
            entity.Driver = model.Driver.Trim();
            entity.Title = model.Title.Trim();
            entity.Description = model.Description.Trim();
            entity.Icon = string.IsNullOrWhiteSpace(model.Icon) ? "bi-award-fill" : model.Icon.Trim();
            entity.Tone = string.IsNullOrWhiteSpace(model.Tone) ? "amber" : model.Tone.Trim();
            entity.Tier = string.IsNullOrWhiteSpace(model.Tier) ? "gold" : model.Tier.Trim();
            entity.Category = string.IsNullOrWhiteSpace(model.Category) ? "Spezial" : model.Category.Trim();

            await _db.SaveChangesAsync();
            await _audit.LogAsync(model.Id > 0 ? "UpdateAchievement" : "CreateAchievement",
                "CustomAchievement", entity.Id.ToString(),
                $"Driver={entity.Driver}, Title={entity.Title}, League={entity.LeagueId ?? "*"}");

            TempData["AdminMessage"] = "Achievement gespeichert.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin.Drivers.Achievements")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _db.CustomAchievements.FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null)
            {
                TempData["AdminMessage"] = "Achievement nicht gefunden.";
                return RedirectToAction(nameof(Index));
            }

            _db.CustomAchievements.Remove(entity);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("DeleteAchievement", "CustomAchievement", id.ToString(),
                $"Driver={entity.Driver}, Title={entity.Title}");

            TempData["AdminMessage"] = "Achievement gelöscht.";
            return RedirectToAction(nameof(Index));
        }

        // ---- Achievement Definitions ----
        [HttpGet]
        [Authorize(Policy = "Admin.Drivers.Definitions")]
        public async Task<IActionResult> Definitions(int? editId)
        {
            var items = await _db.AchievementDefinitions
                .OrderBy(d => d.SortOrder).ThenBy(d => d.Title)
                .ToListAsync();

            ViewBag.EditItem = editId.HasValue
                ? items.FirstOrDefault(x => x.Id == editId.Value)
                : null;

            return View("~/Views/Admin/AchievementDefinitions.cshtml", items);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin.Drivers.Definitions")]
        public async Task<IActionResult> SaveDefinition(AchievementDefinition model)
        {
            if (model is null
                || string.IsNullOrWhiteSpace(model.Key)
                || string.IsNullOrWhiteSpace(model.Title)
                || string.IsNullOrWhiteSpace(model.Description))
            {
                TempData["AdminMessage"] = "Key, Titel und Beschreibung sind Pflicht.";
                return RedirectToAction(nameof(Definitions));
            }

            var entity = model.Id > 0
                ? await _db.AchievementDefinitions.AsTracking().FirstOrDefaultAsync(x => x.Id == model.Id)
                : null;

            var keyNormalized = model.Key.Trim().ToLowerInvariant();

            var conflict = await _db.AchievementDefinitions
                .FirstOrDefaultAsync(x => x.Key == keyNormalized && x.Id != model.Id);
            if (conflict != null)
            {
                TempData["AdminMessage"] = $"Ein Achievement mit dem Key '{keyNormalized}' existiert bereits.";
                return RedirectToAction(nameof(Definitions), new { editId = model.Id > 0 ? model.Id : (int?)null });
            }

            if (entity is null)
            {
                entity = new AchievementDefinition { IsBuiltIn = false };
                _db.AchievementDefinitions.Add(entity);
            }

            entity.Key = keyNormalized;
            entity.Title = model.Title.Trim();
            entity.Description = model.Description.Trim();
            entity.Icon = string.IsNullOrWhiteSpace(model.Icon) ? "bi-award-fill" : model.Icon.Trim();
            entity.Tone = string.IsNullOrWhiteSpace(model.Tone) ? "amber" : model.Tone.Trim();
            entity.Tier = string.IsNullOrWhiteSpace(model.Tier) ? "gold" : model.Tier.Trim();
            entity.Category = string.IsNullOrWhiteSpace(model.Category) ? "Spezial" : model.Category.Trim();
            entity.Metric = model.Metric;
            entity.Target = Math.Max(1, model.Target);
            entity.IsActive = model.IsActive;
            entity.SortOrder = model.SortOrder;

            await _db.SaveChangesAsync();
            _staticCache.InvalidateAchievementDefinitions();
            await _audit.LogAsync(model.Id > 0 ? "UpdateAchievementDefinition" : "CreateAchievementDefinition",
                "AchievementDefinition", entity.Id.ToString(),
                $"Key={entity.Key}, Metric={entity.Metric}, Target={entity.Target}");

            TempData["AdminMessage"] = "Achievement-Definition gespeichert.";
            return RedirectToAction(nameof(Definitions));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin.Drivers.Definitions")]
        public async Task<IActionResult> DeleteDefinition(int id)
        {
            var entity = await _db.AchievementDefinitions.AsTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null)
            {
                TempData["AdminMessage"] = "Definition nicht gefunden.";
                return RedirectToAction(nameof(Definitions));
            }

            if (entity.IsBuiltIn)
            {
                entity.IsActive = false;
                await _db.SaveChangesAsync();
                _staticCache.InvalidateAchievementDefinitions();
                await _audit.LogAsync("DisableAchievementDefinition", "AchievementDefinition", id.ToString(),
                    $"Key={entity.Key}");
                TempData["AdminMessage"] = "Built-in Achievement wurde deaktiviert (nicht gelöscht).";
                return RedirectToAction(nameof(Definitions));
            }

            _db.AchievementDefinitions.Remove(entity);
            await _db.SaveChangesAsync();
            _staticCache.InvalidateAchievementDefinitions();
            await _audit.LogAsync("DeleteAchievementDefinition", "AchievementDefinition", id.ToString(),
                $"Key={entity.Key}");

            TempData["AdminMessage"] = "Achievement-Definition gelöscht.";
            return RedirectToAction(nameof(Definitions));
        }
    }
}
