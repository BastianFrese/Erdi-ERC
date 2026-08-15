using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erdi_ERC.Controllers
{
    /// <summary>Track-Setups-Verwaltung: Editor, Access-Tiers/Role-Mappings, manuelle Zugänge, Sperren.</summary>
    [Authorize(Policy = "Admin")]
    public class AdminSetupsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IAdminAuditService _audit;
        private readonly IConfiguration _config;

        public AdminSetupsController(AppDbContext db, IAdminAuditService audit, IConfiguration config)
        {
            _db = db;
            _audit = audit;
            _config = config;
        }

        [HttpGet]
        [Authorize(Policy = "Admin.System.Setups")]
        public async Task<IActionResult> TrackSetups()
        {
            var setups = await _db.TrackSetups
                .OrderBy(x => x.Track)
                .ThenByDescending(x => x.CreatedAt)
                .ToListAsync();

            ViewBag.RoleMappings = await _db.SetupAccessRoleMappings.ToListAsync();

            var setupOptions = _config.GetSection("DiscordSetupAccess");
            ViewBag.SetupAccessGuildId = setupOptions["GuildId"];
            ViewBag.SetupAccessGuildConfigured = !string.IsNullOrWhiteSpace(setupOptions["GuildId"]);
            ViewBag.SetupAccessMappingsReady = true;
            ViewBag.SetupAccessMappingsCount = await _db.SetupAccessRoleMappings.CountAsync();
            ViewBag.SetupMetricConfig = Erdi_ERC.Models.SetupGameSpec.GetMetricConfig();
            ViewBag.F1Tracks = Erdi_ERC.Models.F1RaceCatalog.Tracks;
            ViewBag.F1RaceLengths = Erdi_ERC.Models.F1RaceCatalog.Lengths;

            // Current game year setting for admin editor (short form, e.g. "26")
            var appOpts = _config.GetSection("Application");
            ViewBag.F1GameYear = appOpts["F1GameYearShort"] ?? "26";

            // Profiles with manual setup tier grant (for the "Zugänge verschenken" panel)
            ViewBag.ManualSetupGrants = await _db.DriverProfiles
                .Where(p => p.ManualSetupTier != null)
                .OrderBy(p => p.DiscordName)
                .ToListAsync();

            ViewBag.SetupBlockedUsers = await _db.SetupBlockedUsers
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return View("~/Views/Admin/TrackSetups.cshtml", setups);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTrackSetup(int? id, string track, string title, int requiredAccessTier, string? requiredRoleLabel, string? setupInfo, string setupText, string? strategy, string? gameYear)
        {
            if (string.IsNullOrWhiteSpace(track) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(setupText))
            {
                TempData["AdminMessage"] = "Strecke, Titel und Setup-Daten sind Pflicht.";
                return RedirectToAction(nameof(TrackSetups));
            }

            // Debug: prefer explicit form value if provided (some clients may not bind the parameter)
            string? formGameYear = null;
            try
            {
                if (Request?.Form != null && Request.Form.ContainsKey("gameYear"))
                {
                    formGameYear = Request.Form["gameYear"].ToString();
                }
            }
            catch { formGameYear = null; }

            TrackSetup? entity = id.HasValue && id.Value > 0
                ? await _db.TrackSetups.FindAsync(id.Value)
                : null;

            if (entity is null)
            {
                entity = new TrackSetup { CreatedAt = DateTime.UtcNow };
                _db.TrackSetups.Add(entity);
            }

            entity.Track = track.Trim();
            entity.Title = title.Trim();
            // Prefer explicit form value if present, otherwise use bound parameter
            var chosenYear = !string.IsNullOrWhiteSpace(formGameYear) ? formGameYear : gameYear;
            entity.GameYear = string.IsNullOrWhiteSpace(chosenYear) ? null : chosenYear.Trim();
            entity.RequiredAccessTier = requiredAccessTier;
            entity.RequiredRoleLabel = string.IsNullOrWhiteSpace(requiredRoleLabel) ? null : requiredRoleLabel.Trim();
            entity.SetupInfo = string.IsNullOrWhiteSpace(setupInfo) ? null : setupInfo.Trim();
            entity.SetupText = setupText.Trim();
            entity.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await _audit.LogAsync("SaveTrackSetup", "TrackSetup", entity.Id.ToString(), $"Track={entity.Track}, Tier={entity.RequiredAccessTier}, GameYear={entity.GameYear}");

            TempData["AdminMessage"] = $"Setup gespeichert. Spieljahr: {(entity.GameYear ?? "(leer)")}";
            return RedirectToAction(nameof(TrackSetups));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTrackSetup(int id)
        {
            var setup = await _db.TrackSetups.FindAsync(id);
            if (setup is not null)
            {
                _db.TrackSetups.Remove(setup);
                await _db.SaveChangesAsync();
                await _audit.LogAsync("DeleteTrackSetup", "TrackSetup", id.ToString(), $"Track={setup.Track}");
                TempData["AdminMessage"] = "Setup gelöscht.";
            }

            return RedirectToAction(nameof(TrackSetups));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSetupAccessRoleMapping(int? id, int tier, string roleId, string? label)
        {
            if (string.IsNullOrWhiteSpace(roleId))
            {
                TempData["AdminMessage"] = "Role ID ist erforderlich.";
                return RedirectToAction(nameof(TrackSetups));
            }

            if (tier < 3 || tier > 5)
            {
                TempData["AdminMessage"] = "Tier muss zwischen 3 und 5 liegen (Twitch-Sub-Tiers).";
                return RedirectToAction(nameof(TrackSetups));
            }

            SetupAccessRoleMapping? entity = id.HasValue && id.Value > 0
                ? await _db.SetupAccessRoleMappings.FindAsync(id.Value)
                : null;

            if (entity is null)
            {
                entity = new SetupAccessRoleMapping { CreatedAt = DateTime.UtcNow };
                _db.SetupAccessRoleMappings.Add(entity);
            }

            entity.Tier = tier;
            entity.RoleId = roleId.Trim();
            entity.Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();

            await _db.SaveChangesAsync();
            await _audit.LogAsync("SaveSetupAccessRoleMapping", "SetupAccessRoleMapping", entity.Id.ToString(), $"Tier={tier}, RoleId={roleId}");
            TempData["AdminMessage"] = "Role-Mapping gespeichert.";
            return RedirectToAction(nameof(TrackSetups));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSetupAccessRoleMapping(int id)
        {
            var mapping = await _db.SetupAccessRoleMappings.FindAsync(id);
            if (mapping is not null)
            {
                _db.SetupAccessRoleMappings.Remove(mapping);
                await _db.SaveChangesAsync();
                await _audit.LogAsync("DeleteSetupAccessRoleMapping", "SetupAccessRoleMapping", id.ToString(), $"Tier={mapping.Tier}");
                TempData["AdminMessage"] = "Mapping gelöscht.";
            }

            return RedirectToAction(nameof(TrackSetups));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GrantSetupAccess(string discordId, int tier)
        {
            if (string.IsNullOrWhiteSpace(discordId) || tier < 1 || tier > 5)
            {
                TempData["AdminMessage"] = "Ungültige Eingabe für Setup-Zugang.";
                return RedirectToAction(nameof(TrackSetups));
            }

            var profile = await _db.DriverProfiles.AsTracking().FirstOrDefaultAsync(p => p.DiscordId == discordId);
            if (profile is null)
            {
                TempData["AdminMessage"] = $"Kein Profil für Discord-ID {discordId} gefunden.";
                return RedirectToAction(nameof(TrackSetups));
            }

            profile.ManualSetupTier = tier;
            profile.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("GrantSetupAccess", "DriverProfile", discordId, $"ManualTier={tier}, User={profile.DiscordName}");
            TempData["AdminMessage"] = $"Setup-Zugang Tier {tier} an {profile.DiscordName} vergeben. Der User muss sich neu einloggen.";
            return RedirectToAction(nameof(TrackSetups));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeSetupAccess(string discordId)
        {
            var profile = await _db.DriverProfiles.AsTracking().FirstOrDefaultAsync(p => p.DiscordId == discordId);
            if (profile is null)
            {
                TempData["AdminMessage"] = $"Kein Profil für Discord-ID {discordId} gefunden.";
                return RedirectToAction(nameof(TrackSetups));
            }

            profile.ManualSetupTier = null;
            profile.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("RevokeSetupAccess", "DriverProfile", discordId, $"ManualTier entfernt, User={profile.DiscordName}");
            TempData["AdminMessage"] = $"Manueller Setup-Zugang von {profile.DiscordName} entzogen. Der User muss sich neu einloggen.";
            return RedirectToAction(nameof(TrackSetups));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> BlockSetupUser(string discordId, string? reason)
        {
            discordId = discordId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(discordId))
            {
                TempData["AdminMessage"] = "Discord-ID darf nicht leer sein.";
                return RedirectToAction(nameof(TrackSetups));
            }

            var existing = await _db.SetupBlockedUsers.FindAsync(discordId);
            if (existing != null)
            {
                TempData["AdminMessage"] = $"{discordId} ist bereits gesperrt.";
                return RedirectToAction(nameof(TrackSetups));
            }

            _db.SetupBlockedUsers.Add(new SetupBlockedUser
            {
                DiscordId = discordId,
                Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            await _audit.LogAsync("BlockSetupUser", "SetupBlockedUser", discordId, $"Reason={reason}");
            TempData["AdminMessage"] = $"Discord-ID {discordId} wird von Setups ausgeschlossen.";
            return RedirectToAction(nameof(TrackSetups));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UnblockSetupUser(string discordId)
        {
            var entry = await _db.SetupBlockedUsers.FindAsync(discordId?.Trim() ?? string.Empty);
            if (entry != null)
            {
                _db.SetupBlockedUsers.Remove(entry);
                await _db.SaveChangesAsync();
                await _audit.LogAsync("UnblockSetupUser", "SetupBlockedUser", discordId!, "Sperre aufgehoben");
            }
            TempData["AdminMessage"] = $"Sperre für {discordId} aufgehoben.";
            return RedirectToAction(nameof(TrackSetups));
        }

        [HttpGet]
        [HttpPost]
        public IActionResult TrackSetupStrategy(string? trackKey, string? lengthKey, string? strategyPlan)
        {
            var track  = F1RaceCatalog.Tracks.FirstOrDefault(t => t.Key == trackKey);
            var length = F1RaceCatalog.Lengths.FirstOrDefault(l => l.Key == lengthKey);

            if (track == null || length == null)
                return Json(new { error = "Strecke oder Renn-Länge nicht gefunden." });

            var plan = F1RaceCatalog.BuildPlan(track, length);
            var text = string.IsNullOrWhiteSpace(strategyPlan) ? plan.ToHumanReadable() : strategyPlan.Trim();

            return Json(new
            {
                text,
                trackName   = plan.TrackName,
                lengthLabel = plan.LengthLabel,
                totalLaps   = plan.TotalLaps,
                stops       = plan.Stops
            });
        }
    }
}
