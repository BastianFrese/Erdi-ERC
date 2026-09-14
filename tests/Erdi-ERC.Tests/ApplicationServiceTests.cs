using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erdi_ERC.Tests;

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
        IStaticDataCache? cache = null,
        IApplicationTargetingService? targeting = null)
    {
        var memCache = new MemoryCache(new MemoryCacheOptions());
        cache ??= new StaticDataCache(ctx.Db, memCache);
        audit ??= new NoopAuditService();
        webhookAuto ??= new NoopWebhookService();
        targeting ??= new ApplicationTargetingService(ctx.Db, cache);
        return new ApplicationService(
            ctx.Db, audit, webhookAuto, cache, targeting,
            NullLogger<ApplicationService>.Instance);
    }

    private static League NewLeague(string id, string name, int? capacity = null,
        string? currentSeason = null, string? nextSeason = null, bool openForNext = false)
        => new()
        {
            Id = id, Name = name, Capacity = capacity,
            CountsTowardOverall = true,
            SortOrder = 0,
            CurrentSeason = currentSeason,
            NextSeason = nextSeason,
            ApplicationsOpenForNextSeason = openForNext,
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
            Platform = "PC", LeagueId = "pro", Season = "2026", Position = 1
        });
        ctx.Db.WaitlistEntries.Add(new WaitlistEntry
        {
            Id = "w2", DiscordId = "222", DiscordName = "222", GamerTag = "R2",
            Platform = "PC", LeagueId = "otherliga", Season = "2026", Position = 1
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var entries = await svc.ListWaitlistAsync("pro", null, CancellationToken.None);

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
            Id = "w1", DiscordId = "111", DiscordName = "User1", GamerTag = "R1", Season = "2026",
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
            Id = "a1", DiscordId = "1", DiscordName = "1", GamerTag = "T1", Season = "2026",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Status = (int)ApplicationStatus.Pending
        });
        ctx.Db.Applications.Add(new Application
        {
            Id = "a2", DiscordId = "2", DiscordName = "2", GamerTag = "T2", Season = "2026",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Status = (int)ApplicationStatus.Rejected
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var pending = await svc.ListAsync(ApplicationStatus.Pending, null, null, 0, 50, CancellationToken.None);

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
            Id = "a1", DiscordId = "1", DiscordName = "1", GamerTag = "T1", Season = "2026",
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

        // EA-Name (GamerTag) ist auch der sichtbare DisplayName des Profils.
        Assert.Equal("ManualRacer", ctx.Db.DriverProfiles.Single(p => p.DiscordId == "222").DisplayName);
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
    public async Task ManualRegister_existingProfileWithoutDisplayName_getsEnteredEaName()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        ctx.Db.DriverProfiles.Add(new DriverProfile
        {
            DiscordId = "222",
            DiscordName = "ManualUser",
            GamerTags = new List<DriverGamerTag>
            {
                new() { DiscordId = "222", Platform = "PC", GamerTag = "OldTag", IsPrimary = true }
            }
        });

        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.ManualRegisterAsync(ManualCmd(), "admin1", CancellationToken.None);

        Assert.Equal(ManualRegisterOutcome.Ok, result.Outcome);
        var profile = ctx.Db.DriverProfiles.Single(p => p.DiscordId == "222");
        Assert.Equal("ManualRacer", profile.DisplayName);

        // Tag-Regel: ein Tag pro Plattform — kein zweiter PC-Tag, kein Rename.
        var tags = ctx.Db.DriverGamerTags.Where(t => t.DiscordId == "222").ToList();
        Assert.Single(tags);
        Assert.Equal("OldTag", tags[0].GamerTag);

        // Standing unter dem eingegebenen EA-Namen angelegt.
        Assert.NotNull(ctx.Db.DriverStandings.SingleOrDefault(
            s => s.Driver == "ManualRacer" && s.LeagueId == "pro"));
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

    // ── Self-Service: Withdraw / LeaveWaitlist / ListMine ──────────────────────

    [Fact]
    public async Task Withdraw_deletesOwnPendingApplication()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        ctx.Db.Applications.Add(new Application
        {
            Id = "a1", DiscordId = "111", DiscordName = "111", GamerTag = "R1", Season = "2026",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Status = (int)ApplicationStatus.Pending
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.WithdrawAsync("a1", "111", CancellationToken.None);

        Assert.Equal(WithdrawOutcome.Ok, result.Outcome);
        Assert.Empty(ctx.Db.Applications);
    }

    [Fact]
    public async Task Withdraw_returnsNotOwner_forForeignApplication()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        ctx.Db.Applications.Add(new Application
        {
            Id = "a1", DiscordId = "999", DiscordName = "999", GamerTag = "R1", Season = "2026",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Status = (int)ApplicationStatus.Pending
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.WithdrawAsync("a1", "111", CancellationToken.None);

        Assert.Equal(WithdrawOutcome.NotOwner, result.Outcome);
        Assert.Single(ctx.Db.Applications);
    }

    [Fact]
    public async Task Withdraw_returnsNotPending_forDecidedApplication()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        ctx.Db.Applications.Add(new Application
        {
            Id = "a1", DiscordId = "111", DiscordName = "111", GamerTag = "R1", Season = "2026",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Status = (int)ApplicationStatus.Accepted
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.WithdrawAsync("a1", "111", CancellationToken.None);

        Assert.Equal(WithdrawOutcome.NotPending, result.Outcome);
        Assert.Single(ctx.Db.Applications);
    }

    [Fact]
    public async Task LeaveWaitlist_removesEntry_andRenumbersFollowers()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        ctx.Db.WaitlistEntries.AddRange(
            new WaitlistEntry { Id = "w1", DiscordId = "1", DiscordName = "1", GamerTag = "A", Platform = "PC", LeagueId = "pro", Season = "2026", Position = 1 },
            new WaitlistEntry { Id = "w2", DiscordId = "2", DiscordName = "2", GamerTag = "B", Platform = "PC", LeagueId = "pro", Season = "2026", Position = 2 },
            new WaitlistEntry { Id = "w3", DiscordId = "3", DiscordName = "3", GamerTag = "C", Platform = "PC", LeagueId = "pro", Position = 3 });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.LeaveWaitlistAsync("w1", "1", CancellationToken.None);

        Assert.Equal(WithdrawOutcome.Ok, result.Outcome);
        var remaining = ctx.Db.WaitlistEntries.OrderBy(w => w.Position).ToList();
        Assert.Equal(2, remaining.Count);
        Assert.Equal(1, remaining.First(w => w.Id == "w2").Position);
        Assert.Equal(2, remaining.First(w => w.Id == "w3").Position);
    }

    [Fact]
    public async Task LeaveWaitlist_returnsNotOwner_forForeignEntry()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        ctx.Db.WaitlistEntries.Add(new WaitlistEntry
        {
            Id = "w1", DiscordId = "999", DiscordName = "999", GamerTag = "A", Season = "2026",
            Platform = "PC", LeagueId = "pro", Position = 1
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.LeaveWaitlistAsync("w1", "111", CancellationToken.None);

        Assert.Equal(WithdrawOutcome.NotOwner, result.Outcome);
        Assert.Single(ctx.Db.WaitlistEntries);
    }

    [Fact]
    public async Task ListMine_returnsOnlyOwnApplications_newestFirst()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        ctx.Db.Applications.AddRange(
            new Application { Id = "a1", DiscordId = "111", DiscordName = "111", GamerTag = "R1", Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer", Season = "2026", CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new Application { Id = "a2", DiscordId = "111", DiscordName = "111", GamerTag = "R1", Platform = "PC", TargetLeagueId = "pro", Role = "Ersatzfahrer", Season = "2026", CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new Application { Id = "x1", DiscordId = "999", DiscordName = "999", GamerTag = "R9", Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer" });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var mine = await svc.ListMineAsync("111", CancellationToken.None);

        Assert.Equal(2, mine.Count);
        Assert.Equal("a2", mine[0].Id);
        Assert.NotNull(mine[0].TargetLeague);
    }

    [Fact]
    public async Task ListMyWaitlist_excludesPromotedEntries()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga"));
        ctx.Db.WaitlistEntries.AddRange(
            new WaitlistEntry { Id = "w1", DiscordId = "111", DiscordName = "111", GamerTag = "A", Platform = "PC", LeagueId = "pro", Season = "2026", Position = 1, PromotedToApplicationId = "appX" },
            new WaitlistEntry { Id = "w2", DiscordId = "111", DiscordName = "111", GamerTag = "A", Platform = "PC", LeagueId = "pro", Season = "2026", Position = 2 });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var mine = await svc.ListMyWaitlistAsync("111", CancellationToken.None);

        var entry = Assert.Single(mine);
        Assert.Equal("w2", entry.Id);
    }

    // ── Kapazitäts-Infos + AcceptsApplications ─────────────────────────────────

    [Fact]
    public async Task GetLeagueCapacity_countsStammAndWaitlist()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga", capacity: 2));
        ctx.Db.DriverStandings.AddRange(
            new DriverStanding { LeagueId = "pro", Driver = "A", IsReserveDriver = false },
            new DriverStanding { LeagueId = "pro", Driver = "B", IsReserveDriver = true });
        ctx.Db.WaitlistEntries.Add(new WaitlistEntry
        {
            Id = "w1", DiscordId = "1", DiscordName = "1", GamerTag = "C", Season = "2026",
            Platform = "PC", LeagueId = "pro", Position = 1
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var infos = await svc.GetLeagueCapacityAsync(CancellationToken.None);

        var info = Assert.Single(infos);
        Assert.Equal(1, info.OccupiedSeats);   // Reserve zählt nicht
        Assert.Equal(1, info.WaitlistLength);
        Assert.False(info.IsFull);
        Assert.Equal(1, info.FreeSeats);
    }

    [Fact]
    public async Task Submit_throws_whenLeagueClosedForApplications()
    {
        using var ctx = new SqliteTestContext();
        var league = NewLeague("pro", "ProLiga");
        league.AcceptsApplications = false;
        ctx.Db.Leagues.Add(league);
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SubmitAsync(Cmd(leagueId: "pro"), CancellationToken.None));
    }

    // ── Season-Awareness ────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_persistsLeagueCurrentSeason_whenNoNextSeason()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga", currentSeason: "2026"));
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.SubmitAsync(Cmd(leagueId: "pro"), CancellationToken.None);

        Assert.Equal(SubmitOutcome.Submitted, result.Outcome);
        var app = ctx.Db.Applications.Single();
        Assert.Equal("2026", app.Season);
    }

    [Fact]
    public async Task Submit_persistsNextSeason_whenApplicationsOpenForNextSeason()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga",
            currentSeason: "2026", nextSeason: "2027", openForNext: true));
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.SubmitAsync(Cmd(leagueId: "pro"), CancellationToken.None);

        Assert.Equal(SubmitOutcome.Submitted, result.Outcome);
        var app = ctx.Db.Applications.Single();
        Assert.Equal("2027", app.Season);
    }

    [Fact]
    public async Task Submit_dedupScopedBySeason_allowsSameUserInMultipleSeasons()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga", currentSeason: "2026"));
        ctx.Db.Applications.Add(new Application
        {
            Id = "old", DiscordId = "111", DiscordName = "111", GamerTag = "R1",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Season = "2025", Status = (int)ApplicationStatus.Pending
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.SubmitAsync(Cmd(leagueId: "pro"), CancellationToken.None);

        Assert.Equal(SubmitOutcome.Submitted, result.Outcome);
        Assert.Equal(2, ctx.Db.Applications.Count());
    }

    [Fact]
    public async Task Submit_capacityCountedPerSeason_notTotalStandings()
    {
        using var ctx = new SqliteTestContext();
        // Liga mit 1 Stammfahrer in DriverStandings (aus alter Season) → Capacity = 1 → voll
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga", currentSeason: "2026", capacity: 1));
        ctx.Db.DriverStandings.Add(new DriverStanding
        {
            LeagueId = "pro", Driver = "OldDriver", Position = 0, Points = 0,
            IsReserveDriver = false
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.SubmitAsync(Cmd(leagueId: "pro"), CancellationToken.None);

        // Stamm-Slot durch historicalStandings belegt → Waitlist
        Assert.Equal(SubmitOutcome.Waitlisted, result.Outcome);
        Assert.Single(ctx.Db.WaitlistEntries);
    }

    [Fact]
    public async Task GetSeasonSummaryAsync_groupsBySeasonPerLeague()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga", currentSeason: "2026"));
        ctx.Db.Applications.Add(new Application
        {
            Id = "a1", DiscordId = "1", DiscordName = "1", GamerTag = "A",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Season = "2026", Status = (int)ApplicationStatus.Pending
        });
        ctx.Db.Applications.Add(new Application
        {
            Id = "a2", DiscordId = "2", DiscordName = "2", GamerTag = "B",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Season = "2026", Status = (int)ApplicationStatus.Accepted
        });
        ctx.Db.Applications.Add(new Application
        {
            Id = "a3", DiscordId = "3", DiscordName = "3", GamerTag = "C",
            Platform = "PC", TargetLeagueId = "pro", Role = "Ersatzfahrer",
            Season = "2026", Status = (int)ApplicationStatus.Rejected
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var rows = await svc.GetSeasonSummaryAsync(season: "2026", CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal("2026", row.Season);
        Assert.Equal("pro", row.LeagueId);
        Assert.Equal(3, row.PendingCount + row.AcceptedCount + row.RejectedCount);
        Assert.True(row.PendingCount >= 1);
        Assert.True(row.AcceptedCount >= 1);
        Assert.True(row.RejectedCount >= 1);
    }

    [Fact]
    public async Task CloseSeasonAsync_rejectsAllPending_whenRejectAllMode()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga", currentSeason: "2026"));
        ctx.Db.Applications.Add(new Application
        {
            Id = "a1", DiscordId = "1", DiscordName = "1", GamerTag = "A",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Season = "2026", Status = (int)ApplicationStatus.Pending
        });
        ctx.Db.Applications.Add(new Application
        {
            Id = "a2", DiscordId = "2", DiscordName = "2", GamerTag = "B",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Season = "2026", Status = (int)ApplicationStatus.Pending
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.CloseSeasonAsync("pro", "2026", "2027", SeasonCloseMode.RejectAll, "admin1", CancellationToken.None);

        Assert.Equal(CloseSeasonOutcome.Ok, result.Outcome);
        Assert.Equal(2, result.RejectedApplications);
        Assert.All(ctx.Db.Applications, a => Assert.Equal((int)ApplicationStatus.Rejected, a.Status));
    }

    [Fact]
    public async Task CloseSeasonAsync_rollsOver_movesOpenAppsToTargetSeason()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga", currentSeason: "2026"));
        ctx.Db.Applications.Add(new Application
        {
            Id = "a1", DiscordId = "1", DiscordName = "1", GamerTag = "A",
            Platform = "PC", TargetLeagueId = "pro", Role = "Stammfahrer",
            Season = "2026", Status = (int)ApplicationStatus.Pending
        });
        await ctx.Db.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.CloseSeasonAsync("pro", "2026", "2027", SeasonCloseMode.Rollover, "admin1", CancellationToken.None);

        Assert.Equal(CloseSeasonOutcome.Ok, result.Outcome);
        Assert.Equal(1, result.MovedApplications);
        var app = ctx.Db.Applications.Single();
        Assert.Equal("2027", app.Season);
        Assert.Equal((int)ApplicationStatus.Pending, app.Status);
    }

    [Fact]
    public async Task Targeting_info_returnsAllSeasonsForAllAcceptedLeagues()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga", currentSeason: "2026"));
        ctx.Db.Leagues.Add(NewLeague("pro2", "ProLiga2", currentSeason: "2027"));
        await ctx.Db.SaveChangesAsync();

        var targeting = new ApplicationTargetingService(ctx.Db,
            new StaticDataCache(ctx.Db, new MemoryCache(new MemoryCacheOptions())));
        var info = await targeting.GetTargetingInfoAsync(CancellationToken.None);

        Assert.Equal(2, info.Count);
        Assert.Contains(info, t => t.LeagueId == "pro" && t.TargetSeason == "2026" && !t.IsNextSeason);
        Assert.Contains(info, t => t.LeagueId == "pro2" && t.TargetSeason == "2027" && !t.IsNextSeason);
    }

    [Fact]
    public async Task Targeting_info_marksNextSeason_whenOpenForNext()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(NewLeague("pro", "ProLiga",
            currentSeason: "2026", nextSeason: "2027", openForNext: true));
        await ctx.Db.SaveChangesAsync();

        var targeting = new ApplicationTargetingService(ctx.Db,
            new StaticDataCache(ctx.Db, new MemoryCache(new MemoryCacheOptions())));
        var info = await targeting.GetTargetingInfoAsync(CancellationToken.None);

        var entry = Assert.Single(info);
        Assert.Equal("2027", entry.TargetSeason);
        Assert.True(entry.IsNextSeason);
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