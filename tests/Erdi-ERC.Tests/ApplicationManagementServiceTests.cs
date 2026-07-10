using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static <OWNER_HANDLE>_ERC.Tests.Infrastructure.ApplicationServiceTestHarness;

namespace <OWNER_HANDLE>_ERC.Tests;

/// <summary>
/// Integrationstests für <c>ApplicationManagementService</c> auf SQLite-In-Memory
/// (echte Transaktionen). Decken die Audit-Persistenz, den Standing-Cleanup bei
/// Override-Liga und den transaktionalen Liga-Zuweisungs-Flow ab.
/// </summary>
public class ApplicationManagementServiceTests
{
    // ── Audit-Persistenz ─────────────────────────────────────────────────────

    [Fact]
    public async Task RejectApplication_persists_audit_log_and_state()
    {
        using var ctx = new SqliteTestContext();
        var app = SeedApplication(ctx.Db, gamingName: "RejectMe");
        var service = CreateService(ctx.Db);

        var result = await service.RejectApplicationAsync(app.Id, "Spam", "admin-1");

        Assert.True(result.Success);

        // Frischer Context: prüft echte DB-Persistenz, nicht nur den ChangeTracker.
        using var verify = ctx.NewContext();
        var audit = await verify.AdminAuditLogs
            .SingleOrDefaultAsync(x => x.Action == "RejectApplication" && x.EntityId == app.Id.ToString());
        Assert.NotNull(audit);

        var reloaded = await verify.ApplicationForms.FindAsync(app.Id);
        Assert.True(reloaded!.IsRejected);
    }

    [Fact]
    public async Task FlagForReview_persists_audit_log()
    {
        using var ctx = new SqliteTestContext();
        var app = SeedApplication(ctx.Db, gamingName: "FlagMe");
        var service = CreateService(ctx.Db);

        var result = await service.FlagForReviewAsync(app.Id, "Verdächtig", "admin-1");

        Assert.True(result.Success);
        using var verify = ctx.NewContext();
        Assert.True(await verify.AdminAuditLogs.AnyAsync(x => x.Action == "FlagApplicationForReview"));
        Assert.True((await verify.ApplicationForms.FindAsync(app.Id))!.IsFlagged);
    }

    // ── Standing-Cleanup bei Override-Liga (HIGH-Bug-Fix) ────────────────────

    [Fact]
    public async Task Accept_with_override_league_creates_standing_in_override()
    {
        using var ctx = new SqliteTestContext();
        var applied = SeedLeague(ctx.Db, "applied", "Rookie Crossplay Division 3");
        var over = SeedLeague(ctx.Db, "override", "Main Division 1");
        var app = SeedApplication(ctx.Db, gamingName: "Speedy", appliedLeagueId: applied.Id);
        var service = CreateService(ctx.Db);

        var result = await service.AcceptApplicationAsync(app.Id, "admin-1", overrideLeagueId: over.Id);

        Assert.True(result.Success);
        using var verify = ctx.NewContext();
        Assert.True(await verify.DriverStandings.AnyAsync(s => s.LeagueId == over.Id && s.Driver == "Speedy"));
        Assert.False(await verify.DriverStandings.AnyAsync(s => s.LeagueId == applied.Id));
    }

    [Fact]
    public async Task Unaccept_removes_orphan_standing_from_override_league()
    {
        using var ctx = new SqliteTestContext();
        var applied = SeedLeague(ctx.Db, "applied", "Rookie Crossplay Division 3");
        var over = SeedLeague(ctx.Db, "override", "Main Division 1");
        var app = SeedApplication(ctx.Db, gamingName: "Speedy", appliedLeagueId: applied.Id);
        var service = CreateService(ctx.Db);

        await service.AcceptApplicationAsync(app.Id, "admin-1", overrideLeagueId: over.Id);

        // Vorbedingung: Standing existiert in der Override-Liga.
        using (var pre = ctx.NewContext())
            Assert.True(await pre.DriverStandings.AnyAsync(s => s.LeagueId == over.Id));

        var unaccept = await service.UnacceptApplicationAsync(app.Id, "Fehler", "admin-1");

        Assert.True(unaccept.Success);
        using var verify = ctx.NewContext();
        // Der leere Auto-Standing muss auch aus der Override-Liga verschwinden.
        Assert.Equal(0, await verify.DriverStandings.CountAsync(s => s.LeagueId == over.Id));
        Assert.False((await verify.ApplicationForms.FindAsync(app.Id))!.IsAccepted);
    }

