using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace <OWNER_HANDLE>_ERC.Controllers
{
    [Authorize(Policy = "Admin.System.AboutMe")]
    public class AdminAboutController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IAdminAuditService _audit;
        private readonly IMediaService _media;

        public AdminAboutController(AppDbContext db, IAdminAuditService audit, IMediaService media)
        {
            _db    = db;
            _audit = audit;
            _media = media;
        }

        // ── Index ────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Index()
        {
            var profiles = await _db.AboutMeProfiles
                .OrderBy(p => p.SortOrder).ThenBy(p => p.DisplayName)
                .ToListAsync();
            return View("~/Views/Admin/About/Index.cshtml", profiles);
        }

        // ── Create GET ───────────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult Create() =>
            View("~/Views/Admin/About/Edit.cshtml", new AboutMeProfile());

        // ── Edit GET ─────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var profile = await _db.AboutMeProfiles.FindAsync(id);
            if (profile == null) return NotFound();
            return View("~/Views/Admin/About/Edit.cshtml", profile);
        }

        // ── Save (Create + Edit) POST ────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(AboutMeProfile model, IFormFile? avatarFile, IFormFile? backgroundFile)
        {
            // File uploads take priority (only uploads allowed; URL fields removed in UI)
            if (avatarFile is { Length: > 0 })
            {
                var url = await _media.SaveAboutImageAsync(avatarFile, "avatar");
                if (url != null) model.AvatarUrl = url;
                else ModelState.AddModelError("AvatarUrl", "Ungültiges Bild (max. 10 MB, jpg/png/webp).");
            }
            if (backgroundFile is { Length: > 0 })
            {
                var url = await _media.SaveAboutImageAsync(backgroundFile, "background");
                if (url != null) model.BackgroundUrl = url;
                else ModelState.AddModelError("BackgroundUrl", "Ungültiges Bild (max. 10 MB, jpg/png/webp).");
            }
            // Slug normalisieren
            model.Slug = model.Slug.Trim().ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue")
                .Replace("ß", "ss");

            // Slug-Eindeutigkeit prüfen
            var slugExists = await _db.AboutMeProfiles
                .AnyAsync(p => p.Slug == model.Slug && p.Id != model.Id);
            if (slugExists)
                ModelState.AddModelError("Slug", "Dieser Slug wird bereits verwendet.");

            if (!ModelState.IsValid)
                return View("~/Views/Admin/About/Edit.cshtml", model);

            if (model.Id == 0)
            {
                model.CreatedAt     = DateTime.UtcNow;
                model.UpdatedAt     = DateTime.UtcNow;
                model.LastEditedBy  = User.Identity?.Name;
                _db.AboutMeProfiles.Add(model);
                await _db.SaveChangesAsync();
                await _audit.LogAsync("Über-mich erstellt", "AboutMeProfile", model.Id.ToString(), model.DisplayName);
                TempData["AboutSuccess"] = $"Profil \"{model.DisplayName}\" erstellt.";
            }
            else
            {
                var existing = await _db.AboutMeProfiles.FindAsync(model.Id);
                if (existing == null) return NotFound();

                existing.Slug            = model.Slug;
                existing.DisplayName     = model.DisplayName;
                existing.Tagline         = model.Tagline;
                // Only update AvatarUrl/BackgroundUrl if a file upload provided; do not accept URL fields anymore
                if (!string.IsNullOrWhiteSpace(model.AvatarUrl)) existing.AvatarUrl = model.AvatarUrl;
                if (!string.IsNullOrWhiteSpace(model.BackgroundUrl)) existing.BackgroundUrl = model.BackgroundUrl;
                existing.Bio             = model.Bio;
                existing.ShortBio        = model.ShortBio;
                existing.DiscordUsername = model.DiscordUsername;
                existing.TwitchUrl       = model.TwitchUrl;
                existing.YouTubeUrl      = model.YouTubeUrl;
                existing.InstagramUrl    = model.InstagramUrl;
                existing.TwitterUrl      = model.TwitterUrl;
                existing.SteamUrl        = model.SteamUrl;
                existing.FavoriteTrack   = model.FavoriteTrack;
                existing.FavoriteCar     = model.FavoriteCar;
                existing.RacingNumber    = model.RacingNumber;
                existing.AccentColor     = model.AccentColor;
                existing.IsPublic        = model.IsPublic;
                existing.SortOrder       = model.SortOrder;
                existing.UpdatedAt       = DateTime.UtcNow;
                existing.LastEditedBy    = User.Identity?.Name;

                await _db.SaveChangesAsync();
                await _audit.LogAsync("Über-mich bearbeitet", "AboutMeProfile", existing.Id.ToString(), existing.DisplayName);
                TempData["AboutSuccess"] = $"Profil \"{existing.DisplayName}\" gespeichert.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ── Delete ───────────────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var profile = await _db.AboutMeProfiles.FindAsync(id);
            if (profile != null)
            {
                _db.AboutMeProfiles.Remove(profile);
                await _db.SaveChangesAsync();
                await _audit.LogAsync("Über-mich gelöscht", "AboutMeProfile", id.ToString(), profile.DisplayName);
                TempData["AboutSuccess"] = $"Profil \"{profile.DisplayName}\" gelöscht.";
            }
            return RedirectToAction(nameof(Index));
        }

        // ── Toggle Visibility ────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePublic(int id)
        {
            var profile = await _db.AboutMeProfiles.FindAsync(id);
            if (profile != null)
            {
                profile.IsPublic   = !profile.IsPublic;
                profile.UpdatedAt  = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
