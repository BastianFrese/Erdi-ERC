using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Helpers;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace <OWNER_HANDLE>_ERC.Controllers
{
    public class ProfileController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IDriverProfileService _profiles;
        private readonly IAdminAuditService _audit;
        private readonly IStaticDataCache _staticCache;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(AppDbContext db, IDriverProfileService profiles, IAdminAuditService audit, IStaticDataCache staticCache, ILogger<ProfileController> logger)
        {
            _db = db;
            _profiles = profiles;
            _audit = audit;
            _staticCache = staticCache;
            _logger = logger;
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

            var aliases = profile.GamerTags.Select(t => t.GamerTag.Trim()).Where(s => s.Length > 0).ToList();
            if (!string.IsNullOrWhiteSpace(profile.DisplayName)) aliases.Add(profile.DisplayName.Trim());
            var aliasSet = new HashSet<string>(aliases, StringComparer.OrdinalIgnoreCase);

            var leagues = await _db.Leagues
                .Include(l => l.Standings)
                .Include(l => l.Races).ThenInclude(r => r.Finishes)
                .Include(l => l.Races).ThenInclude(r => r.ReserveAssignments)
                .ToListAsync();

            var races = new List<DriverRaceEntry>();
            int wins = 0, podiums = 0, fastest = 0, totalPoints = 0;
            string? team = null;

            foreach (var l in leagues)
            {
                var standing = l.Standings.FirstOrDefault(s => aliasSet.Contains(s.Driver?.Trim() ?? ""));
                if (standing is not null)
                {
                    totalPoints += standing.Points;
                    team ??= standing.Team;
                }

                foreach (var r in l.Races)
                {
                    var finish = r.Finishes.FirstOrDefault(f => aliasSet.Contains(f.Driver?.Trim() ?? ""));
                    if (finish is null) continue;

                    if (finish.Position == 1) wins++;
                    if (finish.Position is >= 1 and <= 3) podiums++;
                    if (finish.FastestLap) fastest++;

                    var reserveFor = r.ReserveAssignments.FirstOrDefault(a => aliasSet.Contains(a.ReserveDriver?.Trim() ?? ""))?.MainDriver;

                    races.Add(new DriverRaceEntry
                    {
                        RaceId = r.RowId,
                        LeagueId = l.Id,
                        Date = r.Date,
                        Track = r.Track,
                        Position = finish.Position,
                        Points = 0,
                        FastestLap = finish.FastestLap,
                        RaceTimeMs = finish.RaceTimeMs,
                        Team = standing?.Team ?? string.Empty,
                        WasReserve = !string.IsNullOrWhiteSpace(reserveFor),
                        ReserveForDriver = reserveFor
                    });
                }
            }

            var detail = new DriverDetailViewModel
            {
                Driver = profile.DisplayName ?? profile.DiscordName,
                Team = team ?? string.Empty,
                TotalPoints = totalPoints,
                Wins = wins,
                Podiums = podiums,
                FastestLaps = fastest,
                BestFinish = races.Where(r => r.Position > 0).Select(r => (int?)r.Position).DefaultIfEmpty(null).Min(),
                AverageFinish = races.Where(r => r.Position > 0).Select(r => (double)r.Position).DefaultIfEmpty().Average(),
                Races = races.OrderByDescending(r => r.Date).ToList()
            };

            var custom = await _db.CustomAchievements
                .Where(c => aliases.Contains(c.Driver))
                .ToListAsync();
            // Achievement-Definitionen sind quasi-statisch (Admin pflegt sie selten) → aus Cache.
            var defs = await _staticCache.GetActiveAchievementDefinitionsAsync();
            detail.Achievements = DriverAchievementsHelper.Compute(detail, custom, defs.ToList()).ToList();

            ViewBag.Profile = profile;
            ViewBag.CanEditEaName = string.Equals(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, profile.DiscordId, StringComparison.Ordinal);
            ViewBag.ProfileWall = await _db.ProfileWallMessages
                .Where(x => x.ProfileDiscordId == profile.DiscordId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(20)
                .ToListAsync();
            return View(detail);
        }

        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEaName(string eaName, bool confirmEaNameAccuracy = false)
        {
            var discordId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(discordId))
            {
                return Forbid();
            }

            if (!confirmEaNameAccuracy)
            {
                TempData["ProfileMessage"] = "Bitte bestätige vor dem Speichern, dass dein EA-Name zu 100% korrekt und ehrlich eingetragen ist.";
                return RedirectToAction(nameof(Index), new { discordId });
            }

            var normalizedEaName = eaName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedEaName))
            {
                TempData["ProfileMessage"] = "Bitte einen gültigen EA-Namen eintragen.";
                return RedirectToAction(nameof(Index), new { discordId });
            }

            if (normalizedEaName.Length > 128)
            {
                TempData["ProfileMessage"] = "Der EA-Name darf maximal 128 Zeichen lang sein.";
                return RedirectToAction(nameof(Index), new { discordId });
            }

            var profile = await _db.DriverProfiles
                .AsTracking()
                .Include(p => p.GamerTags)
                .FirstOrDefaultAsync(p => p.DiscordId == discordId);
            if (profile is null)
            {
                return NotFound();
            }

            var existingEaTag = profile.GamerTags
                .FirstOrDefault(t => string.Equals(t.Platform, "EA", StringComparison.OrdinalIgnoreCase));
            var previousEaName = existingEaTag?.GamerTag?.Trim() ?? string.Empty;

            if (string.Equals(previousEaName, normalizedEaName, StringComparison.Ordinal))
            {
                TempData["ProfileMessage"] = "Dein EA-Name ist bereits so eingetragen.";
                return RedirectToAction(nameof(Index), new { discordId });
            }

            if (existingEaTag is null)
            {
                profile.GamerTags.Add(new DriverGamerTag
                {
                    DiscordId = discordId,
                    Platform = "EA",
                    GamerTag = normalizedEaName,
                    IsPrimary = profile.GamerTags.Count == 0,
                    LinkedAt = DateTime.UtcNow,
                    LinkedByDiscordId = discordId
                });
            }
            else
            {
                existingEaTag.GamerTag = normalizedEaName;
                existingEaTag.LinkedAt = DateTime.UtcNow;
                existingEaTag.LinkedByDiscordId = discordId;
            }

            if (string.IsNullOrWhiteSpace(profile.DisplayName)
                || string.Equals(profile.DisplayName.Trim(), previousEaName, StringComparison.OrdinalIgnoreCase))
            {
                profile.DisplayName = normalizedEaName;
            }

            if (string.IsNullOrWhiteSpace(profile.PreferredPlatform))
            {
                profile.PreferredPlatform = "EA";
            }

            profile.UpdatedAt = DateTime.UtcNow;

            var changedReferences = 0;
            if (!string.IsNullOrWhiteSpace(previousEaName))
            {
                changedReferences = await RenameDriverReferencesAsync(profile, previousEaName, normalizedEaName);
            }

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Speichern des EA-Namens für {DiscordId}", discordId);
                TempData["ProfileMessage"] = "Fehler beim Speichern. Bitte versuche es erneut.";
                return RedirectToAction(nameof(Index), new { discordId });
            }
            await _audit.LogAsync(
                "UpdateEaName",
                "DriverProfile",
                discordId,
                $"Discord={profile.DiscordName}, OldEaName={previousEaName}, NewEaName={normalizedEaName}, ChangedReferences={changedReferences}");

            TempData["ProfileMessage"] = "EA-Name gespeichert. Bitte trage immer zu 100% deinen echten und korrekten EA-Namen ein.";
            return RedirectToAction(nameof(Index), new { discordId });
        }

        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfileMeta(string favoriteTrack, string inputDevice, string preferredPlatform, string nationality, string? bio)
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

            profile.FavoriteTrack = string.IsNullOrWhiteSpace(favoriteTrack) ? null : favoriteTrack.Trim()[..Math.Min(favoriteTrack.Trim().Length, 128)];
            profile.InputDevice = string.IsNullOrWhiteSpace(inputDevice) ? null : inputDevice.Trim()[..Math.Min(inputDevice.Trim().Length, 64)];
            profile.PreferredPlatform = string.IsNullOrWhiteSpace(preferredPlatform) ? null : preferredPlatform.Trim()[..Math.Min(preferredPlatform.Trim().Length, 64)];
            profile.Nationality = string.IsNullOrWhiteSpace(nationality) ? null : nationality.Trim()[..Math.Min(nationality.Trim().Length, 64)];
            profile.Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim()[..Math.Min(bio.Trim().Length, 512)];
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

        private async Task<int> RenameDriverReferencesAsync(DriverProfile profile, string oldName, string newName)
        {
            var changed = 0;

            var standings = await _db.DriverStandings.AsTracking()
                .Where(x => x.Driver == oldName || x.ReserveForDriver == oldName)
                .ToListAsync();
            foreach (var standing in standings)
            {
                if (string.Equals(standing.Driver, oldName, StringComparison.Ordinal))
                {
                    standing.Driver = newName;
                    changed++;
                }
                if (string.Equals(standing.ReserveForDriver, oldName, StringComparison.Ordinal))
                {
                    standing.ReserveForDriver = newName;
                    changed++;
                }
            }

            var raceResults = await _db.RaceResults.AsTracking()
                .Where(x => x.Winner == oldName || x.FastestLap == oldName)
                .ToListAsync();
            foreach (var race in raceResults)
            {
                if (string.Equals(race.Winner, oldName, StringComparison.Ordinal))
                {
                    race.Winner = newName;
                    changed++;
                }
                if (string.Equals(race.FastestLap, oldName, StringComparison.Ordinal))
                {
                    race.FastestLap = newName;
                    changed++;
                }
            }

            var finishes = await _db.RaceFinishes.AsTracking()
                .Where(x => x.Driver == oldName)
                .ToListAsync();
            foreach (var finish in finishes)
            {
                finish.Driver = newName;
                changed++;
            }

            var reserveAssignments = await _db.RaceReserveAssignments.AsTracking()
                .Where(x => x.ReserveDriver == oldName || x.MainDriver == oldName)
                .ToListAsync();
            foreach (var assignment in reserveAssignments)
            {
                if (string.Equals(assignment.ReserveDriver, oldName, StringComparison.Ordinal))
                {
                    assignment.ReserveDriver = newName;
                    changed++;
                }
                if (string.Equals(assignment.MainDriver, oldName, StringComparison.Ordinal))
                {
                    assignment.MainDriver = newName;
                    changed++;
                }
            }

            var penalties = await _db.LeaguePenalties.AsTracking()
                .Where(x => x.Driver == oldName)
                .ToListAsync();
            foreach (var penalty in penalties)
            {
                penalty.Driver = newName;
                changed++;
            }

            var customAchievements = await _db.CustomAchievements.AsTracking()
                .Where(x => x.Driver == oldName)
                .ToListAsync();
            foreach (var achievement in customAchievements)
            {
                achievement.Driver = newName;
                changed++;
            }

            var applications = await _db.ApplicationForms.AsTracking()
                .Where(x => x.GamingName == oldName && x.Platform == "EA"
                    && ((x.DiscordId != null && x.DiscordId == profile.DiscordId) || x.DiscordName == profile.DiscordName))
                .ToListAsync();
            foreach (var application in applications)
            {
                application.GamingName = newName;
                changed++;
            }

            return changed;
        }
    }
}
