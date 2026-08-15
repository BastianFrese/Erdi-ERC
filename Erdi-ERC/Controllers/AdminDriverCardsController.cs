using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Erdi_ERC.Controllers
{
    [Authorize(Policy = "Admin")]
    public class AdminDriverCardsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IAdminAuditService _audit;
        private readonly IDriverProfileService _profiles;

        public AdminDriverCardsController(AppDbContext db, IAdminAuditService audit, IDriverProfileService profiles)
        {
            _db = db;
            _audit = audit;
            _profiles = profiles;
        }

        [HttpGet("/admin/driver-cards")]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Fahrerkarten";
            ViewData["Kicker"] = "Fahrer";
            ViewData["HeroTitle"] = "Fahrerkarten";

            var leagues = await _db.Leagues
                .AsNoTracking()
                .Include(l => l.Standings)
                .Include(l => l.Races).ThenInclude(r => r.Finishes)
                .ToListAsync();

            var profiles = await _db.DriverProfiles
                .AsNoTracking()
                .Include(p => p.GamerTags)
                .ToListAsync();

            // gamer-tag/display-name -> profile lookup
            var tagToProfile = new Dictionary<string, DriverProfile>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in profiles)
            {
                foreach (var tag in p.GamerTags)
                {
                    if (!string.IsNullOrWhiteSpace(tag.GamerTag))
                        tagToProfile.TryAdd(tag.GamerTag.Trim(), p);
                }
                if (!string.IsNullOrWhiteSpace(p.DisplayName))
                    tagToProfile.TryAdd(p.DisplayName.Trim(), p);
                tagToProfile.TryAdd(p.DiscordName.Trim(), p);
            }

            var seen = new HashSet<string>();
            var cards = new List<(DriverProfile Profile, DriverDetailViewModel CardData)>();

            foreach (var league in leagues)
            {
                foreach (var standing in league.Standings)
                {
                    if (string.IsNullOrWhiteSpace(standing.Driver)) continue;
                    if (!tagToProfile.TryGetValue(standing.Driver.Trim(), out var profile)) continue;
                    if (!seen.Add(profile.DiscordId)) continue;

                    var aliases = profile.GamerTags
                        .Select(t => t.GamerTag.Trim())
                        .Where(s => s.Length > 0)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    if (!string.IsNullOrWhiteSpace(profile.DisplayName))
                        aliases.Add(profile.DisplayName.Trim());
                    aliases.Add(profile.DiscordName.Trim());

                    int wins = 0, podiums = 0, fastest = 0, totalPoints = 0;
                    int? driverNumber = null;
                    string? team = null;
                    var races = new List<DriverRaceEntry>();

                    foreach (var l in leagues)
                    {
                        var s = l.Standings.FirstOrDefault(x => aliases.Contains(x.Driver?.Trim() ?? ""));
                        if (s is not null)
                        {
                            totalPoints += s.Points;
                            team ??= s.Team;
                            driverNumber ??= s.DriverNumber;
                        }

                        foreach (var r in l.Races)
                        {
                            var finish = r.Finishes.FirstOrDefault(f => aliases.Contains(f.Driver?.Trim() ?? ""));
                            if (finish is null) continue;
                            if (finish.Position == 1) wins++;
                            if (finish.Position is >= 1 and <= 3) podiums++;
                            if (finish.FastestLap) fastest++;
                            races.Add(new DriverRaceEntry
                            {
                                RaceId     = r.RowId,
                                LeagueId   = l.Id,
                                Date       = r.Date,
                                Track      = r.Track,
                                Position   = finish.Position,
                                Points     = 0,
                                FastestLap = finish.FastestLap,
                                Team       = s?.Team ?? string.Empty
                            });
                        }
                    }

                    cards.Add((profile, new DriverDetailViewModel
                    {
                        Driver       = profile.DisplayName ?? profile.DiscordName,
                        Team         = team ?? string.Empty,
                        DriverNumber = driverNumber,
                        TotalPoints  = totalPoints,
                        Wins         = wins,
                        Podiums      = podiums,
                        FastestLaps  = fastest,
                        BestFinish   = races.Where(r => r.Position > 0).Select(r => (int?)r.Position).DefaultIfEmpty(null).Min(),
                        Races        = races
                    }));
                }
            }

            bool isSuperAdmin = User.HasClaim("erdi:superadmin", "true");
            ViewBag.CanEditNames = isSuperAdmin
                || User.Claims.Any(c => c.Type == "erdi:perm"
                    && (c.Value == AdminPermissions.DriversCards || c.Value == AdminPermissions.Drivers));

            return View("~/Views/Admin/DriverCards/Index.cshtml", cards);
        }

        [HttpPost("/admin/driver-cards/update-name")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin.Drivers.Cards")]
        public async Task<IActionResult> UpdateDisplayName(string discordId, string? displayName)
        {
            if (string.IsNullOrWhiteSpace(discordId)) return BadRequest();
            var newName = displayName?.Trim();
            if (string.IsNullOrWhiteSpace(newName))
            {
                TempData["AdminMessage"] = "Bitte einen gültigen Namen eintragen.";
                return RedirectToAction(nameof(Index));
            }

            var profile = await _db.DriverProfiles
                .AsNoTracking()
                .Include(p => p.GamerTags)
                .FirstOrDefaultAsync(p => p.DiscordId == discordId);
            if (profile is null) return NotFound();

            var oldName = profile.GamerTags
                .FirstOrDefault(t => string.Equals(t.Platform, "EA", StringComparison.OrdinalIgnoreCase))
                ?.GamerTag?.Trim()
                ?? profile.DisplayName
                ?? profile.DiscordName;

            var actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "?";
            var changed = await _profiles.RenameIngameNameAsync(discordId, newName, actorId);

            await _audit.LogAsync("AdminRenameDriver", "DriverProfile", discordId,
                $"'{oldName}' → '{newName}', {changed} Referenz(en) aktualisiert");

            TempData["AdminMessage"] = $"EA-Name für {profile.DiscordName} geändert: \"{oldName}\" → \"{newName}\" ({changed} Referenz(en) aktualisiert).";
            return RedirectToAction(nameof(Index));
        }
    }
}
