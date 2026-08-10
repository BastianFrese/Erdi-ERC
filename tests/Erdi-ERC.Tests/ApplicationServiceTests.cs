using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Services;
using <OWNER_HANDLE>_ERC.Tests.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace <OWNER_HANDLE>_ERC.Tests;

/// <summary>
/// Tests für <see cref="ApplicationService"/>: Submit (Stamm/Ersatz/Probe/Warteliste),
/// Accept (transaktional: DriverProfile + DriverGamerTag + DriverStanding), Reject,
/// Waitlist-Promotion und Discord-Join-Warnung Snapshot.
/// </summary>
public class ApplicationServiceTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static ApplicationService BuildService(
        SqliteTestContext ctx,
        IAdminAuditService? audit = null,
        IWebhookAutomationService? webhookAuto = null,
        IStaticDataCache? cache = null)
    {
        var memCache = new MemoryCache(new MemoryCacheOptions());
        cache ??= new StaticDataCache(ctx.Db, memCache);
        audit ??= new NoopAuditService();
        webhookAuto ??= new NoopWebhookService();
        return new ApplicationService(
            ctx.Db, audit, webhookAuto, cache,
            NullLogger<ApplicationService>.Instance);
    }

    private static League NewLeague(string id, string name, int? capacity = null)
        => new()
        {
            Id = id, Name = name, Capacity = capacity,
            CountsTowardOverall = true,
            SortOrder = 0,
        };

    private static SubmitApplicationCommand Cmd(string discordId = "111", string tag = "Racer1",
        string leagueId = "pro", string role = "Stammfahrer", string? motivation = null,
        bool warn = false, string? warnDetail = null)
        => new(
            DiscordId: discordId,
            DiscordName: discordId,
            GamerTag: tag,
            Platform: "PC",
            TargetLeagueId: leagueId,
            Role: role,
            Motivation: motivation,
            DiscordJoinWarning: warn,
            DiscordJoinWarningDetail: warnDetail);

    // ── Submit ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_createsApplication_forStammfahrerOnOpenLeague()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.SubmitAsync(Cmd(leagueId: "pro"), CancellationToken.None);

        Assert.Equal(SubmitOutcome.Submitted, result.Outcome);
        Assert.NotNull(result.Application);
        Assert.Equal("Racer1", result.Application!.GamerTag);
        Assert.Equal((int)ApplicationStatus.Pending, result.Application.Status);
        Assert.Equal("Stammfahrer", result.Application.Role);
    }

    [Fact]
    public async Task Submit_routesToWaitlist_whenStammCapacityReached()
    {
        using var ctx = new SqliteTestContext();
        var league = NewLeague("pro", "ProLiga", capacity: 1);
        ctx.Db.Leagues.Add(league);
        ctx.Db.DriverStandings.Add(new DriverStanding
        {
            LeagueId = "pro", Driver = "ExistingPro", Position = 0,
            Points = 0, IsReserveDriver = false
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.SubmitAsync(Cmd(leagueId: "pro"), CancellationToken.None);

        Assert.Equal(SubmitOutcome.Waitlisted, result.Outcome);
        Assert.NotNull(result.WaitlistEntry);
        Assert.Equal(1, result.WaitlistEntry!.Position);
        Assert.Null(result.Application);
    }

    [Fact]
    public async Task Submit_doesNotRouteToWaitlist_forErsatzfahrer()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga", capacity: 0));
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.SubmitAsync(Cmd(leagueId: "pro", role: "Ersatzfahrer"), CancellationToken.None);

        Assert.Equal(SubmitOutcome.Submitted, result.Outcome);
        Assert.NotNull(result.Application);
        Assert.Equal("Ersatzfahrer", result.Application!.Role);
    }

    [Fact]
    public async Task Submit_returnsAlreadyPending_whenOpenAppExists()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        await svc.SubmitAsync(Cmd(discordId: "111", leagueId: "pro"), CancellationToken.None);
        var second = await svc.SubmitAsync(Cmd(discordId: "111", leagueId: "pro"), CancellationToken.None);

        Assert.Equal(SubmitOutcome.AlreadyPending, second.Outcome);
        Assert.NotNull(second.Application);
    }

    [Fact]
    public async Task Submit_returnsAlreadyWaitlisted_whenOpenWaitlistExists()
    {
        using var ctx = new SqliteTestContext();
        var league = NewLeague("pro", "ProLiga", capacity: 0);
        ctx.Db.Leagues.Add(league);
        ctx.Db.DriverStandings.Add(new DriverStanding
        {
            LeagueId = "pro", Driver = "Full", Position = 0,
            Points = 0, IsReserveDriver = false
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        await svc.SubmitAsync(Cmd(discordId: "111", leagueId: "pro"), CancellationToken.None);
        var second = await svc.SubmitAsync(Cmd(discordId: "111", leagueId: "pro"), CancellationToken.None);

        Assert.Equal(SubmitOutcome.AlreadyWaitlisted, second.Outcome);
        Assert.NotNull(second.WaitlistEntry);
    }

    [Fact]
    public async Task Submit_throws_whenLeagueMissing()
    {
        using var ctx = new SqliteTestContext();
        var svc = BuildService(ctx);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SubmitAsync(Cmd(leagueId: "nope"), CancellationToken.None));
    }

    [Fact]
    public async Task Submit_throws_whenLeagueIsArchived()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "old", Name = "OldLiga", IsArchived = true });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SubmitAsync(Cmd(leagueId: "old"), CancellationToken.None));
    }

    [Fact]
    public async Task Submit_persistsDiscordWarningSnapshot()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.SubmitAsync(
            Cmd(leagueId: "pro", warn: true, warnDetail: "Bewerber ist nicht im Liga-Discord"),
            CancellationToken.None);

        Assert.True(result.Application!.DiscordJoinWarning);
        Assert.Equal("Bewerber ist nicht im Liga-Discord", result.Application.DiscordJoinWarningDetail);
    }

    // ── Accept ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Accept_createsDriverProfileAndGamerTag()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        ctx.Db.Applications.Add(new Application
        {
            Id = "app1", DiscordId = "111", DiscordName = "111", GamerTag = "Racer1",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Status = (int)ApplicationStatus.Pending
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.AcceptAsync("app1", "admin1", "Welcome", CancellationToken.None);

        Assert.Equal(AcceptRejectOutcome.Ok, result.Outcome);
        Assert.NotNull(result.Application);
        Assert.Equal((int)ApplicationStatus.Accepted, result.Application!.Status);
        Assert.Equal("admin1", result.Application.DecidedByDiscordId);
        Assert.NotNull(result.Application.DecidedAt);

        var profile = ctx.Db.DriverProfiles.SingleOrDefault(p => p.DiscordId == "111");
        Assert.NotNull(profile);
        var tag = ctx.Db.DriverGamerTags.SingleOrDefault(t => t.DiscordId == "111" && t.GamerTag == "Racer1");
        Assert.NotNull(tag);
    }

    [Fact]
    public async Task Accept_createsDriverStanding_forStammfahrer()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        ctx.Db.Applications.Add(new Application
        {
            Id = "app1", DiscordId = "111", DiscordName = "111", GamerTag = "Racer1",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Status = (int)ApplicationStatus.Pending
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        await svc.AcceptAsync("app1", "admin1", null, CancellationToken.None);

        var standing = ctx.Db.DriverStandings.SingleOrDefault(s => s.LeagueId == "pro" && s.Driver == "Racer1");
        Assert.NotNull(standing);
        Assert.False(standing!.IsReserveDriver);
    }

    [Fact]
    public async Task Accept_marksDriverStandingAsReserve_whenRoleIsErsatzfahrer()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        ctx.Db.Applications.Add(new Application
        {
            Id = "app1", DiscordId = "111", DiscordName = "111", GamerTag = "R1",
            Platform = "PC", TargetLeagueId = "pro", Role = "Ersatzfahrer",
            Status = (int)ApplicationStatus.Pending
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        await svc.AcceptAsync("app1", "admin1", null, CancellationToken.None);

        var standing = ctx.Db.DriverStandings.Single();
        Assert.True(standing.IsReserveDriver);
    }

    [Fact]
    public async Task Accept_marksDriverStandingAsReserve_whenRoleIsReservefahrer_legacy()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        ctx.Db.Applications.Add(new Application
        {
            Id = "app1", DiscordId = "111", DiscordName = "111", GamerTag = "R1",
            Platform = "PC", TargetLeagueId = "pro", Role = "Reservefahrer",
            Status = (int)ApplicationStatus.Pending
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        await svc.AcceptAsync("app1", "admin1", null, CancellationToken.None);

        var standing = ctx.Db.DriverStandings.Single();
        Assert.True(standing.IsReserveDriver);
    }

    [Fact]
    public async Task Accept_returnsNotFound_whenApplicationMissing()
    {
        using var ctx = new SqliteTestContext();
        var svc = BuildService(ctx);
        var result = await svc.AcceptAsync("nonexistent", "admin1", null, CancellationToken.None);

        Assert.Equal(AcceptRejectOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Accept_savesReviewNote_forUserVisibility()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        ctx.Db.Applications.Add(new Application
        {
            Id = "app1", DiscordId = "111", DiscordName = "111", GamerTag = "R1",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Status = (int)ApplicationStatus.Pending
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        await svc.AcceptAsync("app1", "admin1", "Schön dich dabei zu haben", CancellationToken.None);

        var app = ctx.Db.Applications.Single();
        Assert.Equal("Schön dich dabei zu haben", app.ReviewNote);
    }

    // ── Reject ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reject_setsStatusAndReviewNote()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        ctx.Db.Applications.Add(new Application
        {
            Id = "app1", DiscordId = "111", DiscordName = "111", GamerTag = "R1",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Status = (int)ApplicationStatus.Pending
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.RejectAsync("app1", "admin1", "Bitte später erneut bewerben", CancellationToken.None);

        Assert.Equal(AcceptRejectOutcome.Ok, result.Outcome);
        Assert.Equal((int)ApplicationStatus.Rejected, result.Application!.Status);
        Assert.Equal("Bitte später erneut bewerben", result.Application.ReviewNote);
    }

    [Fact]
    public async Task Reject_doesNotCreateDriverProfile()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        ctx.Db.Applications.Add(new Application
        {
            Id = "app1", DiscordId = "111", DiscordName = "111", GamerTag = "R1",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Status = (int)ApplicationStatus.Pending
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        await svc.RejectAsync("app1", "admin1", null, CancellationToken.None);

        Assert.Empty(ctx.Db.DriverProfiles);
        Assert.Empty(ctx.Db.DriverGamerTags);
    }

    // ── Waitlist + Promote ─────────────────────────────────────────────────────

    [Fact]
    public async Task ListWaitlistAsync_returnsEntriesForLeague()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        ctx.Db.Leagues.Add(NewLeague("otherliga", "OtherLiga"));
        ctx.Db.WaitlistEntries.Add(new WaitlistEntry
        {
            Id = "w1", DiscordId = "111", DiscordName = "111", GamerTag = "R1",
            Platform = "PC", LeagueId = "pro", Position = 1
        });
        ctx.Db.WaitlistEntries.Add(new WaitlistEntry
        {
            Id = "w2", DiscordId = "222", DiscordName = "222", GamerTag = "R2",
            Platform = "PC", LeagueId = "otherliga", Position = 1
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var entries = await svc.ListWaitlistAsync("pro", CancellationToken.None);

        Assert.Single(entries);
        Assert.Equal("w1", entries[0].Id);
    }

    [Fact]
    public async Task PromoteFromWaitlistAsync_copiesEntryToApplication()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        ctx.Db.WaitlistEntries.Add(new WaitlistEntry
        {
            Id = "w1", DiscordId = "111", DiscordName = "User1", GamerTag = "R1",
            Platform = "PC", LeagueId = "pro", Position = 1
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.PromoteFromWaitlistAsync("w1", "admin1", CancellationToken.None);

        Assert.Equal(PromoteOutcome.Ok, result.Outcome);
        Assert.NotNull(result.Application);
        Assert.Equal("111", result.Application!.DiscordId);
        Assert.Equal("pro", result.Application.TargetLeagueId);
        Assert.Equal((int)ApplicationStatus.Pending, result.Application.Status);

        var entry = ctx.Db.WaitlistEntries.Single(e => e.Id == "w1");
        Assert.Equal(result.Application.Id, entry.PromotedToApplicationId);
    }

    [Fact]
    public async Task PromoteFromWaitlistAsync_returnsNotFound_whenMissing()
    {
        using var ctx = new SqliteTestContext();
        var svc = BuildService(ctx);
        var result = await svc.PromoteFromWaitlistAsync("nonexistent", "admin1", CancellationToken.None);

        Assert.Equal(PromoteOutcome.NotFound, result.Outcome);
    }

    // ── List / GetById / HasOpen ───────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_returnsPaginated_filteredByStatus()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
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

        var svc = BuildService(ctx);
        var pending = await svc.ListAsync(ApplicationStatus.Pending, null, 0, 50, CancellationToken.None);

        Assert.Single(pending);
        Assert.Equal("a1", pending[0].Id);
    }

    [Fact]
    public async Task HasOpenApplicationAsync_returnsTrueForPending_only()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        ctx.Db.Applications.Add(new Application
        {
            Id = "a1", DiscordId = "1", DiscordName = "1", GamerTag = "T1",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Status = (int)ApplicationStatus.Rejected
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var hasOpen = await svc.HasOpenApplicationAsync("1", "pro", CancellationToken.None);
        Assert.False(hasOpen);
    }

    [Fact]
    public async Task GetByIdAsync_returnsNull_whenMissing()
    {
        using var ctx = new SqliteTestContext();
        var svc = BuildService(ctx);
        var app = await svc.GetByIdAsync("nope", CancellationToken.None);
        Assert.Null(app);
    }

    // ── ManualRegister ─────────────────────────────────────────────────────────

    private static ManualRegisterCommand ManualCmd(string discordId = "222", string tag = "ManualRacer",
        string leagueId = "pro", string role = "Stammfahrer")
        => new(
            DiscordId: discordId,
            DiscordName: "ManualUser",
            GamerTag: tag,
            Platform: "PC",
            LeagueId: leagueId,
            Role: role);

    [Fact]
    public async Task ManualRegister_createsProfileTagAndStanding()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.ManualRegisterAsync(ManualCmd(), "admin1", CancellationToken.None);

        Assert.Equal(ManualRegisterOutcome.Ok, result.Outcome);
        Assert.NotNull(ctx.Db.DriverProfiles.SingleOrDefault(p => p.DiscordId == "222"));
        Assert.NotNull(ctx.Db.DriverGamerTags.SingleOrDefault(t => t.DiscordId == "222" && t.GamerTag == "ManualRacer"));
        var standing = ctx.Db.DriverStandings.SingleOrDefault(s => s.LeagueId == "pro" && s.Driver == "ManualRacer");
        Assert.NotNull(standing);
        Assert.False(standing!.IsReserveDriver);
    }

    [Fact]
    public async Task ManualRegister_marksReserve_forErsatzfahrer()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.ManualRegisterAsync(ManualCmd(role: "Ersatzfahrer"), "admin1", CancellationToken.None);

        Assert.Equal(ManualRegisterOutcome.Ok, result.Outcome);
        Assert.True(ctx.Db.DriverStandings.Single().IsReserveDriver);
    }

    [Fact]
    public async Task ManualRegister_returnsLeagueNotFound_forUnknownLeague()
    {
        using var ctx = new SqliteTestContext();
        var svc = BuildService(ctx);
        var result = await svc.ManualRegisterAsync(ManualCmd(leagueId: "nope"), "admin1", CancellationToken.None);

        Assert.Equal(ManualRegisterOutcome.LeagueNotFound, result.Outcome);
        Assert.Empty(ctx.Db.DriverProfiles);
    }

    [Fact]
    public async Task ManualRegister_returnsAlreadyRegistered_andWritesNothing_whenStandingExists()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        ctx.Db.DriverStandings.Add(new DriverStanding
        {
            LeagueId = "pro", Driver = "ManualRacer", Position = 0,
            Points = 0, IsReserveDriver = false
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.ManualRegisterAsync(ManualCmd(), "admin1", CancellationToken.None);

        Assert.Equal(ManualRegisterOutcome.AlreadyRegistered, result.Outcome);
        Assert.Empty(ctx.Db.DriverProfiles);
        Assert.Empty(ctx.Db.DriverGamerTags);
        Assert.Single(ctx.Db.DriverStandings);
    }

    [Fact]
    public async Task ManualRegister_bypassesCapacity()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga", capacity: 1));
        ctx.Db.DriverStandings.Add(new DriverStanding
        {
            LeagueId = "pro", Driver = "ExistingPro", Position = 0,
            Points = 0, IsReserveDriver = false
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.ManualRegisterAsync(ManualCmd(), "admin1", CancellationToken.None);

        Assert.Equal(ManualRegisterOutcome.Ok, result.Outcome);
        Assert.Equal(2, ctx.Db.DriverStandings.Count());
    }

    // ── Test Doubles ───────────────────────────────────────────────────────────

    private sealed class NoopAuditService : IAdminAuditService
    {
        public Task LogAsync(string action, string entityType, string entityId, string details)
            => Task.CompletedTask;
        public Task LogAndSaveAsync(string action, string entityType, string entityId, string details)
            => Task.CompletedTask;
    }

    private sealed class NoopWebhookService : IWebhookAutomationService
    {
        public Task FireAsync(string eventType, Dictionary<string, string> vars)
            => Task.CompletedTask;
        public Task<(bool Success, string? Error)> TestRuleAsync(int ruleId, Dictionary<string, string> vars, CancellationToken ct = default)
            => Task.FromResult((true, (string?)null));
    }
}