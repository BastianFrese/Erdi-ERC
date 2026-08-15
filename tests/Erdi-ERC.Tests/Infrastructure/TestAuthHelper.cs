using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace Erdi_ERC.Tests.Infrastructure;

/// <summary>
/// Hilfsmethoden, um Controller-Actions mit einem authentifizierten Principal
/// auszuführen, ohne TestServer aufzubauen. Wird für Controller-Integrationstests
/// auf demselben SQLite-In-Memory-Kontext verwendet.
/// </summary>
public static class TestAuthHelper
{
    /// <summary>Erzeugt einen authentifizierten HttpContext für Discord-OAuth-Cookie-Auth.</summary>
    public static DefaultHttpContext CreateAuthenticatedContext(
        string discordId,
        string discordName,
        string? accessToken = null)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, discordId),
            new Claim(ClaimTypes.Name, discordName),
            new Claim(ClaimTypes.AuthenticationMethod, "Discord")
        }, CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };

        if (!string.IsNullOrEmpty(accessToken))
        {
            var authService = new FakeAuthenticationService(accessToken);
            httpContext.RequestServices = new FakeServiceProvider(authService);
        }

        return httpContext;
    }

    /// <summary>Erzeugt einen Admin-Principal mit den angegebenen granularen Claims.</summary>
    public static DefaultHttpContext CreateAdminContext(
        string userId,
        string userName,
        bool canView = true,
        bool canManage = true,
        bool canMetrics = true)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userName),
            new(ClaimTypes.Role, "Admin")
        };

        if (canView) claims.Add(new Claim("Admin.Applications.View", "true"));
        if (canManage) claims.Add(new Claim("Admin.Applications.Manage", "true"));
        if (canMetrics) claims.Add(new Claim("Admin.Applications.Metrics", "true"));
        // Legacy-Rolle, die Program.cs-Policy ebenfalls akzeptiert.
        claims.Add(new Claim("applications", "true"));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }

    /// <summary>Verbindet einen Controller mit dem authentifizierten HttpContext.</summary>
    public static void AttachContext(Controller controller, HttpContext httpContext)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
            RouteData = new Microsoft.AspNetCore.Routing.RouteData()
        };

        // UrlHelper wird gebraucht, wenn Actions RedirectToAction/Url.Action aufrufen.
        // In isolierten Unit-Tests ist kein Endpointrouting vorhanden — Stub reicht.
        controller.Url = new StubUrlHelper();
    }

    private sealed class StubUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new ActionContext();
        public string Action(UrlActionContext actionContext) => $"/stub/{actionContext.Action}";
        public string Content(string? contentPath) => contentPath ?? "";
        public bool IsLocalUrl(string? url) => false;
        public string Link(string? routeName, object? values) => $"/stub/{routeName}";
        public string RouteUrl(UrlRouteContext routeContext) => $"/stub/{routeContext.RouteName}";
    }

    private sealed class FakeAuthenticationService : IAuthenticationService
    {
        private readonly string _accessToken;

        public FakeAuthenticationService(string accessToken)
        {
            _accessToken = accessToken;
        }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
            => Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(
                    new ClaimsPrincipal(new ClaimsIdentity()),
                    new AuthenticationProperties { Items = { { "access_token", _accessToken } } },
                    scheme ?? CookieAuthenticationDefaults.AuthenticationScheme)));

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }

    private sealed class FakeServiceProvider : IServiceProvider
    {
        private readonly IAuthenticationService _authService;

        public FakeServiceProvider(IAuthenticationService authService)
        {
            _authService = authService;
        }

        public object? GetService(Type serviceType)
            => serviceType == typeof(IAuthenticationService) ? _authService : null;
    }
}
