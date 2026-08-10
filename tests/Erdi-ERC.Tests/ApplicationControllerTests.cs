using <OWNER_HANDLE>_ERC.Controllers;
using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Options;
using <OWNER_HANDLE>_ERC.Services;
using <OWNER_HANDLE>_ERC.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace <OWNER_HANDLE>_ERC.Tests;

/// <summary>
/// Controller-Integrationstests für den User-seitigen Bewerbungs-Flow (Apply GET/POST,
/// Submitted, MyApplication). Benutzt denselben SQLite-In-Memory-Kontext wie die
/// Service-Tests, plus einen Stub-DiscordGuildService.
/// </summary>
public class ApplicationControllerTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static ApplicationController BuildController(
        SqliteTestContext ctx,
        IDiscordGuildService? guildSvc = null,
        IApplicationService? apps = null)
    {
        var memCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var cache = new StaticDataCache(ctx.Db, memCache);
        var svc = apps ?? BuildAppSvc(ctx.Db);
        var logger = NullLogger<ApplicationController>.Instance;
        var ctrl = new ApplicationController(
            svc,
            cache,
            guildSvc ?? FakeDiscordGuildService.FullyJoined(),
            Microsoft.Extensions.Options.Options.Create(new DriverMatchingOptions()),
            logger);

        ctrl.TempData = new TempDataDictionary(new DefaultHttpContext(), new SessionlessTempDataProvider());
        return ctrl;
    }

    private static ApplicationService BuildAppSvc(AppDbContext db)
    {
        var memCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        return new ApplicationService(
            db,
            new NoopAudit(),
            new NoopWebhook(),
            new StaticDataCache(db, memCache),
            NullLogger<ApplicationService>.Instance);
    }

    // ── Apply GET ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Apply_GET_rendersView_withLeaguesAndDiscordName()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "pro", Name = "ProLiga", CountsTowardOverall = true });
        await ctx.Db.SaveChangesAsync();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAuthenticatedContext("111", "RacerOne"));

        var result = await ctrl.Apply();

        Assert.IsType<ViewResult>(result);
        Assert.Equal("Apply", ((ViewResult)result).ViewName);
        Assert.NotNull(ctrl.ViewBag.Leagues);
        Assert.NotNull(ctrl.ViewBag.AllowedPlatforms);
    }

    // ── Apply POST ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_createsApplication_andRedirectsToSubmitted()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "pro", Name = "ProLiga", CountsTowardOverall = true });
        await ctx.Db.SaveChangesAsync();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAuthenticatedContext("111", "RacerOne"));

        var input = new ApplyInput
        {
            GamerTag = "RacerOne",
            Platform = "PC",
            TargetLeagueId = "pro",
            Role = "Stammfahrer",
            Motivation = "Bin motiviert"
        };
        var result = await ctrl.Submit(input);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ApplicationController.Submitted), redirect.ActionName);
        Assert.Single(ctx.Db.Applications);
    }

    [Fact]
    public async Task Submit_returnsChallenge_whenDiscordIdMissing()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "pro", Name = "ProLiga" });
        await ctx.Db.SaveChangesAsync();

        var ctrl = BuildController(ctx);
        // Empty principal — no NameIdentifier claim
        TestAuthHelper.AttachContext(ctrl, new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal()
        });

        var result = await ctrl.Submit(new ApplyInput { GamerTag = "X", TargetLeagueId = "pro", Role = "Stammfahrer" });

        Assert.IsType<ChallengeResult>(result);
    }

    [Fact]
    public async Task Submit_routesToWaitlist_whenCapacityFull()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "pro", Name = "ProLiga", Capacity = 0 });
        ctx.Db.DriverStandings.Add(new DriverStanding
        {
            LeagueId = "pro", Driver = "Full", Position = 0, Points = 0,
            IsReserveDriver = false
        });
        await ctx.Db.SaveChangesAsync();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAuthenticatedContext("111", "Racer1"));

        var result = await ctrl.Submit(new ApplyInput
        {
            GamerTag = "Racer1", TargetLeagueId = "pro", Role = "Stammfahrer"
        });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Empty(ctx.Db.Applications);
        Assert.Single(ctx.Db.WaitlistEntries);
        Assert.Equal("1", ctrl.TempData["WaitlistPosition"]);
    }

    [Fact]
    public async Task Submit_addsDiscordWarning_whenUserMissingLeagueGuild()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "pro", Name = "ProLiga" });
        await ctx.Db.SaveChangesAsync();

        var ctrl = BuildController(ctx, FakeDiscordGuildService.MissingLeague());
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAuthenticatedContext("111", "User1"));

        await ctrl.Submit(new ApplyInput
        {
            GamerTag = "User1", TargetLeagueId = "pro", Role = "Stammfahrer"
        });

        Assert.Equal("true", ctrl.TempData["DiscordWarn"]);
        Assert.NotNull(ctrl.TempData["DiscordWarnDetail"]);

        var app = ctx.Db.Applications.Single();
        Assert.True(app.DiscordJoinWarning);
    }

    [Fact]
    public async Task Submit_returnsViewWithError_whenLeagueMissing()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAuthenticatedContext("111", "User1"));

        var result = await ctrl.Submit(new ApplyInput
        {
            GamerTag = "User1", TargetLeagueId = "nope", Role = "Stammfahrer"
        });

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Apply", view.ViewName);
        Assert.False(ctrl.ModelState.IsValid);
    }

    // ── Submitted ───────────────────────────────────────────────────────────────

    [Fact]
    public void Submitted_rendersView()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAuthenticatedContext("111", "User1"));

        var result = ctrl.Submitted();
        Assert.IsType<ViewResult>(result);
    }

    // ── MyApplication ───────────────────────────────────────────────────────────

    [Fact]
    public async Task MyApplication_returnsOnlyCallerApplications()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "pro", Name = "ProLiga" });
        ctx.Db.Applications.Add(new Application
        {
            Id = "a1", DiscordId = "111", DiscordName = "User1", GamerTag = "R1",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Status = (int)ApplicationStatus.Pending
        });
        ctx.Db.Applications.Add(new Application
        {
            Id = "a2", DiscordId = "222", DiscordName = "User2", GamerTag = "R2",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Status = (int)ApplicationStatus.Pending
        });
        await ctx.Db.SaveChangesAsync();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAuthenticatedContext("111", "User1"));

        var result = await ctrl.MyApplication();

        var view = Assert.IsType<ViewResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<Application>>(view.Model);
        Assert.Single(list);
    }

    [Fact]
    public async Task MyApplication_returnsChallenge_whenDiscordIdMissing()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal()
        });

        var result = await ctrl.MyApplication();
        Assert.IsType<ChallengeResult>(result);
    }

    // ── Sessionless TempData provider for tests ────────────────────────────────

    private sealed class SessionlessTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }

    // ── No-op doubles ──────────────────────────────────────────────────────────

    private sealed class NoopAudit : IAdminAuditService
    {
        public Task LogAsync(string action, string entityType, string entityId, string details) => Task.CompletedTask;
        public Task LogAndSaveAsync(string action, string entityType, string entityId, string details) => Task.CompletedTask;
    }
    private sealed class NoopWebhook : IWebhookAutomationService
    {
        public Task FireAsync(string eventType, Dictionary<string, string> vars) => Task.CompletedTask;
        public Task<(bool Success, string? Error)> TestRuleAsync(int ruleId, Dictionary<string, string> vars, CancellationToken ct = default) => Task.FromResult((true, (string?)null));
    }
}