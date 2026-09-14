using System.Text;
using Erdi_ERC.Middleware;
using Erdi_ERC.Services;
using Microsoft.AspNetCore.Http;

namespace Erdi_ERC.Tests;

/// <summary>
/// Sichert die Durchlass-/Block-Entscheidung des <see cref="MaintenanceModeMiddleware"/>:
/// Wartungsmodus blockt Nicht-Admins, lässt aber Admins, Health-Checks und den
/// Login/OAuth-Pfad immer durch (sonst könnte sich ein ausgeloggter Admin nicht
/// mehr einloggen, um den Modus zu beenden).
/// </summary>
public class MaintenanceModeMiddlewareTests
{
    [Fact]
    public void MaintenanceOff_NeverBlocks()
    {
        Assert.False(MaintenanceModeMiddleware.ShouldBlock("/", isAdmin: false, maintenanceActive: false));
        Assert.False(MaintenanceModeMiddleware.ShouldBlock("/Races/Results", isAdmin: false, maintenanceActive: false));
    }

    [Fact]
    public void MaintenanceOn_BlocksAnonymousHome()
    {
        Assert.True(MaintenanceModeMiddleware.ShouldBlock("/", isAdmin: false, maintenanceActive: true));
    }

    [Fact]
    public void MaintenanceOn_BlocksPublicPages()
    {
        Assert.True(MaintenanceModeMiddleware.ShouldBlock("/Races/Results", isAdmin: false, maintenanceActive: true));
        Assert.True(MaintenanceModeMiddleware.ShouldBlock("/Stats/Erdi10", isAdmin: false, maintenanceActive: true));
    }

    [Fact]
    public void MaintenanceOn_AdminPassesThrough()
    {
        Assert.False(MaintenanceModeMiddleware.ShouldBlock("/", isAdmin: true, maintenanceActive: true));
        Assert.False(MaintenanceModeMiddleware.ShouldBlock("/admin", isAdmin: true, maintenanceActive: true));
    }

    [Fact]
    public void MaintenanceOn_HealthChecksPassThrough()
    {
        Assert.False(MaintenanceModeMiddleware.ShouldBlock("/health/live", isAdmin: false, maintenanceActive: true));
        Assert.False(MaintenanceModeMiddleware.ShouldBlock("/health/ready", isAdmin: false, maintenanceActive: true));
    }

    [Fact]
    public void MaintenanceOn_LoginFlowAllThreeHopsPassThrough()
    {
        // 1) Discord-Challenge, 2) OAuth-Callback, 3) Cookie-Set (SignInAsync) — alle drei
        // Hops müssen frei bleiben, sonst kann sich ein ausgeloggter Admin nicht einloggen.
        Assert.False(MaintenanceModeMiddleware.ShouldBlock("/Account/Login", isAdmin: false, maintenanceActive: true));
        Assert.False(MaintenanceModeMiddleware.ShouldBlock("/signin-discord", isAdmin: false, maintenanceActive: true));
        Assert.False(MaintenanceModeMiddleware.ShouldBlock("/signin-discord?code=abc", isAdmin: false, maintenanceActive: true));
        Assert.False(MaintenanceModeMiddleware.ShouldBlock("/Account/LoginCallback", isAdmin: false, maintenanceActive: true));
        // Hinweis: ShouldBlock bekommt nur den Pfad (Request.Path ohne QueryString) — die
        // QueryString-Variante wird im echten Flow nie übergeben, der InvokeAsync-Test deckt
        // den HTTP-Vertrag ab.
    }

    [Fact]
    public void MaintenanceOn_LogoutPassesThrough()
    {
        Assert.False(MaintenanceModeMiddleware.ShouldBlock("/Account/Logout", isAdmin: false, maintenanceActive: true));
    }

    [Fact]
    public void MaintenanceOn_TrailingSlashOnLoginIsTolerated()
    {
        Assert.False(MaintenanceModeMiddleware.ShouldBlock("/Account/Login/", isAdmin: false, maintenanceActive: true));
    }

    [Fact]
    public void MaintenanceOn_SimilarButDifferentPathsAreBlocked()
    {
        // Präzise Allowlist: ähnliche Pfade dürfen NICHT durchrutschen.
        Assert.True(MaintenanceModeMiddleware.ShouldBlock("/signin-discord-evil", isAdmin: false, maintenanceActive: true));
        Assert.True(MaintenanceModeMiddleware.ShouldBlock("/Account/LoginCallbackEvil", isAdmin: false, maintenanceActive: true));
    }

    // ---- HTTP-Vertrag von InvokeAsync ----

    [Fact]
    public async Task InvokeAsync_BlockedRequest_Returns503NoStoreHtmlEncoded()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/";
        context.Response.Body = new MemoryStream();
        var nextCalled = false;

        var middleware = new MaintenanceModeMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var settings = new FakeSiteSettings { MaintenanceMode = true, MaintenanceMessage = "<script>alert('x')</script>" };

        await middleware.InvokeAsync(context, settings);

        Assert.False(nextCalled, "next() darf bei blockiertem Request nicht laufen.");
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.StartsWith("text/html", context.Response.ContentType);

        var body = Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
        Assert.Contains("Wartungsarbeiten", body);
        Assert.DoesNotContain("<script>alert", body);          // XSS: Message muss escaped sein
        Assert.Contains("&lt;script&gt;", body);
    }

    [Fact]
    public async Task InvokeAsync_AdminRequest_PassesThrough()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/admin";
        context.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim("erdi:admin", "true")
            }, "test"));
        var nextCalled = false;

        var middleware = new MaintenanceModeMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var settings = new FakeSiteSettings { MaintenanceMode = true };

        await middleware.InvokeAsync(context, settings);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_MaintenanceOff_PassesThrough()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/";
        var nextCalled = false;

        var middleware = new MaintenanceModeMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var settings = new FakeSiteSettings { MaintenanceMode = false };

        await middleware.InvokeAsync(context, settings);

        Assert.True(nextCalled);
    }

    private sealed class FakeSiteSettings : ISiteSettingsService
    {
        public bool MaintenanceMode { get; init; }
        public string MaintenanceMessage { get; init; } = string.Empty;

        public bool IsAppDownloadEnabled() => false;
        public void SetAppDownloadEnabled(bool enabled, string changedBy) { }
        public bool HasInstallerFile() => false;
        public bool IsMaintenanceMode() => MaintenanceMode;
        public void SetMaintenanceMode(bool enabled, string changedBy) { }
        public string GetMaintenanceMessage() => MaintenanceMessage;
        public void SetMaintenanceMessage(string message, string changedBy) { }
    }
}
