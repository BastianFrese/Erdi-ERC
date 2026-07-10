using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Options;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace <OWNER_HANDLE>_ERC.Controllers
{
    /// <summary>Öffentliche Track-Setups: Ansicht mit Tier-Zugriff, Sandbox, Kommentare, Likes, Exclusive-Consent.</summary>
    public class SetupsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ITrackSetupAccessPolicy _trackSetupAccessPolicy;
        private readonly DiscordSetupAccessOptions _setupAccessOptions;

        public SetupsController(
            AppDbContext db,
            ITrackSetupAccessPolicy trackSetupAccessPolicy,
            IOptions<DiscordSetupAccessOptions> setupAccessOptions)
        {
            _db = db;
            _trackSetupAccessPolicy = trackSetupAccessPolicy;
            _setupAccessOptions = setupAccessOptions.Value;
        }

        [HttpGet]
        public async Task<IActionResult> TrackSetups(string? track = null, string? gameYear = null)
        {
            var normalizedTrack = track?.Trim();
            var selectedTrack = string.IsNullOrWhiteSpace(normalizedTrack)
                ? null
                : normalizedTrack;

            var normalizedGameYear = gameYear?.Trim();
            var selectedGameYear = string.IsNullOrWhiteSpace(normalizedGameYear)
                ? null
                : normalizedGameYear;

            var tracks = await _db.TrackSetups
                .Select(x => x.Track)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            var availableGameYears = await _db.TrackSetups
                .Where(x => x.GameYear != null && x.GameYear != "")
                .Select(x => x.GameYear!)
                .Distinct()
                .OrderByDescending(x => x)
                .ToListAsync();

            // "Frisch eingetroffen": Setups der letzten Tage, gruppiert nach Strecke + Spieljahr.
            // Bewusst ungefiltert (über alle Strecken), damit die Übersicht immer vollständig ist.
            var newSinceUtc = DateTime.UtcNow.AddDays(-SetupNewsGroup.WindowDays);
            var newSetupGroups = await _db.TrackSetups
                .Where(x => x.UpdatedAt >= newSinceUtc)
                .GroupBy(x => new { x.Track, x.GameYear })
                .Select(g => new SetupNewsGroup
                {
                    Track = g.Key.Track,
                    GameYear = g.Key.GameYear,
                    NewCount = g.Count(x => x.CreatedAt >= newSinceUtc),
                    UpdatedCount = g.Count(x => x.CreatedAt < newSinceUtc),
                    LatestUtc = g.Max(x => x.UpdatedAt)
                })
                .OrderByDescending(x => x.LatestUtc)
                .ToListAsync();

            var setupTierClaim = User.FindFirst("erdi:setup-tier")?.Value;
            var setupRoleClaim = User.FindFirst("erdi:setup-role")?.Value;
            var communityGuildClaim = User.FindFirst("erdi:on-community-guild")?.Value;
            var joinedAtClaim = User.FindFirst("erdi:guild-joined-at")?.Value;
            var tenurePendingClaim = User.FindFirst("erdi:tenure-pending")?.Value;
            var currentTier = int.TryParse(setupTierClaim, out var parsedTier) ? parsedTier : 0;
            var isOnCommunityGuild = string.Equals(communityGuildClaim, "true", StringComparison.OrdinalIgnoreCase);
            var isTenurePending = string.Equals(tenurePendingClaim, "true", StringComparison.OrdinalIgnoreCase);
            DateTimeOffset? guildJoinedAt = DateTimeOffset.TryParse(joinedAtClaim, out var parsedJoinedAt) ? parsedJoinedAt : null;

            var query = _db.TrackSetups.AsQueryable();
            if (!string.IsNullOrWhiteSpace(selectedTrack))
            {
                query = query.Where(x => x.Track == selectedTrack);
            }
            if (!string.IsNullOrWhiteSpace(selectedGameYear))
            {
                query = query.Where(x => x.GameYear == selectedGameYear);
            }

            var setups = await query
                .OrderBy(x => x.Track)
                .ThenByDescending(x => x.RequiredAccessTier)
                .ThenByDescending(x => x.UpdatedAt)
                .ToListAsync();

            var currentDiscordIdForBlock = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var isBlocked = !string.IsNullOrWhiteSpace(currentDiscordIdForBlock)
                && await _db.SetupBlockedUsers.AnyAsync(x => x.DiscordId == currentDiscordIdForBlock);

            var visibleSetups = isBlocked
                ? new List<TrackSetup>()
                : setups.Where(x => _trackSetupAccessPolicy.CanView(x, currentTier, setupRoleClaim)).ToList();
            var visibleSetupIds = visibleSetups.Select(x => x.Id).ToList();
            var comments = await _db.SetupComments
                .Where(x => visibleSetupIds.Contains(x.TrackSetupId))
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
            var likes = await _db.SetupLikes
                .Where(x => visibleSetupIds.Contains(x.TrackSetupId))
                .ToListAsync();
            var currentDiscordId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

            ViewBag.SetupTracks = tracks;
            ViewBag.SelectedTrack = selectedTrack;
            ViewBag.SelectedGameYear = selectedGameYear;
            ViewBag.AvailableGameYears = availableGameYears;
            ViewBag.NewSetupGroups = newSetupGroups;
            ViewBag.NewSetupSinceUtc = newSinceUtc;
            ViewBag.SetupTier = currentTier;
            ViewBag.SetupRole = setupRoleClaim;
            ViewBag.IsOnCommunityGuild = isOnCommunityGuild;
            ViewBag.IsTenurePending = isTenurePending;
            ViewBag.GuildJoinedAt = guildJoinedAt;
            ViewBag.TenureRequiredDays = _setupAccessOptions.MinGuildTenureDays;
            ViewBag.VisibleSetups = visibleSetups;
            ViewBag.HiddenSetups = isBlocked
                ? setups
                : setups.Where(x => !_trackSetupAccessPolicy.CanView(x, currentTier, setupRoleClaim)).ToList();
            ViewBag.SetupEditorConfig = SetupGameSpec.GetEditorConfig();
            ViewBag.SetupMetricConfig = SetupGameSpec.GetMetricConfig();
            ViewBag.SetupComments = comments
                .GroupBy(x => x.TrackSetupId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAt).ToList());
            ViewBag.SetupLikeCounts = likes
                .GroupBy(x => x.TrackSetupId)
                .ToDictionary(g => g.Key, g => g.Count());
            ViewBag.LikedSetupIds = likes
                .Where(x => x.DiscordId == currentDiscordId)
                .Select(x => x.TrackSetupId)
                .ToHashSet();

            // Determine if current user has accepted exclusive setup terms
            bool userHasAcceptedExclusiveTerms = false;
            if (!string.IsNullOrWhiteSpace(currentDiscordId))
            {
                var profile = await _db.DriverProfiles.FindAsync(currentDiscordId);
                userHasAcceptedExclusiveTerms = profile?.HasAcceptedExclusiveSetupTerms ?? false;
            }
            ViewBag.UserHasAcceptedExclusiveSetupTerms = userHasAcceptedExclusiveTerms;

            return View();
        }

        /// <summary>
        /// Sandbox-Modus: Liefert ein Setup-Objekt als JSON zurück, damit der Client
        /// lokal (im Browser/Session) damit experimentieren kann – ohne DB-Persistenz.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SetupSandbox(int id)
        {
            var setup = await _db.TrackSetups.FindAsync(id);
            if (setup == null) return NotFound();

            // Tier aus Claims (gleiche Logik wie TrackSetups-Action)
            var sbTierClaim = User.FindFirst("erdi:setup-tier")?.Value;
            var sbRoleClaim = User.FindFirst("erdi:setup-role")?.Value;
            var sbTier = int.TryParse(sbTierClaim, out var parsedSbTier) ? parsedSbTier : 0;
            var sbDiscordId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var sbIsBlocked = !string.IsNullOrWhiteSpace(sbDiscordId)
                && await _db.SetupBlockedUsers.AnyAsync(x => x.DiscordId == sbDiscordId);
            if (sbIsBlocked || !_trackSetupAccessPolicy.CanView(setup, sbTier, sbRoleClaim))
                return Forbid();

            return View(setup);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptExclusiveSetupTerms(int setupId, bool accept = false)
        {
            var discordId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var discordName = User.Identity?.Name ?? "Community User";
            if (string.IsNullOrWhiteSpace(discordId)) return Forbid();

            if (!accept)
            {
                // User cancelled or did not check the box
                return RedirectToAction(nameof(TrackSetups));
            }

            var profile = await _db.DriverProfiles.FindAsync(discordId);
            if (profile is null)
            {
                profile = new <OWNER_HANDLE>_ERC.Models.DriverProfile
                {
                    DiscordId = discordId,
                    DiscordName = discordName,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    HasAcceptedExclusiveSetupTerms = true,
                    ExclusiveSetupTermsAcceptedAt = DateTime.UtcNow
                };
                _db.DriverProfiles.Add(profile);
            }
            else
            {
                profile.HasAcceptedExclusiveSetupTerms = true;
                profile.ExclusiveSetupTermsAcceptedAt = DateTime.UtcNow;
                profile.UpdatedAt = DateTime.UtcNow;
                _db.DriverProfiles.Update(profile);
            }

            await _db.SaveChangesAsync();

            // Redirect back to TrackSetups so the setups are visible
            return RedirectToAction(nameof(TrackSetups));
        }

        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSetupComment(int setupId, string message, string? track = null, string? gameYear = null)
        {
            var discordId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var discordName = User.Identity?.Name ?? "Community User";
            if (string.IsNullOrWhiteSpace(discordId)) return Forbid();

            var normalized = message?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                _db.SetupComments.Add(new SetupComment
                {
                    TrackSetupId = setupId,
                    AuthorDiscordId = discordId,
                    AuthorName = discordName,
                    Message = normalized.Length > 600 ? normalized[..600] : normalized,
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }

            // Fragment ***REMOVED***setup-{id}: nach dem Post zur betroffenen Karte springen statt an den Seitenanfang.
            return RedirectToAction(nameof(TrackSetups), "Setups", new { track, gameYear }, $"setup-{setupId}");
        }

        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSetupLike(int setupId, string? track = null, string? gameYear = null)
        {
            var discordId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var discordName = User.Identity?.Name ?? "Community User";
            if (string.IsNullOrWhiteSpace(discordId)) return Forbid();

            var existing = await _db.SetupLikes.FirstOrDefaultAsync(x => x.TrackSetupId == setupId && x.DiscordId == discordId);
            if (existing is null)
            {
                _db.SetupLikes.Add(new SetupLike
                {
                    TrackSetupId = setupId,
                    DiscordId = discordId,
                    DiscordName = discordName,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                _db.SetupLikes.Remove(existing);
            }

            await _db.SaveChangesAsync();
            // Fragment ***REMOVED***setup-{id}: nach dem Post zur betroffenen Karte springen statt an den Seitenanfang.
            return RedirectToAction(nameof(TrackSetups), "Setups", new { track, gameYear }, $"setup-{setupId}");
        }
    }
}
