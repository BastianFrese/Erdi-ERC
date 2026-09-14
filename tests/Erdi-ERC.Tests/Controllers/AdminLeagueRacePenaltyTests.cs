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
/// Renn-Eintragung: Strafsekunden muessen auf die Gesamtzeit (RaceTimeMs)
/// genau des Fahrers addiert werden, in dessen Zeile sie eingetragen wurden,
/// und die eingetragene Zeit ist die GESAMT gefahrene Rennzeit (keine
/// Rundenzeit) — inkl. Stunden-Format H:MM:SS.mmm.
/// </summary>
public class AdminRaceEntryPenaltyTests
{
    private static AdminLeagueController BuildController(SqliteTestContext ctx)
    {
        var memCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new StaticDataCache(ctx.Db, memCache);
        var profileService = new DriverProfileService(
            ctx.Db,
            OptionsFactory.Create(new DriverMatchingOptions
            {
                MinQueryLength = 2,
                MaxLevenshteinDistance = 2,
                MaxSuggestions = 5,
            }));
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

    [Fact]
    public async Task SaveEnteredRace_penaltyOnSecondDriver_addsSecondsToThatDriverOnly()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "l1", Name = "L1" });
        ctx.Db.SaveChanges();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.SaveEnteredRace(
            leagueId: "l1",
            date: new DateTime(2026, 8, 31),
            track: "Monza",
            fastestLapDriver: "Alpha",
            positions: new[] { "Alpha", "Bravo" },
            raceTimes: new[] { "1:30.000", "1:31.000" },
            penaltySeconds: new[] { "", "10" },
            dnfDrivers: null,
            reserveDrivers: null,
            reserveMainDrivers: null);

        Assert.IsType<RedirectToActionResult>(result);

        var finishes = ctx.Db.RaceFinishes
            .Where(f => f.Driver == "Alpha" || f.Driver == "Bravo")
            .OrderBy(f => f.Position)
            .ToList();
        Assert.Equal(2, finishes.Count);
        Assert.Equal(90_000, finishes[0].RaceTimeMs);
        // 1:31.000 = 91.000 ms + 10 s Strafe = 101.000 ms.
        Assert.Equal(101_000, finishes[1].RaceTimeMs);
        Assert.Equal(1, finishes[0].Position);
        Assert.Equal(2, finishes[1].Position);
    }

    [Fact]
    public async Task SaveEnteredRace_penaltyOnWinner_keepsPositionButAddsTime()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "l1", Name = "L1" });
        ctx.Db.SaveChanges();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.SaveEnteredRace(
            leagueId: "l1",
            date: new DateTime(2026, 8, 31),
            track: "Monza",
            fastestLapDriver: null,
            positions: new[] { "Alpha", "Bravo" },
            raceTimes: new[] { "1:30.000", "1:31.000" },
            penaltySeconds: new[] { "10", "" },
            dnfDrivers: null,
            reserveDrivers: null,
            reserveMainDrivers: null);

        Assert.IsType<RedirectToActionResult>(result);

        var alpha = ctx.Db.RaceFinishes.Single(f => f.Driver == "Alpha");
        Assert.Equal(100_000, alpha.RaceTimeMs); // 90.000 + 10 s
        Assert.Equal("Alpha", ctx.Db.RaceResults.First().Winner);
    }

    [Fact]
    public async Task SaveEnteredRace_totalTimeWithHours_parsesAsFullRaceTime()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "l1", Name = "L1" });
        ctx.Db.SaveChanges();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        // 1:24:35.100 = 1 h 24 min 35.1 s = 5.075.100 ms (Gesamtzeit, keine Rundenzeit).
        var result = await ctrl.SaveEnteredRace(
            leagueId: "l1",
            date: new DateTime(2026, 8, 31),
            track: "Monza",
            fastestLapDriver: null,
            positions: new[] { "Alpha" },
            raceTimes: new[] { "1:24:35.100" },
            penaltySeconds: new[] { "" },
            dnfDrivers: null,
            reserveDrivers: null,
            reserveMainDrivers: null);

        Assert.IsType<RedirectToActionResult>(result);

        var finish = ctx.Db.RaceFinishes.Single(f => f.Driver == "Alpha");
        Assert.Equal(5_075_100, finish.RaceTimeMs);
    }

    [Fact]
    public async Task SaveEnteredRace_minusPenalty_subtractsTime()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "l1", Name = "L1" });
        ctx.Db.SaveChanges();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.SaveEnteredRace(
            leagueId: "l1",
            date: new DateTime(2026, 8, 31),
            track: "Monza",
            fastestLapDriver: null,
            positions: new[] { "Alpha" },
            raceTimes: new[] { "1:30.000" },
            penaltySeconds: new[] { "-5" }, // Minus-Strafe = Zeit-Abzug
            dnfDrivers: null,
            reserveDrivers: null,
            reserveMainDrivers: null);

        Assert.IsType<RedirectToActionResult>(result);

        var finish = ctx.Db.RaceFinishes.Single(f => f.Driver == "Alpha");
        // 90.000 ms − 5 s = 85.000 ms.
        Assert.Equal(85_000, finish.RaceTimeMs);
    }

    [Fact]
    public async Task SaveEnteredRace_minusPenalty_cannotMakeTimeNegative()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "l1", Name = "L1" });
        ctx.Db.SaveChanges();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        // Absurder Abzug (> Gesamtzeit): effektive Zeit wird auf mind. 1 ms geklemmt.
        var result = await ctrl.SaveEnteredRace(
            leagueId: "l1",
            date: new DateTime(2026, 8, 31),
            track: "Monza",
            fastestLapDriver: null,
            positions: new[] { "Alpha" },
            raceTimes: new[] { "1:30.000" },
            penaltySeconds: new[] { "-200" },
            dnfDrivers: null,
            reserveDrivers: null,
            reserveMainDrivers: null);

        Assert.IsType<RedirectToActionResult>(result);

        var finish = ctx.Db.RaceFinishes.Single(f => f.Driver == "Alpha");
        Assert.Equal(1, finish.RaceTimeMs);
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