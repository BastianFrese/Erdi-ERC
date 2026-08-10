using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace <OWNER_HANDLE>_ERC.Controllers
{
    /// <summary>
    /// User-seitige Bewerbungs-Routen: Apply (Formular anzeigen + Submit),
    /// Submitted (Bestätigungsseite) und MyApplication (eigene Bewerbungen).
    /// </summary>
    [Authorize]
    public class ApplicationController : Controller
    {
        private readonly IApplicationService _applications;
        private readonly IStaticDataCache _staticCache;
        private readonly IDiscordGuildService _discordGuildService;
        private readonly <OWNER_HANDLE>_ERC.Options.DriverMatchingOptions _driverMatching;
        private readonly ILogger<ApplicationController> _logger;

        public ApplicationController(
            IApplicationService applications,
            IStaticDataCache staticCache,
            IDiscordGuildService discordGuildService,
            Microsoft.Extensions.Options.IOptions<<OWNER_HANDLE>_ERC.Options.DriverMatchingOptions> driverMatching,
            ILogger<ApplicationController> logger)
        {
            _applications = applications;
            _staticCache = staticCache;
            _discordGuildService = discordGuildService;
            _driverMatching = driverMatching.Value;
            _logger = logger;
        }

        // ── Apply (GET) ──────────────────────────────────────────────────────────

        [HttpGet("/Application/Apply")]
        public async Task<IActionResult> Apply()
        {
            var leagues = await _staticCache.GetApplicationLeaguesAsync();
            ViewBag.Leagues = leagues;
            ViewBag.AllowedPlatforms = _driverMatching.AllowedPlatforms;
            ViewBag.LeagueCapacity = await _applications.GetLeagueCapacityAsync(HttpContext.RequestAborted);

            var communityJoined = User.HasClaim("erdi:on-community-guild", "true");
            var leagueJoined = User.HasClaim("erdi:on-league-guild", "true");
            ViewBag.NeedsGuildWarning = !communityJoined || !leagueJoined;

            return View("Apply", new ApplyInput
            {
                DiscordName = User.FindFirst("discord_username")?.Value
                    ?? User.Identity?.Name
                    ?? string.Empty,
            });
        }

        // ── Apply (POST) ─────────────────────────────────────────────────────────

        [HttpPost("/Application/Apply")]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("forms")]
        public async Task<IActionResult> Submit(ApplyInput input)
        {
            var leagues = await _staticCache.GetApplicationLeaguesAsync();
            ViewBag.Leagues = leagues;
            ViewBag.AllowedPlatforms = _driverMatching.AllowedPlatforms;
            ViewBag.LeagueCapacity = await _applications.GetLeagueCapacityAsync(HttpContext.RequestAborted);

            if (!ModelState.IsValid)
            {
                return View("Apply", input);
            }

            var discordId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var discordDisplay = User.FindFirst("discord_username")?.Value
                ?? User.Identity?.Name
                ?? string.Empty;
            if (string.IsNullOrEmpty(discordId))
            {
                return Challenge();
            }

            // Stufe 2: frischer Discord-API-Snapshot (fail-open)
            string? accessToken = null;
            try
            {
                accessToken = await HttpContext.GetTokenAsync("access_token");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve Discord access token");
            }

            bool warning = false;
            string? warningDetail = null;
            var guild = await _discordGuildService.CheckMembershipAsync(accessToken, HttpContext.RequestAborted);
            switch (guild.Status)
            {
                case DiscordGuildCheckStatus.Ok:
                    warning = !guild.JoinedCommunity || !guild.JoinedLeague;
                    if (warning)
                    {
                        var missing = new List<string>();
                        if (!guild.JoinedCommunity) missing.Add("Community");
                        if (!guild.JoinedLeague) missing.Add("Liga");
                        warningDetail = "Fehlend: " + string.Join(", ", missing);
                    }
                    break;
                case DiscordGuildCheckStatus.Unavailable:
                    warningDetail = "Discord-API nicht erreichbar";
                    break;
                case DiscordGuildCheckStatus.LoginExpired:
                    warningDetail = "Discord-Login abgelaufen";
                    break;
            }

            var cmd = new SubmitApplicationCommand(
                DiscordId: discordId,
                DiscordName: discordDisplay,
                GamerTag: input.GamerTag.Trim(),
                Platform: input.Platform,
                TargetLeagueId: input.TargetLeagueId,
                Role: input.Role,
                Motivation: string.IsNullOrWhiteSpace(input.Motivation) ? null : input.Motivation.Trim(),
                DiscordJoinWarning: warning,
                DiscordJoinWarningDetail: warningDetail);

            SubmitApplicationResult result;
            try
            {
                result = await _applications.SubmitAsync(cmd, HttpContext.RequestAborted);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View("Apply", input);
            }

            TempData["SubmitResultType"] = result.Outcome.ToString();
            if (result.Outcome == SubmitOutcome.Waitlisted && result.WaitlistEntry is not null)
            {
                TempData["WaitlistPosition"] = result.WaitlistEntry.Position.ToString();
            }
            if (warning)
            {
                TempData["DiscordWarn"] = "true";
                TempData["DiscordWarnDetail"] = warningDetail;
            }

            return RedirectToAction(nameof(Submitted));
        }

        // ── Submitted (GET) ──────────────────────────────────────────────────────

        [HttpGet("/Application/Submitted")]
        public IActionResult Submitted()
        {
            return View("Submitted");
        }

        // ── MyApplication (GET) ──────────────────────────────────────────────────

        [HttpGet("/Application/MyApplication")]
        public async Task<IActionResult> MyApplication()
        {
            var discordId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(discordId))
            {
                return Challenge();
            }

            var list = await _applications.ListAsync(
                statusFilter: null,
                leagueFilter: null,
                skip: 0,
                take: 50,
                ct: HttpContext.RequestAborted);

            var mine = list.Where(a => a.DiscordId == discordId).ToList();
            return View("MyApplication", mine);
        }
    }

    /// <summary>Eingabe-Modell für Apply GET/POST.</summary>
    public class ApplyInput
    {
        public string DiscordName { get; set; } = string.Empty;
        public string GamerTag { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string TargetLeagueId { get; set; } = string.Empty;
        public string Role { get; set; } = "Stammfahrer";
        public string? Motivation { get; set; }
    }
}
