using AspNet.Security.OAuth.Discord;
using Erdi_ERC.Data;
using Erdi_ERC.Helpers;
using Erdi_ERC.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Erdi_ERC.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ISetupAccessService _setupAccessService;
        private readonly ITrollService _trollService;
        private readonly ILogger<AccountController> _logger;

        // Cookie, das einen User nach einem Troll für TrollOptions.CooldownMinutes verschont.
        private const string TrollCooldownCookie = "erdi-troll-cd";

        public AccountController(
            AppDbContext db,
            ISetupAccessService setupAccessService,
            ITrollService trollService,
            ILogger<AccountController> logger)
        {
            _db = db;
            _setupAccessService = setupAccessService;
            _trollService = trollService;
            _logger = logger;
        }

        [HttpGet]
        [EnableRateLimiting("auth")]
        public IActionResult Login(string? returnUrl = null)
        {
            returnUrl ??= Url.Action("Index", "Home");
            return Challenge(
                new AuthenticationProperties { RedirectUri = Url.Action(nameof(LoginCallback), new { returnUrl })! },
                DiscordAuthenticationDefaults.AuthenticationScheme);
        }

        [HttpGet]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> LoginCallback(string? returnUrl = null)
        {
            var result = await HttpContext.AuthenticateAsync(DiscordAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded || result.Principal is null)
            {
                return RedirectToAction(nameof(Login));
            }

            var identity = (ClaimsIdentity)result.Principal.Identity!;
            var discordId = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(discordId))
            {
                // Admin-Claims (erdi:admin, ggf. erdi:superadmin + erdi:perm) bereits beim
                // Login setzen — sonst fehlt der Admin-Sidebar bis zum ersten Claims-Sync
                // (RefreshDiscordMembershipMinutes) jeder Kategorie-Gruppe.
                var adminUser = await _db.AdminUsers
                    .Include(a => a.Permissions)
                    .FirstOrDefaultAsync(a => a.DiscordId == discordId);
                AdminClaimsHelper.AddAdminClaims(identity, adminUser);

                var freshDiscordName = identity.FindFirst(ClaimTypes.Name)?.Value;
                if (!string.IsNullOrWhiteSpace(freshDiscordName))
                {
                    var profile = await _db.DriverProfiles.AsTracking()
                        .FirstOrDefaultAsync(p => p.DiscordId == discordId);
                    if (profile is not null && !string.Equals(profile.DiscordName, freshDiscordName, StringComparison.Ordinal))
                    {
                        profile.DiscordName = freshDiscordName;
                        await _db.SaveChangesAsync();
                    }
                }
            }

            var accessToken = result.Properties?.GetTokenValue("access_token");
            var setupAccess = await _setupAccessService.ResolveSetupAccessAsync(accessToken, HttpContext.RequestAborted);

            // Admin kann manuell einen höheren Tier vergeben (Setup-Zugang verschenken).
            int effectiveTier = setupAccess.Tier;
            if (!string.IsNullOrEmpty(discordId))
            {
                var manualTier = await _db.DriverProfiles.AsNoTracking()
                    .Where(p => p.DiscordId == discordId && p.ManualSetupTier != null)
                    .Select(p => p.ManualSetupTier)
                    .FirstOrDefaultAsync();
                if (manualTier.HasValue && manualTier.Value > effectiveTier)
                    effectiveTier = manualTier.Value;
            }

            identity.AddClaim(new Claim("erdi:setup-tier", effectiveTier.ToString()));
            if (!string.IsNullOrWhiteSpace(setupAccess.RoleLabel))
            {
                identity.AddClaim(new Claim("erdi:setup-role", setupAccess.RoleLabel));
            }
            identity.AddClaim(new Claim("erdi:on-community-guild", setupAccess.IsOnCommunityGuild ? "true" : "false"));
            if (setupAccess.GuildJoinedAtUtc.HasValue)
            {
                identity.AddClaim(new Claim("erdi:guild-joined-at", setupAccess.GuildJoinedAtUtc.Value.ToString("O")));
            }
            identity.AddClaim(new Claim("erdi:tenure-pending", setupAccess.IsPendingTenure ? "true" : "false"));
            identity.AddClaim(new Claim("erdi:setup-sync-at", DateTimeOffset.UtcNow.ToString("O")));

            var authProps = new AuthenticationProperties
            {
                IsPersistent = true
            };

            var tokens = result.Properties?.GetTokens()?.ToList();
            if (tokens is { Count: > 0 })
            {
                authProps.StoreTokens(tokens);
            }

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                authProps);

            // --- Erdi-Troll-Roll: mit kleiner Wahrscheinlichkeit einen Login-Prank dazwischenschieben. ---
            // Strikt fail-open: ein Fehler im Troll-System darf den Login NIE blockieren (siehe TryStartTroll).
            var safeReturnUrl = (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) ? returnUrl! : "/";
            if (TryStartTroll(identity, safeReturnUrl, out var trollRedirect))
            {
                return trollRedirect!;
            }

            // Url.IsLocalUrl bevor LocalRedirect: schützt vor 500-Fehlerseite, wenn
            // ein Angreifer den User mit ?returnUrl=https://evil.com auf den Login-Flow lockt.
            // (LocalRedirect würde sonst InvalidOperationException werfen.)
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return Redirect("/");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied() => View();

        /// <summary>
        /// Würfelt einen Login-Prank aus und legt – wenn er greift – die Gag-Daten in TempData ab.
        /// Strikt fail-open: jeder Fehler wird geloggt und führt zu „kein Troll" (normaler Login).
        /// </summary>
        private bool TryStartTroll(ClaimsIdentity identity, string safeReturnUrl, out IActionResult? redirect)
        {
            redirect = null;
            try
            {
                var isAdmin = identity.HasClaim("erdi:admin", "true");
                if (!_trollService.IsEnabled
                    || (isAdmin && !_trollService.AppliesToAdmins)
                    || IsTrollOnCooldown()
                    || !_trollService.RollShouldTrigger())
                {
                    return false;
                }

                var gag = _trollService.PickGag();
                var challenge = _trollService.BuildChallenge(gag);

                TempData[TrollController.TkGag] = gag.Key;
                if (challenge.Prompt is not null) TempData[TrollController.TkPrompt] = challenge.Prompt;
                if (challenge.ExpectedAnswer is not null) TempData[TrollController.TkAnswer] = challenge.ExpectedAnswer;
                TempData[TrollController.TkReturnUrl] = safeReturnUrl;

                SetTrollCooldown();
                redirect = RedirectToAction("Gate", "Troll");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Troll-Roll beim Login fehlgeschlagen – überspringe (fail-open).");
                return false;
            }
        }

        private bool IsTrollOnCooldown()
            => long.TryParse(Request.Cookies[TrollCooldownCookie], out var untilUnix)
               && DateTimeOffset.FromUnixTimeSeconds(untilUnix) > DateTimeOffset.UtcNow;

        private void SetTrollCooldown()
        {
            var until = DateTimeOffset.UtcNow.Add(_trollService.Cooldown);
            Response.Cookies.Append(TrollCooldownCookie, until.ToUnixTimeSeconds().ToString(), new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = until,
                IsEssential = true
            });
        }
    }
}
