using AspNet.Security.OAuth.Discord;
using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace <OWNER_HANDLE>_ERC.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ISetupAccessService _setupAccessService;

        public AccountController(AppDbContext db, ISetupAccessService setupAccessService)
        {
            _db = db;
            _setupAccessService = setupAccessService;
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
                var isAdmin = await _db.AdminUsers.AnyAsync(a => a.DiscordId == discordId);
                if (isAdmin)
                {
                    identity.AddClaim(new Claim("erdi:admin", "true"));
                }

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

            identity.AddClaim(new Claim("erdi:setup-tier", setupAccess.Tier.ToString()));
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
    }
}
