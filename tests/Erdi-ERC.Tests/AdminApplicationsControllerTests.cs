using Erdi_ERC.Controllers;
using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Erdi_ERC.Tests;

/// <summary>
/// Controller-Integrationstests für die Admin-Bewerbungsansichten: List, Detail,
/// Accept/Reject, Waitlist und PromoteFromWaitlist. Verwendet TestAuthHelper mit
/// Admin.Applications.View/Manage-Claims.
/// </summary>
public class AdminApplicationsControllerTests
{
    private static AdminApplicationsController BuildController(SqliteTestContext ctx)
    {
        var memCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new StaticDataCache(ctx.Db, memCache);
        var svc = new ApplicationService(
            ctx.Db,
            new NoopAudit(),
            new NoopWebhook(),
            cache,
            new ApplicationTargetingService(ctx.Db, cache),
            NullLogger<ApplicationService>.Instance);
        var ctrl = new AdminApplicationsController(svc, cache, ctx.Db,
            Microsoft.Extensions.Options.Options.Create(new Erdi_ERC.Options.DriverMatchingOptions()));
        ctrl.TempData = new TempDataDictionary(new Microsoft.AspNetCore.Http.DefaultHttpContext(),
            new NullTempDataProvider());
        return ctrl;
    }

    // ── List ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_returnsItemsView_withFilter()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "pro", Name = "ProLiga" });
        ctx.Db.Applications.Add(new Application
        {
            Id = "a1", DiscordId = "111", DiscordName = "User1", GamerTag = "R1",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Status = (int)ApplicationStatus.Pending
        });
        await ctx.Db.SaveChangesAsync();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.List(status: "Pending", leagueId: "pro", season: null, page: 1);

