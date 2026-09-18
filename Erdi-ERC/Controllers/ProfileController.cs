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
    public class ProfileController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IDriverProfileService _profiles;
        private readonly IAdminAuditService _audit;
        private readonly IStaticDataCache _staticCache;
        private readonly IMediaService _media;
        private readonly ILogger<ProfileController> _logger;
        private readonly ProfileHistoryService _history;

        public ProfileController(AppDbContext db, IDriverProfileService profiles, IAdminAuditService audit, IStaticDataCache staticCache, IMediaService media, ILogger<ProfileController> logger, ProfileHistoryService history)
        {
            _db = db;
            _profiles = profiles;
            _audit = audit;
            _staticCache = staticCache;
            _media = media;
            _logger = logger;
            _history = history;
        }

        [HttpGet("/Profile/Driver/{driverName}")]
        public async Task<IActionResult> ByDriverName(string driverName)
        {
            if (string.IsNullOrWhiteSpace(driverName)) return NotFound();

            var profile = await _profiles.FindByDriverNameAsync(driverName.Trim());
            if (profile is null) return NotFound();

            return RedirectToAction(nameof(Index), new { discordId = profile.DiscordId });
        }

        [HttpGet("/Profile/{discordId?}")]
        public async Task<IActionResult> Index(string? discordId)
        {
            discordId ??= User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(discordId)) return NotFound();

            var profile = await _profiles.GetByDiscordIdAsync(discordId);
            if (profile is null) return NotFound();

            // Aliase = alle Namen, unter denen der Fahrer in Standings/Finishes stehen kann
            // (GamerTags, DisplayName, DiscordName) — zentral im DriverAliasHelper, damit die
            // Liste nicht pro Aufrufer auseinanderläuft. Ohne den DiscordName hat ein Fahrer,
            // dessen Ergebnisse unter dem Discord-Namen eingetragen wurden, auf der Fahrerkarte
            // Stats, im Profil aber keine.
            var aliases = DriverAliasHelper.Build(profile);

            // Teure All-Ligen-Aggregation kommt gecacht aus dem ProfileHistoryService;
            // die View selbst bleibt live (user-spezifisches HTML).
            var detail = await _history.GetHistoryAsync(aliases)
                ?? new DriverDetailViewModel
                {
                    Driver = string.Empty,
                    Races = new List<DriverRaceEntry>()
                };
            detail.Driver = profile.DisplayName ?? profile.DiscordName;

            var custom = await _db.CustomAchievements
                .Where(c => aliases.Contains(c.Driver))
                .ToListAsync();
            // Achievement-Definitionen sind quasi-statisch (Admin pflegt sie selten) → aus Cache.
            var defs = await _staticCache.GetActiveAchievementDefinitionsAsync();
            detail.Achievements = DriverAchievementsHelper.Compute(detail, custom, defs.ToList()).ToList();

            ViewBag.Profile = profile;
            ViewBag.NumberColor = _profiles.ResolveDriverNumberColor(profile);
            var isOwnProfile = string.Equals(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, profile.DiscordId, StringComparison.Ordinal);
            ViewBag.CanEditEaName = isOwnProfile;

            ViewBag.ProfileWall = await _db.ProfileWallMessages
                .Where(x => x.ProfileDiscordId == profile.DiscordId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(20)
                .ToListAsync();
            return View(detail);
        }

        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("forms")]
        public async Task<IActionResult> UpdateEaName(string eaName, bool confirmEaNameAccuracy = false)
        {
            var discordId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(discordId))
                return Forbid();

            if (!confirmEaNameAccuracy)
            {
                TempData["ProfileMessage"] = "Bitte bestätige vor dem Speichern, dass dein Ingame-Name zu 100% korrekt ist.";
                return RedirectToAction(nameof(Index), new { discordId });
            }

            var normalized = eaName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                TempData["ProfileMessage"] = "Bitte einen gültigen Ingame-Namen eintragen.";
                return RedirectToAction(nameof(Index), new { discordId });
            }

            if (normalized.Length > 128)
            {
                TempData["ProfileMessage"] = "Der Ingame-Name darf maximal 128 Zeichen lang sein.";
                return RedirectToAction(nameof(Index), new { discordId });
            }

            var profile = await _db.DriverProfiles
                .AsNoTracking()
                .Include(p => p.GamerTags)
                .FirstOrDefaultAsync(p => p.DiscordId == discordId);
            if (profile is null)
                return NotFound();

            var platform = !string.IsNullOrWhiteSpace(profile.PreferredPlatform)
                ? profile.PreferredPlatform.Trim()
                : "EA";
            var previousName = profile.GamerTags
                .FirstOrDefault(t => string.Equals(t.Platform, platform, StringComparison.OrdinalIgnoreCase))
                ?.GamerTag?.Trim()
                ?? profile.DisplayName?.Trim()
                ?? string.Empty;

            if (string.Equals(previousName, normalized, StringComparison.Ordinal))
            {
                TempData["ProfileMessage"] = "Dein Ingame-Name ist bereits so eingetragen.";
                return RedirectToAction(nameof(Index), new { discordId });
            }

            int changedReferences;
            try
            {
                changedReferences = await _profiles.RenameIngameNameAsync(discordId, normalized, discordId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Speichern des Ingame-Namens für {DiscordId}", discordId);
                TempData["ProfileMessage"] = "Fehler beim Speichern. Bitte versuche es erneut.";
                return RedirectToAction(nameof(Index), new { discordId });
            }
            await _audit.LogAsync(
                "UpdateIngameName",
                "DriverProfile",
                discordId,
                $"Discord={profile.DiscordName}, Platform={platform}, OldName={previousName}, NewName={normalized}, ChangedRefs={changedReferences}");

            TempData["ProfileMessage"] = "Ingame-Name gespeichert. Bitte trage immer zu 100% deinen echten und korrekten Ingame-Namen ein.";
            return RedirectToAction(nameof(Index), new { discordId });
        }

        private static readonly System.Text.RegularExpressions.Regex HexColorRegex = new(
            @"^#[0-9A-Fa-f]{6}$",
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("forms")]
        public async Task<IActionResult> UpdateProfileMeta(string favoriteTrack, string? favoriteTeam, string inputDevice, string preferredPlatform, string nationality, string? bio, int? age = null, string? driverNumberColor = null)
        {
            var discordId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(discordId))
            {
                return Forbid();
            }

            var profile = await _db.DriverProfiles.AsTracking()
                .FirstOrDefaultAsync(p => p.DiscordId == discordId);
            if (profile is null)
            {
                return NotFound();
            }

            var trackKey = string.IsNullOrWhiteSpace(favoriteTrack) ? null : favoriteTrack.Trim().ToLowerInvariant();
            profile.FavoriteTrack = trackKey is not null && Erdi_ERC.Models.F1RaceCatalog.FindTrack(trackKey) is not null
                ? trackKey
                : null;
            var teamKey = string.IsNullOrWhiteSpace(favoriteTeam) ? null : favoriteTeam.Trim().ToLowerInvariant();
            profile.FavoriteTeam = teamKey is not null && Erdi_ERC.Helpers.F1TeamsHelper.Teams.Any(t => t.CssKey == teamKey)
                ? teamKey
                : null;
            profile.InputDevice = string.IsNullOrWhiteSpace(inputDevice) ? null : inputDevice.Trim()[..Math.Min(inputDevice.Trim().Length, 64)];
            profile.PreferredPlatform = string.IsNullOrWhiteSpace(preferredPlatform) ? null : preferredPlatform.Trim()[..Math.Min(preferredPlatform.Trim().Length, 64)];
            profile.Nationality = string.IsNullOrWhiteSpace(nationality) ? null : nationality.Trim()[..Math.Min(nationality.Trim().Length, 64)];
            profile.Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim()[..Math.Min(bio.Trim().Length, 512)];
            // Alter nur im plausiblen Bereich übernehmen, sonst löschen.
            profile.Age = age is >= 14 and <= 99 ? age : null;

            // Fahrernummer-Farbe: gültige #RRGGBB übernehmen, sonst leer lassen (Default greift).
            var color = driverNumberColor?.Trim() ?? string.Empty;
            profile.DriverNumberColor = HexColorRegex.IsMatch(color) ? color : null;

            profile.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Speichern der Profil-Infos für {DiscordId}", discordId);
                TempData["ProfileMessage"] = "Fehler beim Speichern. Bitte versuche es erneut.";
                return RedirectToAction(nameof(Index), new { discordId });
            }
            TempData["ProfileMessage"] = "Profil-Infos aktualisiert.";
            return RedirectToAction(nameof(Index), new { discordId });
        }

        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("forms")]
        public async Task<IActionResult> UploadProfilePhoto(IFormFile? photo)
        {
            var discordId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(discordId))
            {
                return Forbid();
            }

            if (photo is null || photo.Length == 0)
            {
                TempData["ProfileMessage"] = "Bitte ein Bild auswählen.";
                return RedirectToAction(nameof(Index), new { discordId });
            }

            var profile = await _db.DriverProfiles.AsTracking()
                .FirstOrDefaultAsync(p => p.DiscordId == discordId);
            if (profile is null)
            {
                return NotFound();
            }

            var url = await _media.SaveDriverPhotoAsync(photo, discordId);
            if (url is null)
            {
                TempData["ProfileMessage"] = "Ungültiges Bild (max. 10 MB; jpg, png, webp, gif oder avif).";
                return RedirectToAction(nameof(Index), new { discordId });
            }

            var previousPhoto = profile.PhotoUrl;
            profile.PhotoUrl = url;
            profile.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Speichern des Profilbilds für {DiscordId}", discordId);
                _media.TryDeleteDriverPhoto(url); // gerade geschriebene Datei wieder entfernen
                TempData["ProfileMessage"] = "Fehler beim Speichern. Bitte versuche es erneut.";
                return RedirectToAction(nameof(Index), new { discordId });
            }

            // Altes Bild erst nach erfolgreichem Speichern entfernen.
            if (!string.IsNullOrWhiteSpace(previousPhoto))
            {
                _media.TryDeleteDriverPhoto(previousPhoto);
            }

            await _audit.LogAsync("UploadProfilePhoto", "DriverProfile", discordId, $"Photo={url}");
            TempData["ProfileMessage"] = "Profilbild aktualisiert.";
            return RedirectToAction(nameof(Index), new { discordId });
        }

        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveProfilePhoto()
        {
            var discordId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(discordId))
            {
                return Forbid();
            }

            var profile = await _db.DriverProfiles.AsTracking()
                .FirstOrDefaultAsync(p => p.DiscordId == discordId);
            if (profile is null)
            {
                return NotFound();
            }

            var previousPhoto = profile.PhotoUrl;
            profile.PhotoUrl = null;
            profile.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(previousPhoto))
            {
                _media.TryDeleteDriverPhoto(previousPhoto);
            }

            TempData["ProfileMessage"] = "Profilbild entfernt.";
            return RedirectToAction(nameof(Index), new { discordId });
        }

        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("forms")]
        public async Task<IActionResult> AddWallMessage(string profileDiscordId, string message)
        {
            var authorDiscordId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var authorName = User.Identity?.Name ?? "Community User";
            if (string.IsNullOrWhiteSpace(authorDiscordId))
            {
                return Forbid();
            }

            var normalizedMessage = message?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(profileDiscordId) && !string.IsNullOrWhiteSpace(normalizedMessage))
            {
                _db.ProfileWallMessages.Add(new ProfileWallMessage
                {
                    ProfileDiscordId = profileDiscordId.Trim(),
                    AuthorDiscordId = authorDiscordId,
                    AuthorName = authorName,
                    Message = normalizedMessage.Length > 600 ? normalizedMessage[..600] : normalizedMessage,
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index), new { discordId = profileDiscordId });
        }

    }
}
