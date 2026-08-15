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
/// Tests für die strikte Gastfahrer-Name-Validierung in
/// <see cref="AdminLeagueController.SaveEnteredRace"/> / <see cref="AdminLeagueController.UpdateEnteredRace"/>:
/// - Unbekannter Name -> TempData["RaceError"], kein Save.
/// - Exakter Match (case-insensitive) -> Eintrag erlaubt, Name wird kanonisiert.
/// - Stammfahrer ohne DriverProfile -> Eintrag erlaubt (nur Gäste werden validiert).
/// - Tippfehler (Distanz 1) -> kein ExactMatch -> Save abgelehnt.
/// </summary>
public class AdminLeagueControllerGuestValidationTests
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

    private static void SeedLeagueWithStandings(
        SqliteTestContext ctx, string leagueId, params (string Driver, bool IsReserve)[] drivers)
    {
        ctx.Db.Leagues.Add(new League { Id = leagueId, Name = "Testliga" });
        foreach (var (driver, isReserve) in drivers)
        {
            ctx.Db.DriverStandings.Add(new DriverStanding
            {
                LeagueId = leagueId,
                Driver = driver,
                Team = "Ferrari",
                IsReserveDriver = isReserve,
                ReserveForDriver = string.Empty
            });
        }
        ctx.Db.SaveChanges();
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

    // ── SaveEnteredRace ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveEnteredRace_guestNameNotInDriverProfile_returnsTempDataError()
    {
        using var ctx = new SqliteTestContext();
        SeedLeagueWithStandings(ctx, "l1", ("Meier", false));
        SeedProfile(ctx, "Mick Schumacher");

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.SaveEnteredRace(
            leagueId: "l1",
            date: DateTime.UtcNow,
            track: "Spa",
            fastestLapDriver: null,
            positions: new[] { "Hans Müller" },
            raceTimes: null,
            penaltySeconds: null,
            dnfDrivers: null,
            reserveDrivers: null,
            reserveMainDrivers: null,
            guestMainDrivers: new[] { "Meier" });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(ctrl.TempData.ContainsKey("RaceError"));
        var msg = ctrl.TempData["RaceError"] as string;
        Assert.NotNull(msg);
        Assert.Contains("Hans Müller", msg!);
        Assert.Empty(ctx.Db.RaceFinishes);
    }

    [Fact]
    public async Task SaveEnteredRace_guestNameWithTypoExactMatch_returnsTempDataError()
    {
        // "Mick Schumaher" hat Levenshtein 1 zu "Mick Schumacher" -> kein ExactMatch,
        // also lehnt die Server-Validierung ab.
        using var ctx = new SqliteTestContext();
        SeedLeagueWithStandings(ctx, "l1", ("Meier", false));
        SeedProfile(ctx, "Mick Schumacher");

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.SaveEnteredRace(
            leagueId: "l1",
            date: DateTime.UtcNow,
            track: "Spa",
            fastestLapDriver: null,
            positions: new[] { "Mick Schumaher" },
            raceTimes: null,
            penaltySeconds: null,
            dnfDrivers: null,
            reserveDrivers: null,
            reserveMainDrivers: null,
            guestMainDrivers: new[] { "Meier" });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(ctrl.TempData.ContainsKey("RaceError"));
        Assert.Empty(ctx.Db.RaceFinishes);
    }

    [Fact]
    public async Task SaveEnteredRace_guestNameExactMatch_writesCanonicalName()
    {
        using var ctx = new SqliteTestContext();
        SeedLeagueWithStandings(ctx, "l1", ("Meier", false));
        SeedProfile(ctx, "Mick Schumacher");

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.SaveEnteredRace(
            leagueId: "l1",
            date: DateTime.UtcNow,
            track: "Spa",
            fastestLapDriver: null,
            positions: new[] { "mick schumacher" }, // case-insensitive Eingabe
            raceTimes: null,
            penaltySeconds: null,
            dnfDrivers: null,
            reserveDrivers: null,
            reserveMainDrivers: null,
            guestMainDrivers: new[] { "Meier" });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.False(ctrl.TempData.ContainsKey("RaceError"));

        var finish = Assert.Single(ctx.Db.RaceFinishes);
        // RaceFinish wird mit Rohtext geschrieben (Trim). Der kanonisierte Name wird
        // nur fuer Gastfahrer (RaceGuestAssignment) aufgeloest, um Drift zwischen
        // Finish.Driver und DriverProfile.GamerTag zu vermeiden.
        Assert.Equal("mick schumacher", finish.Driver);
        var guest = Assert.Single(ctx.Db.RaceGuestAssignments);
        Assert.Equal("Mick Schumacher", guest.GuestDriver);
        Assert.Equal("Meier", guest.MainDriver);
    }

    [Fact]
    public async Task SaveEnteredRace_stammFahrerInLeagueButNotInDriverProfile_succeeds()
    {
        // Liga-Stammfahrer dürfen weiter eingetragen werden, auch wenn kein DriverProfile
        // existiert. Nur Gastfahrer (Driver außerhalb der Liga-Standings) werden validiert.
        using var ctx = new SqliteTestContext();
        SeedLeagueWithStandings(ctx, "l1", ("StammA", false));

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.SaveEnteredRace(
            leagueId: "l1",
            date: DateTime.UtcNow,
            track: "Spa",
            fastestLapDriver: null,
            positions: new[] { "StammA" },
            raceTimes: null,
            penaltySeconds: null,
            dnfDrivers: null,
            reserveDrivers: null,
            reserveMainDrivers: null,
            guestMainDrivers: null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.False(ctrl.TempData.ContainsKey("RaceError"));
        var finish = Assert.Single(ctx.Db.RaceFinishes);
        Assert.Equal("StammA", finish.Driver);
    }

    [Fact]
    public async Task SaveEnteredRace_noGuestSpecified_succeeds()
    {
        // Reines Liga-Rennen ohne Gäste -> keine Validierung, Save erlaubt.
        using var ctx = new SqliteTestContext();
        SeedLeagueWithStandings(ctx, "l1", ("StammA", false), ("StammB", false));

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.SaveEnteredRace(
            leagueId: "l1",
            date: DateTime.UtcNow,
            track: "Spa",
            fastestLapDriver: null,
            positions: new[] { "StammA", "StammB" },
            raceTimes: new[] { "1:30.000", "1:31.000" },
            penaltySeconds: null,
            dnfDrivers: null,
            reserveDrivers: null,
            reserveMainDrivers: null,
            guestMainDrivers: null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.False(ctrl.TempData.ContainsKey("RaceError"));
        Assert.Equal(2, ctx.Db.RaceFinishes.Count());
    }

    // ── UpdateEnteredRace ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateEnteredRace_guestNameNotInDriverProfile_returnsTempDataError()
    {
        using var ctx = new SqliteTestContext();
        SeedLeagueWithStandings(ctx, "l1", ("Meier", false));
        SeedProfile(ctx, "Mick Schumacher");

        // Seed ein bestehendes Rennen, das überschrieben werden soll.
        var existing = new RaceResult { LeagueId = "l1", Date = DateTime.UtcNow, Track = "Spa" };
        ctx.Db.RaceResults.Add(existing);
        ctx.Db.SaveChanges();

        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.UpdateEnteredRace(
            rowId: existing.RowId,
            leagueId: "l1",
            date: DateTime.UtcNow,
            track: "Spa",
            fastestLapDriver: null,
            positions: new[] { "Hans Müller" },
            raceTimes: null,
            penaltySeconds: null,
            dnfDrivers: null,
            reserveDrivers: null,
            reserveMainDrivers: null,
            guestMainDrivers: new[] { "Meier" });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(ctrl.TempData.ContainsKey("RaceError"));
        // Kein neuer Finish, kein GuestAssignment geschrieben.
        Assert.Empty(ctx.Db.RaceFinishes);
        Assert.Empty(ctx.Db.RaceGuestAssignments);
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