        var view = Assert.IsType<ViewResult>(result);
        Assert.NotNull(ctrl.ViewBag.Items);
        Assert.Equal("a1", ((List<Application>)ctrl.ViewBag.Items)[0].Id);
    }

    [Fact]
    public async Task List_filtersByStatus_all()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "pro", Name = "ProLiga" });
        ctx.Db.Applications.Add(new Application
        {
            Id = "a1", DiscordId = "1", DiscordName = "1", GamerTag = "T1",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Status = (int)ApplicationStatus.Pending
        });
        ctx.Db.Applications.Add(new Application
        {
            Id = "a2", DiscordId = "2", DiscordName = "2", GamerTag = "T2",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Status = (int)ApplicationStatus.Rejected
        });
        await ctx.Db.SaveChangesAsync();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.List(status: "all", leagueId: null, season: null, page: 1);

        var view = Assert.IsType<ViewResult>(result);
        var items = (List<Application>)ctrl.ViewBag.Items;
        Assert.Equal(2, items.Count);
    }

    // ── Detail ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Detail_returnsNotFound_forMissingId()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.Detail("nonexistent");
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Detail_returnsView_withApp()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "pro", Name = "ProLiga" });
        ctx.Db.Applications.Add(new Application
        {
            Id = "a1", DiscordId = "111", DiscordName = "User1", GamerTag = "R1",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Status = (int)ApplicationStatus.Pending
        });
        await ctx.Db.SaveChangesAsync();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.Detail("a1");

        var view = Assert.IsType<ViewResult>(result);
        Assert.NotNull(ctrl.ViewBag.App);
        Assert.Equal("a1", ((Application)ctrl.ViewBag.App).Id);
    }

    // ── Accept ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Accept_setsStatusAndRedirectsToList()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "pro", Name = "ProLiga" });
        ctx.Db.Applications.Add(new Application
        {
            Id = "a1", DiscordId = "111", DiscordName = "User1", GamerTag = "R1",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Status = (int)ApplicationStatus.Pending
        });
        await ctx.Db.SaveChangesAsync();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.Accept("a1", "Welcome aboard");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminApplicationsController.List), redirect.ActionName);
        Assert.Equal("Bewerbung angenommen.", ctrl.TempData["AdminMessage"]);

        var app = ctx.Db.Applications.Single();
        Assert.Equal((int)ApplicationStatus.Accepted, app.Status);
    }

    [Fact]
    public async Task Accept_returnsNotFoundMessage_forMissingId()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.Accept("nope", null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Bewerbung nicht gefunden.", ctrl.TempData["AdminMessage"]);
    }

    // ── Reject ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reject_setsStatusAndRedirectsToList()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "pro", Name = "ProLiga" });
        ctx.Db.Applications.Add(new Application
        {
            Id = "a1", DiscordId = "111", DiscordName = "User1", GamerTag = "R1",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Status = (int)ApplicationStatus.Pending
        });
        await ctx.Db.SaveChangesAsync();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.Reject("a1", "Später gerne erneut");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Bewerbung abgelehnt.", ctrl.TempData["AdminMessage"]);

        var app = ctx.Db.Applications.Single();
        Assert.Equal((int)ApplicationStatus.Rejected, app.Status);
        Assert.Equal("Später gerne erneut", app.ReviewNote);
    }

    // ── Waitlist ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Waitlist_returnsEntriesForLeague()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "pro", Name = "ProLiga" });
        ctx.Db.WaitlistEntries.Add(new WaitlistEntry
        {
            Id = "w1", DiscordId = "111", DiscordName = "User1", GamerTag = "R1",
            Platform = "PC", LeagueId = "pro", Position = 1
        });
        await ctx.Db.SaveChangesAsync();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.Waitlist("pro", season: null);

        var view = Assert.IsType<ViewResult>(result);
        var entries = Assert.IsAssignableFrom<IEnumerable<WaitlistEntry>>(ctrl.ViewBag.Entries);
        Assert.Single(entries);
    }

    [Fact]
    public async Task Waitlist_returnsEmpty_forMissingLeagueId()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.Waitlist("", season: null);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Empty((List<WaitlistEntry>)ctrl.ViewBag.Entries);
    }

    // ── PromoteFromWaitlist ────────────────────────────────────────────────────

    [Fact]
    public async Task PromoteFromWaitlist_copiesEntryToApplication()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "pro", Name = "ProLiga" });
        ctx.Db.WaitlistEntries.Add(new WaitlistEntry
        {
            Id = "w1", DiscordId = "111", DiscordName = "User1", GamerTag = "R1",
            Platform = "PC", LeagueId = "pro", Position = 1
        });
        await ctx.Db.SaveChangesAsync();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.PromoteFromWaitlist("w1", "pro");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminApplicationsController.Waitlist), redirect.ActionName);
        Assert.Equal("Wartelisten-Eintrag promoviert.", ctrl.TempData["AdminMessage"]);
        Assert.Single(ctx.Db.Applications);
    }

    [Fact]
    public async Task PromoteFromWaitlist_failure_usesErrorMessage()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.PromoteFromWaitlist("nonexistent", "pro");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.NotNull(ctrl.TempData["AdminMessage"]);
        Assert.Empty(ctx.Db.Applications);
    }

    // ── ManualRegister ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ManualRegister_get_returnsViewWithPrefilledLeague()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "pro", Name = "ProLiga" });
        await ctx.Db.SaveChangesAsync();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.ManualRegister(leagueId: "pro");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ManualRegisterInput>(view.Model);
        Assert.Equal("pro", model.LeagueId);
        Assert.NotNull(ctrl.ViewBag.Leagues);
        Assert.NotNull(ctrl.ViewBag.AllowedPlatforms);
    }

    [Fact]
    public async Task ManualRegister_post_registersDriver_andRedirects()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "pro", Name = "ProLiga" });
        await ctx.Db.SaveChangesAsync();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var input = new ManualRegisterInput
        {
            DiscordId = "123456789", DiscordName = "NewDriver", GamerTag = "NewTag",
            Platform = "PC", LeagueId = "pro", Role = "Stammfahrer"
        };
        var result = await ctrl.ManualRegister(input);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminApplicationsController.ManualRegister), redirect.ActionName);
        Assert.NotNull(ctx.Db.DriverProfiles.SingleOrDefault(p => p.DiscordId == "123456789"));
        Assert.NotNull(ctx.Db.DriverStandings.SingleOrDefault(s => s.Driver == "NewTag"));
    }

    [Fact]
    public async Task ManualRegister_post_showsError_whenAlreadyRegistered()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "pro", Name = "ProLiga" });
        ctx.Db.DriverStandings.Add(new DriverStanding
        {
            LeagueId = "pro", Driver = "NewTag", Position = 0,
            Points = 0, IsReserveDriver = false
        });
        await ctx.Db.SaveChangesAsync();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var input = new ManualRegisterInput
        {
            DiscordId = "123456789", DiscordName = "NewDriver", GamerTag = "NewTag",
            Platform = "PC", LeagueId = "pro", Role = "Stammfahrer"
        };
        var result = await ctrl.ManualRegister(input);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(ctrl.ModelState.IsValid);
        Assert.Empty(ctx.Db.DriverProfiles);
    }

    [Fact]
    public async Task ManualRegister_post_returnsView_onInvalidModelState()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));
        ctrl.ModelState.AddModelError("DiscordId", "required");

        var result = await ctrl.ManualRegister(new ManualRegisterInput());

        Assert.IsType<ViewResult>(result);
        Assert.Empty(ctx.Db.DriverProfiles);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context)
            => new Dictionary<string, object?>();
        public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context,
            IDictionary<string, object?> values) { }
    }

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
