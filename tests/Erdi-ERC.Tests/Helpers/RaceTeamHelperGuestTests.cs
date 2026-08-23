using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erdi_ERC.Tests.Helpers;

/// <summary>
/// Tests für den Cross-League-Gast-Pfad in <see cref="Erdi_ERC.Helpers.RaceTeamHelper"/>:
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

        var team = Erdi_ERC.Helpers.RaceTeamHelper.ResolveTeamForRaceDriver(standings, race, "Gast1");

        Assert.Equal("Ferrari", team);
    }

    [Fact]
    public void ResolveTeamForRaceDriver_guestWithoutAssignment_returnsNull()
    {
        var race = BuildRace();
        // Kein GuestAssignment für "Gast1"
        var standings = new List<DriverStanding> { Standing("StammA", "Ferrari") };

        var team = Erdi_ERC.Helpers.RaceTeamHelper.ResolveTeamForRaceDriver(standings, race, "Gast1");

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

        var team = Erdi_ERC.Helpers.RaceTeamHelper.ResolveTeamForRaceDriver(standings, race, "Gast1");

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

        var team = Erdi_ERC.Helpers.RaceTeamHelper.ResolveTeamForRaceDriver(standings, race, "Multi");

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

        Assert.True(Erdi_ERC.Helpers.RaceTeamHelper.IsRaceGuest(race, "Gast1"));
        Assert.True(Erdi_ERC.Helpers.RaceTeamHelper.IsRaceGuest(race, "gast1")); // case-insensitive
        Assert.False(Erdi_ERC.Helpers.RaceTeamHelper.IsRaceGuest(race, "StammA"));

        race.GuestAssignments.Add(new RaceGuestAssignment
        {
            GuestDriver = "Gast2",
            MainDriver = StatsService.GuestSentinelNoMain
        });
        Assert.False(Erdi_ERC.Helpers.RaceTeamHelper.IsRaceGuest(race, "Gast2"));
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

        var points = Erdi_ERC.Helpers.RaceTeamHelper.ComputeTeamPointsForLeague(league, "Ferrari");

        Assert.Equal(25, points);
    }

    // ── Edge-Cases (Gast-Team-Override-Hardening) ───────────────────────────────

    [Fact]
    public void ResolveTeamForRaceDriver_guestWithMainButMainHasNoTeam_returnsNull()
    {
        // MainDriver ist im Standing, hat aber kein Team → Gast bekommt kein Team.
        // Verhindert Phantom-Punkte in der Liga-Team-Wertung.
        var race = BuildRace();
        race.GuestAssignments.Add(new RaceGuestAssignment
        {
            GuestDriver = "Gast1",
            MainDriver = "StammA"
        });
        var standings = new List<DriverStanding> { Standing("StammA", "") };

        var team = Erdi_ERC.Helpers.RaceTeamHelper.ResolveTeamForRaceDriver(standings, race, "Gast1");

        Assert.Null(team);
    }

    [Fact]
    public void ResolveTeamForRaceDriver_guestWithNonExistentMain_returnsNull()
    {
        // MainDriver ist eingetragen, existiert aber nicht in league.Standings.
        // Pfad 1.5 schlägt fehl → kein Team (Pfad 2 würde auch nichts finden, weil
        // "Gast1" selbst kein Standing hat).
        var race = BuildRace();
        race.GuestAssignments.Add(new RaceGuestAssignment
        {
            GuestDriver = "Gast1",
            MainDriver = "Phantom"
        });
        var standings = new List<DriverStanding> { Standing("StammA", "Ferrari") };

        var team = Erdi_ERC.Helpers.RaceTeamHelper.ResolveTeamForRaceDriver(standings, race, "Gast1");

        Assert.Null(team);
    }

    [Fact]
    public void ResolveTeamForRaceDriver_guestInTwoRacesWithDifferentMains_resolvesPerRace()
    {
        // Derselbe Gast in zwei Rennen mit verschiedenen Hauptfahrern → jedes Rennen
        // bekommt sein eigenes Team. Pfad 1.5 ist pro Rennen gescoped, nicht pro Gast.
        var race1 = BuildRace();
        race1.GuestAssignments.Add(new RaceGuestAssignment
        {
            GuestDriver = "Gast1",
            MainDriver = "StammA"
        });
        var race2 = BuildRace();
        race2.GuestAssignments.Add(new RaceGuestAssignment
        {
            GuestDriver = "Gast1",
            MainDriver = "StammB"
        });
        var standings = new List<DriverStanding>
        {
            Standing("StammA", "Ferrari"),
            Standing("StammB", "Mercedes")
        };

        Assert.Equal("Ferrari", Erdi_ERC.Helpers.RaceTeamHelper.ResolveTeamForRaceDriver(standings, race1, "Gast1"));
        Assert.Equal("Mercedes", Erdi_ERC.Helpers.RaceTeamHelper.ResolveTeamForRaceDriver(standings, race2, "Gast1"));
    }

    [Fact]
    public async Task ComputeTeamPointsForLeague_guestWithSentinel_excludedFromAllTeams()
    {
        // Sentinel-Gast zählt für KEIN Team in der Liga-Wertung — weder für den
        // MainDriver (kein echter Main vorhanden) noch als Phantom.
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

        ctx.Db.RaceFinishes.Add(new RaceFinish { RaceResultId = race.RowId, Driver = "Gast1", Position = 1 });
        ctx.Db.RaceGuestAssignments.Add(new RaceGuestAssignment
        {
            RaceResultId = race.RowId,
            GuestDriver = "Gast1",
            MainDriver = StatsService.GuestSentinelNoMain
        });
        await ctx.Db.SaveChangesAsync();

        using var verify = ctx.NewContext();
        var league = verify.Leagues
            .Include(l => l.Standings)
            .Include(l => l.Races).ThenInclude(r => r.Finishes)
            .Include(l => l.Races).ThenInclude(r => r.ReserveAssignments)
            .Include(l => l.Races).ThenInclude(r => r.GuestAssignments)
            .Single();

        Assert.Equal(0, Erdi_ERC.Helpers.RaceTeamHelper.ComputeTeamPointsForLeague(league, "Ferrari"));
    }
}
