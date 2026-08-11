using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Services;
using <OWNER_HANDLE>_ERC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace <OWNER_HANDLE>_ERC.Tests.Helpers;

/// <summary>
/// Tests für den Cross-League-Gast-Pfad in <see cref="<OWNER_HANDLE>_ERC.Helpers.RaceTeamHelper"/>:
/// Gastfahrer mit Liga-Hauptfahrer-Zuordnung erbt das Team des MainDrivers; ohne
/// Zuordnung oder mit Sentinel wird kein Team aufgelöst.
/// </summary>
public class RaceTeamHelperGuestTests
{
    private static DriverStanding Standing(string driver, string? team = null, bool isReserve = false, string? reserveFor = null) => new()
    {
        Driver = driver,
        Team = team ?? string.Empty,
        IsReserveDriver = isReserve,
        ReserveForDriver = reserveFor ?? string.Empty
    };

    private static RaceResult BuildRace(params RaceFinish[] finishes) => new()
    {
        LeagueId = "l1",
        Date = DateTime.UtcNow,
        Track = "Test",
        Finishes = finishes.ToList()
    };

    [Fact]
    public void ResolveTeamForRaceDriver_guestWithMainAssignment_returnsMainTeam()
    {
        var race = BuildRace();
        race.GuestAssignments.Add(new RaceGuestAssignment
        {
            GuestDriver = "Gast1",
            MainDriver = "StammA"
        });
        var standings = new List<DriverStanding> { Standing("StammA", "Ferrari") };

        var team = <OWNER_HANDLE>_ERC.Helpers.RaceTeamHelper.ResolveTeamForRaceDriver(standings, race, "Gast1");

        Assert.Equal("Ferrari", team);
    }

    [Fact]
    public void ResolveTeamForRaceDriver_guestWithoutAssignment_returnsNull()
    {
        var race = BuildRace();
        // Kein GuestAssignment für "Gast1"
        var standings = new List<DriverStanding> { Standing("StammA", "Ferrari") };

        var team = <OWNER_HANDLE>_ERC.Helpers.RaceTeamHelper.ResolveTeamForRaceDriver(standings, race, "Gast1");

        Assert.Null(team);
    }

    [Fact]
    public void ResolveTeamForRaceDriver_guestWithSentinelMain_returnsNull()
    {
        var race = BuildRace();
        race.GuestAssignments.Add(new RaceGuestAssignment
        {
            GuestDriver = "Gast1",
            MainDriver = StatsService.GuestSentinelNoMain
        });
        var standings = new List<DriverStanding> { Standing("StammA", "Ferrari") };

        var team = <OWNER_HANDLE>_ERC.Helpers.RaceTeamHelper.ResolveTeamForRaceDriver(standings, race, "Gast1");

        Assert.Null(team);
    }

    [Fact]
    public void ResolveTeamForRaceDriver_reserveTakesPriorityOverGuest()
    {
        // Wenn der Fahrer sowohl ReserveAssignment als auch GuestAssignment hätte (Defensive-Doppelung),
        // gewinnt der Reserve-Pfad, weil er zuerst evaluiert wird.
        var race = BuildRace();
        race.ReserveAssignments.Add(new RaceReserveAssignment
        {
            ReserveDriver = "Multi",
            MainDriver = "ReserveMain"
        });
        race.GuestAssignments.Add(new RaceGuestAssignment
        {
            GuestDriver = "Multi",
            MainDriver = "GuestMain"
        });
        var standings = new List<DriverStanding>
        {
            Standing("ReserveMain", "ReserveTeam"),
            Standing("GuestMain", "GuestTeam")
        };

        var team = <OWNER_HANDLE>_ERC.Helpers.RaceTeamHelper.ResolveTeamForRaceDriver(standings, race, "Multi");

        Assert.Equal("ReserveTeam", team);
    }

    [Fact]
    public void IsRaceGuest_returnsTrueOnlyWithValidAssignment()
    {
        var race = BuildRace();
        race.GuestAssignments.Add(new RaceGuestAssignment
        {
            GuestDriver = "Gast1",
            MainDriver = "StammA"
        });

        Assert.True(<OWNER_HANDLE>_ERC.Helpers.RaceTeamHelper.IsRaceGuest(race, "Gast1"));
        Assert.True(<OWNER_HANDLE>_ERC.Helpers.RaceTeamHelper.IsRaceGuest(race, "gast1")); // case-insensitive
        Assert.False(<OWNER_HANDLE>_ERC.Helpers.RaceTeamHelper.IsRaceGuest(race, "StammA"));

        race.GuestAssignments.Add(new RaceGuestAssignment
        {
            GuestDriver = "Gast2",
            MainDriver = StatsService.GuestSentinelNoMain
        });
        Assert.False(<OWNER_HANDLE>_ERC.Helpers.RaceTeamHelper.IsRaceGuest(race, "Gast2"));
    }

    [Fact]
    public async Task ComputeTeamPointsForLeague_aggregatesGuestFinishesOntoMainTeam()
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

        // Re-Load mit Includes
        using var verify = ctx.NewContext();
        var league = verify.Leagues
            .Include(l => l.Standings)
            .Include(l => l.Races).ThenInclude(r => r.Finishes)
            .Include(l => l.Races).ThenInclude(r => r.ReserveAssignments)
            .Include(l => l.Races).ThenInclude(r => r.GuestAssignments)
            .Single();

        var points = <OWNER_HANDLE>_ERC.Helpers.RaceTeamHelper.ComputeTeamPointsForLeague(league, "Ferrari");

        Assert.Equal(25, points);
    }
}
