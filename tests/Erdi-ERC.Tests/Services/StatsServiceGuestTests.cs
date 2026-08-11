using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Services;
using <OWNER_HANDLE>_ERC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace <OWNER_HANDLE>_ERC.Tests.Services;

/// <summary>
/// Tests für den StatsService-Gate: Cross-League-Gäste sollen NICHT als Liga-Standings
/// angelegt werden; Sentinel-Bestandsdaten und fehlende Zuordnungen führen zu
/// Skip im Rebuild-Lauf; Finishes von Gästen zählen nicht für Liga-Standings, aber
/// über die Gast→MainDriver-Zuordnung für Team-Punkte (über <see cref="<OWNER_HANDLE>_ERC.Helpers.RaceTeamHelper"/>).
/// </summary>
public class StatsServiceGuestTests
{
    private static IOptions<<OWNER_HANDLE>_ERC.Options.F1ScoringOptions> F1Scoring() =>
        Microsoft.Extensions.Options.Options.Create(new <OWNER_HANDLE>_ERC.Options.F1ScoringOptions());

    [Fact]
    public async Task RebuildLeagueStandings_guestFinishWithoutAssignment_doesNotCreateStanding()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "l1", Name = "Testliga" });
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l1", Driver = "StammA" });
        await ctx.Db.SaveChangesAsync();

        var race = new RaceResult { LeagueId = "l1", Date = DateTime.UtcNow, Track = "T" };
        ctx.Db.RaceResults.Add(race);
        await ctx.Db.SaveChangesAsync();

        ctx.Db.RaceFinishes.Add(new RaceFinish { RaceResultId = race.RowId, Driver = "GastX", Position = 1 });
        // Kein RaceGuestAssignment → Gast hat keine Zuordnung → Gate ignoriert den Finish.
        await ctx.Db.SaveChangesAsync();

        var service = new StatsService(ctx.Db, F1Scoring());
        await service.RebuildLeagueStandingsAsync("l1");

        await using var verify = ctx.NewContext();
        var gastStanding = await verify.DriverStandings.SingleOrDefaultAsync(s => s.LeagueId == "l1" && s.Driver == "GastX");
        Assert.Null(gastStanding);

        var stamm = await verify.DriverStandings.SingleAsync(s => s.Driver == "StammA");
        Assert.Equal(0, stamm.Points); // StammA ist nicht gefahren
    }

    [Fact]
    public async Task RebuildLeagueStandings_guestFinishWithSentinelAssignment_doesNotCreateStanding()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "l1", Name = "Testliga" });
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l1", Driver = "StammA" });
        await ctx.Db.SaveChangesAsync();

        var race = new RaceResult { LeagueId = "l1", Date = DateTime.UtcNow, Track = "T" };
        ctx.Db.RaceResults.Add(race);
        await ctx.Db.SaveChangesAsync();

        ctx.Db.RaceFinishes.Add(new RaceFinish { RaceResultId = race.RowId, Driver = "GastAlt", Position = 1 });
        ctx.Db.RaceGuestAssignments.Add(new RaceGuestAssignment
        {
            RaceResultId = race.RowId,
            GuestDriver = "GastAlt",
            MainDriver = StatsService.GuestSentinelNoMain
        });
        await ctx.Db.SaveChangesAsync();

        var service = new StatsService(ctx.Db, F1Scoring());
        await service.RebuildLeagueStandingsAsync("l1");

        await using var verify = ctx.NewContext();
        var gastStanding = await verify.DriverStandings.SingleOrDefaultAsync(s => s.Driver == "GastAlt");
        Assert.Null(gastStanding);
    }

    [Fact]
    public async Task RebuildLeagueStandings_guestFinishWithValidMain_doesNotCreateStanding_butMainTeamInheritsPoints()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "l1", Name = "Testliga" });
        ctx.Db.DriverStandings.Add(new DriverStanding
        {
            LeagueId = "l1",
            Driver = "StammA",
            Team = "Ferrari"
        });
        await ctx.Db.SaveChangesAsync();

        var race = new RaceResult { LeagueId = "l1", Date = DateTime.UtcNow, Track = "T" };
        ctx.Db.RaceResults.Add(race);
        await ctx.Db.SaveChangesAsync();

        ctx.Db.RaceFinishes.Add(new RaceFinish { RaceResultId = race.RowId, Driver = "Gast1", Position = 1 }); // 25 Pkt
        ctx.Db.RaceGuestAssignments.Add(new RaceGuestAssignment
        {
            RaceResultId = race.RowId,
            GuestDriver = "Gast1",
            MainDriver = "StammA"
        });
        await ctx.Db.SaveChangesAsync();

        var service = new StatsService(ctx.Db, F1Scoring());
        await service.RebuildLeagueStandingsAsync("l1");

        await using var verify = ctx.NewContext();
        // Gast bekommt KEIN eigenes Standing
        var gastStanding = await verify.DriverStandings.SingleOrDefaultAsync(s => s.Driver == "Gast1");
        Assert.Null(gastStanding);

        // StammA bleibt im Standing mit 0 Punkten (kein eigener Finish), aber
        // die Team-Punkte aggregieren den Gast-Finish (25 Pkt für Ferrari).
        var league = await verify.Leagues
            .Include(l => l.Standings)
            .Include(l => l.Races).ThenInclude(r => r.Finishes)
            .Include(l => l.Races).ThenInclude(r => r.ReserveAssignments)
            .Include(l => l.Races).ThenInclude(r => r.GuestAssignments)
            .SingleAsync();

        var teamPoints = <OWNER_HANDLE>_ERC.Helpers.RaceTeamHelper.ComputeTeamPointsForLeague(league, "Ferrari");
        Assert.Equal(25, teamPoints);
    }
}