    [Fact]
    public async Task Unaccept_keeps_standing_with_real_results()
    {
        using var ctx = new SqliteTestContext();
        var league = SeedLeague(ctx.Db, "l1", "Main Division 1");
        var app = SeedApplication(ctx.Db, gamingName: "Champion", appliedLeagueId: league.Id);
        var service = CreateService(ctx.Db);

        await service.AcceptApplicationAsync(app.Id, "admin-1");

        // Standing bekommt echte Ergebnisse → darf beim Unaccept NICHT gelöscht werden.
        using (var seed = ctx.NewContext())
        {
            var standing = await seed.DriverStandings.SingleAsync(s => s.LeagueId == league.Id);
            standing.Points = 25;
            standing.Wins = 1;
            await seed.SaveChangesAsync();
        }

        await service.UnacceptApplicationAsync(app.Id, "Versehen", "admin-1");

        using var verify = ctx.NewContext();
        Assert.True(await verify.DriverStandings.AnyAsync(s => s.LeagueId == league.Id && s.Points == 25));
    }

    // ── Transaktionale Liga-Zuweisung ────────────────────────────────────────

    [Fact]
    public async Task AssignToLeague_creates_standing_updates_division_and_audits()
    {
        using var ctx = new SqliteTestContext();
        var league = SeedLeague(ctx.Db, "l1", "Main Division 1");
        var app = SeedApplication(ctx.Db, gamingName: "Verstappen", isAccepted: true);
        var service = CreateService(ctx.Db);

        var result = await service.AssignToLeagueAsync(app.Id, league.Id, "Stammfahrer", "admin-1");

        Assert.True(result.Success);
        using var verify = ctx.NewContext();
        var standing = await verify.DriverStandings.SingleOrDefaultAsync(s => s.LeagueId == league.Id);
        Assert.NotNull(standing);
        Assert.Equal("Verstappen", standing!.Driver);

        var reloaded = await verify.ApplicationForms.FindAsync(app.Id);
        Assert.Equal("Main Division 1", reloaded!.Division);
        Assert.Equal("Stammfahrer", reloaded.AssignedRole);
        Assert.True(await verify.AdminAuditLogs.AnyAsync(x => x.Action == "AssignApplicationToLeague"));
    }

    [Fact]
    public async Task AssignToLeague_fails_for_archived_league()
    {
        using var ctx = new SqliteTestContext();
        var league = SeedLeague(ctx.Db, "l1", "Archiviert", isArchived: true);
        var app = SeedApplication(ctx.Db, gamingName: "NoGo", isAccepted: true);
        var service = CreateService(ctx.Db);

        var result = await service.AssignToLeagueAsync(app.Id, league.Id, "Stammfahrer", "admin-1");

        Assert.False(result.Success);
        Assert.Equal("LEAGUE_NOT_FOUND", result.ErrorCode);
    }

    // ── IsAccepted-Lebenszyklus (muss IMMER funktionieren) ───────────────────

    [Fact]
    public async Task Accept_sets_IsAccepted_and_persists()
    {
        using var ctx = new SqliteTestContext();
        var league = SeedLeague(ctx.Db, "l1", "Main Division 1");
        var app = SeedApplication(ctx.Db, gamingName: "Lifecycle", appliedLeagueId: league.Id);
        var service = CreateService(ctx.Db);

        var result = await service.AcceptApplicationAsync(app.Id, "admin-1");

        Assert.True(result.Success);
        using var verify = ctx.NewContext();
        var reloaded = await verify.ApplicationForms.FindAsync(app.Id);
        Assert.Equal(ApplicationStatus.Accepted, reloaded!.Status);
        Assert.True(reloaded.IsAccepted);
        Assert.False(reloaded.IsRejected);
        Assert.NotNull(reloaded.AcceptedAt);
    }

