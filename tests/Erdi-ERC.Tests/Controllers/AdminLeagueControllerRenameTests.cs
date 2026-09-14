using Erdi_ERC.Controllers;
using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Erdi_ERC.Options;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Erdi_ERC.Tests.Controllers;

/// <summary>
/// Tests für die systemweite Fahrer-Umbenennung über die Ligaverwaltung:
/// <see cref="AdminLeagueController.SaveAllStandings"/> erkennt Namensänderungen und
/// propagiert sie (Fahrerkarte/Profil, andere Ligen, Renn-Ergebnisse), wenn der alte
/// Name zu einem DriverProfile aufgelöst werden kann.
/// </summary>
public class AdminLeagueControllerRenameTests
{
    private static AdminLeagueController BuildController(SqliteTestContext ctx)
    {
        var memCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new StaticDataCache(ctx.Db, memCache);
        var profileService = new DriverProfileService(ctx.Db, OptionsFactory.Create(new DriverMatchingOptions()));
        var ctrl = new AdminLeagueController(
            ctx.Db,
            new NoopAudit(),
            profileService,
            new NoopWebhook(),
            new NoopStats(),
            cache);
        ctrl.TempData = new TempDataDictionary(new DefaultHttpContext(), new NullTempDataProvider());
        return ctrl;
    }

    private static DriverProfile SeedProfile(SqliteTestContext ctx, string tag, string discordId = "discord-1")
    {
        var profile = new DriverProfile
        {
            DiscordId = discordId,
            DiscordName = $"{tag}#0001",
            DisplayName = tag,
            PreferredPlatform = "EA",
            GamerTags = new List<DriverGamerTag>
            {
                new() { DiscordId = discordId, Platform = "EA", GamerTag = tag, IsPrimary = true }
            }
        };
        ctx.Db.DriverProfiles.Add(profile);
        ctx.Db.SaveChanges();
        return profile;
    }