    [Fact]
    public async Task Accept_after_reject_clears_rejected_state()
    {
        using var ctx = new SqliteTestContext();
        var league = SeedLeague(ctx.Db, "l1", "Main Division 1");
        var app = SeedApplication(ctx.Db, gamingName: "Comeback", appliedLeagueId: league.Id, isRejected: true);
        var service = CreateService(ctx.Db);

        var result = await service.AcceptApplicationAsync(app.Id, "admin-1");

        Assert.True(result.Success);
        using var verify = ctx.NewContext();
        var reloaded = await verify.ApplicationForms.FindAsync(app.Id);
        // Der frühere Doppel-Flag-Zustand (akzeptiert UND abgelehnt) ist unmöglich:
        Assert.Equal(ApplicationStatus.Accepted, reloaded!.Status);
        Assert.True(reloaded.IsAccepted);
        Assert.False(reloaded.IsRejected);
        Assert.Null(reloaded.RejectedAt);
    }

    [Fact]
    public async Task Accept_via_assign_role_sets_IsAccepted_and_role()
    {
        // Deckt den "Rolle zuweisen"-Button ab: AdminApplicationsController.AssignRole
        // ruft für nicht-akzeptierte Bewerbungen genau diesen Service-Pfad auf.
        using var ctx = new SqliteTestContext();
        var league = SeedLeague(ctx.Db, "l1", "Main Division 1");
        var app = SeedApplication(ctx.Db, gamingName: "RoleFirst");
        var service = CreateService(ctx.Db);

        var result = await service.AcceptApplicationAsync(
            app.Id, "admin-1", overrideLeagueId: league.Id, assignedRole: "Stammfahrer");

        Assert.True(result.Success);
        using var verify = ctx.NewContext();
        var reloaded = await verify.ApplicationForms.FindAsync(app.Id);
        Assert.Equal(ApplicationStatus.Accepted, reloaded!.Status);
        Assert.True(reloaded.IsAccepted);
        Assert.Equal("Stammfahrer", reloaded.AssignedRole);
        Assert.NotNull(reloaded.AcceptedAt);
    }

    [Fact]
    public async Task GetAllApplications_accepted_filter_translates_to_sql_and_filters()
    {
        using var ctx = new SqliteTestContext();
        SeedApplication(ctx.Db, gamingName: "Acc", discordId: "d-a", isAccepted: true);
        SeedApplication(ctx.Db, gamingName: "Open", discordId: "d-o");
        SeedApplication(ctx.Db, gamingName: "Rej", discordId: "d-r", isRejected: true);
        var service = CreateService(ctx.Db);

        var accepted = await service.GetAllApplicationsAsync(acceptedFilter: true);
        var notAccepted = await service.GetAllApplicationsAsync(acceptedFilter: false);
        var withoutRejected = await service.GetAllApplicationsAsync(excludeRejected: true);

        Assert.Single(accepted);
        Assert.True(accepted[0].IsAccepted);
        Assert.Equal(2, notAccepted.Count);
        Assert.DoesNotContain(notAccepted, x => x.IsAccepted);
        Assert.Equal(2, withoutRejected.Count);
        Assert.DoesNotContain(withoutRejected, x => x.IsRejected);
    }

    // ── IsRejected-Lebenszyklus ──────────────────────────────────────────────

    [Fact]
    public async Task Reject_sets_status_clears_flag_and_appends_reason()
    {
        using var ctx = new SqliteTestContext();
        var app = SeedApplication(ctx.Db, gamingName: "RejectFlow", isFlagged: true);
        var service = CreateService(ctx.Db);

        var result = await service.RejectApplicationAsync(app.Id, "Kein Platz", "admin-1");

        Assert.True(result.Success);
        using var verify = ctx.NewContext();
        var reloaded = await verify.ApplicationForms.FindAsync(app.Id);
        Assert.Equal(ApplicationStatus.Rejected, reloaded!.Status);
        Assert.True(reloaded.IsRejected);
        Assert.False(reloaded.IsAccepted);
        Assert.NotNull(reloaded.RejectedAt);
        Assert.False(reloaded.IsFlagged);
        Assert.Contains("Kein Platz", reloaded.ReviewNote);
    }

    [Fact]
    public async Task Reject_accepted_application_is_blocked()
    {
        using var ctx = new SqliteTestContext();
        var app = SeedApplication(ctx.Db, gamingName: "Untouchable", isAccepted: true);
        var service = CreateService(ctx.Db);

        var result = await service.RejectApplicationAsync(app.Id, "Versehen", "admin-1");

        Assert.False(result.Success);
        Assert.Equal("ALREADY_ACCEPTED", result.ErrorCode);
        using var verify = ctx.NewContext();
        // Akzeptierte Bewerbung bleibt akzeptiert — der alte Id-14-Zustand kann nicht entstehen.
        Assert.Equal(ApplicationStatus.Accepted, (await verify.ApplicationForms.FindAsync(app.Id))!.Status);
    }

    [Fact]
    public async Task Unreject_returns_application_to_open()
    {
        using var ctx = new SqliteTestContext();
        var app = SeedApplication(ctx.Db, gamingName: "SecondChance", isRejected: true);
        var service = CreateService(ctx.Db);

        var result = await service.UnrejectApplicationAsync(app.Id, "Doch nochmal prüfen", "admin-1");

        Assert.True(result.Success);
        using var verify = ctx.NewContext();
        var reloaded = await verify.ApplicationForms.FindAsync(app.Id);
        Assert.Equal(ApplicationStatus.Open, reloaded!.Status);
        Assert.False(reloaded.IsRejected);
        Assert.Null(reloaded.RejectedAt);
        Assert.True(await verify.AdminAuditLogs.AnyAsync(x => x.Action == "UnrejectApplication"));
    }

    [Fact]
    public async Task Unreject_blocked_when_other_active_application_exists()
    {
        using var ctx = new SqliteTestContext();
        var rejected = SeedApplication(ctx.Db, gamingName: "OldTry", discordId: "dup-1", isRejected: true);
        SeedApplication(ctx.Db, gamingName: "NewTry", discordId: "dup-1");
        var service = CreateService(ctx.Db);

        var result = await service.UnrejectApplicationAsync(rejected.Id, null, "admin-1");

        // Würde sonst am ActiveDiscordKey-Unique-Index scheitern → sauberer Fehler vorab.
        Assert.False(result.Success);
        Assert.Equal("DUPLICATE_ACTIVE", result.ErrorCode);
        using var verify = ctx.NewContext();
        Assert.Equal(ApplicationStatus.Rejected, (await verify.ApplicationForms.FindAsync(rejected.Id))!.Status);
    }

    // ── Statistik-Aggregation (1 Round-Trip) ─────────────────────────────────

    [Fact]
    public async Task GetStatistics_counts_each_status_correctly()
    {
        using var ctx = new SqliteTestContext();
        SeedApplication(ctx.Db, gamingName: "A", discordId: "d-a", isAccepted: true);
        SeedApplication(ctx.Db, gamingName: "B", discordId: "d-b", isRejected: true);
        SeedApplication(ctx.Db, gamingName: "C", discordId: "d-c", isFlagged: true);
        SeedApplication(ctx.Db, gamingName: "D", discordId: "d-d");
        var service = CreateService(ctx.Db);

        var stats = await service.GetStatisticsAsync();

        Assert.Equal(4, stats.TotalApplications);
        Assert.Equal(1, stats.AcceptedApplications);
        Assert.Equal(1, stats.RejectedApplications);
        Assert.Equal(1, stats.FlaggedForReviewApplications);
        Assert.Equal(2, stats.OpenApplications); // C (flagged) + D (pending) sind offen
    }

    [Fact]
    public async Task GetStatistics_on_empty_db_returns_zeroes()
    {
        using var ctx = new SqliteTestContext();
        var service = CreateService(ctx.Db);

        var stats = await service.GetStatisticsAsync();

        Assert.Equal(0, stats.TotalApplications);
        Assert.Null(stats.LastApplicationTime);
    }
}