    [Fact]
    public async Task SaveAllStandings_renamedDriver_propagatesSystemWide()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "l1", Name = "League One" });
        ctx.Db.Leagues.Add(new League { Id = "l2", Name = "League Two" });
        ctx.Db.SaveChanges();
        SeedProfile(ctx, "OldName");

        // l1: die Zeile, die umbenannt wird.
        var l1Standing = new DriverStanding { LeagueId = "l1", Driver = "OldName", Team = "Ferrari", Position = 1, Points = 50 };
        ctx.Db.DriverStandings.Add(l1Standing);
        // l2: andere Liga mit altem Namen → muss propagiert werden.
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l2", Driver = "OldName", Team = "Ferrari", Position = 1, Points = 30 });
        var race = new RaceResult { LeagueId = "l2", Date = DateTime.UtcNow, Track = "Spa", Winner = "OldName", FastestLap = "" };
        race.Finishes.Add(new RaceFinish { Driver = "OldName", Position = 1 });
        ctx.Db.RaceResults.Add(race);
        ctx.Db.SaveChanges();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.SaveAllStandings(
            leagueId: "l1",
            rowId: new[] { l1Standing.RowId.ToString() },
            driver: new[] { "NewName" },
            driverNumber: new[] { "1" },
            team: new[] { "Ferrari" },
            isReserveDriver: new[] { "false" },
            reserveForDriver: new[] { "" },
            pointsAdjustment: new[] { "0" },
            deleteRowIds: null);

        Assert.IsType<RedirectToActionResult>(result);
        var msg = ctrl.TempData["AdminMessage"] as string;
        Assert.NotNull(msg);
        Assert.Contains("systemweit umbenannt", msg!);

        using var verify = ctx.NewContext();
        Assert.Equal("NewName", (await verify.DriverStandings.SingleAsync(s => s.LeagueId == "l1")).Driver);
        Assert.Equal("NewName", (await verify.DriverStandings.SingleAsync(s => s.LeagueId == "l2")).Driver);
        var reloaded = await verify.RaceResults.Include(r => r.Finishes).SingleAsync();
        Assert.Equal("NewName", reloaded.Winner);
        Assert.Equal("NewName", reloaded.Finishes.Single().Driver);
        var profile = await verify.DriverProfiles.Include(p => p.GamerTags).SingleAsync();
        Assert.Equal("NewName", profile.DisplayName);
    }

    [Fact]
    public async Task SaveAllStandings_swapRenameInOneSubmit_isRejected()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "l1", Name = "League One" });
        ctx.Db.SaveChanges();
        SeedProfile(ctx, "Alice");
        SeedProfile(ctx, "Bob", discordId: "discord-2");
        var rowA = new DriverStanding { LeagueId = "l1", Driver = "Alice", Team = "", Position = 1, Points = 10 };
        var rowB = new DriverStanding { LeagueId = "l1", Driver = "Bob", Team = "", Position = 2, Points = 5 };
        ctx.Db.DriverStandings.Add(rowA);
        ctx.Db.DriverStandings.Add(rowB);
        ctx.Db.SaveChanges();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        // Tausch in einem Submit: Zeile A "Alice"→"Bob", Zeile B "Bob"→"Alice".
        var result = await ctrl.SaveAllStandings(
            leagueId: "l1",
            rowId: new[] { rowA.RowId.ToString(), rowB.RowId.ToString() },
            driver: new[] { "Bob", "Alice" },
            driverNumber: new[] { "1", "2" },
            team: new[] { "", "" },
            isReserveDriver: new[] { "false", "false" },
            reserveForDriver: new[] { "", "" },
            pointsAdjustment: new[] { "0", "0" },
            deleteRowIds: null);

        Assert.IsType<RedirectToActionResult>(result);
        var msg = ctrl.TempData["AdminMessage"] as string;
        Assert.NotNull(msg);
        Assert.Contains("Tausch", msg!);

        // Nichts gespeichert — die Zeilen bleiben unverändert.
        using var verify = ctx.NewContext();
        Assert.Equal("Alice", (await verify.DriverStandings.SingleAsync(s => s.RowId == rowA.RowId)).Driver);
        Assert.Equal("Bob", (await verify.DriverStandings.SingleAsync(s => s.RowId == rowB.RowId)).Driver);
    }

    [Fact]
    public async Task SaveAllStandings_noNameChange_noPropagation()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "l1", Name = "League One" });
        ctx.Db.Leagues.Add(new League { Id = "l2", Name = "League Two" });
        ctx.Db.SaveChanges();
        SeedProfile(ctx, "OldName");
        var l1Standing = new DriverStanding { LeagueId = "l1", Driver = "OldName", Team = "Ferrari", Position = 1, Points = 50 };
        ctx.Db.DriverStandings.Add(l1Standing);
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l2", Driver = "OldName", Team = "Ferrari", Position = 1, Points = 30 });
        ctx.Db.SaveChanges();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.SaveAllStandings(
            leagueId: "l1",
            rowId: new[] { l1Standing.RowId.ToString() },
            driver: new[] { "OldName" },
            driverNumber: new[] { "1" },
            team: new[] { "Ferrari" },
            isReserveDriver: new[] { "false" },
            reserveForDriver: new[] { "" },
            pointsAdjustment: new[] { "0" },
            deleteRowIds: null);

        Assert.IsType<RedirectToActionResult>(result);
        var msg = ctrl.TempData["AdminMessage"] as string;
        Assert.NotNull(msg);
        Assert.DoesNotContain("systemweit umbenannt", msg!);

        using var verify = ctx.NewContext();
        Assert.Equal("OldName", (await verify.DriverStandings.SingleAsync(s => s.LeagueId == "l2")).Driver);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }

    private sealed class NoopAudit : IAdminAuditService
    {
        public Task LogAsync(string action, string entityType, string entityId, string details) => Task.CompletedTask;
        public Task LogAndSaveAsync(string action, string entityType, string entityId, string details) => Task.CompletedTask;
    }

    private sealed class NoopWebhook : IWebhookAutomationService
    {
        public Task FireAsync(string eventType, Dictionary<string, string> vars) => Task.CompletedTask;
        public Task<(bool Success, string? Error)> TestRuleAsync(int ruleId, Dictionary<string, string> vars, CancellationToken ct = default)
            => Task.FromResult((true, (string?)null));
    }

    private sealed class NoopStats : IStatsService
    {
        public Task RebuildLeagueStandingsAsync(string leagueId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RebuildAllLeagueStandingsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
