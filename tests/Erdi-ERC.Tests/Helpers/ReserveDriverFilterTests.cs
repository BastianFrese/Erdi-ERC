using Erdi_ERC.Helpers;
using Erdi_ERC.Models;

namespace Erdi_ERC.Tests.Helpers;

/// <summary>
/// Ersatzfahrer dürfen in der Results-Tabelle erst auftauchen, wenn sie in der Liga
/// tatsächlich ein Rennen gefahren sind — auch ein DNF zählt als gefahren.
/// </summary>
public class ReserveDriverFilterTests
{
    private static League BuildLeague(params (string Driver, bool IsReserve, int Position)[] drivers)
    {
        var league = new League { Id = "l1", Name = "Liga 1" };
        foreach (var (driver, isReserve, position) in drivers)
        {
            league.Standings.Add(new DriverStanding
            {
                LeagueId = "l1",
                Driver = driver,
                IsReserveDriver = isReserve,
                Position = position,
                Team = "T"
            });
        }
        return league;
    }

    private static void AddRace(League league, params (string Driver, int Position)[] finishes)
    {
        var race = new RaceResult { RowId = league.Races.Count + 1, LeagueId = league.Id, Track = "Test", Date = new DateTime(2026, 8, 1) };
        foreach (var (driver, position) in finishes)
        {
            race.Finishes.Add(new RaceFinish { Driver = driver, Position = position });
        }
        league.Races.Add(race);
    }

    [Fact]
    public void VisibleStandings_hidesReserveWithoutAnyRace()
    {
        var league = BuildLeague(("Stamm", false, 1), ("Ersatz", true, 2));
        AddRace(league, ("Stamm", 1));

        var visible = ReserveDriverFilter.VisibleStandings(league);

        Assert.Equal(new[] { "Stamm" }, visible.Select(s => s.Driver));
    }

    [Fact]
    public void VisibleStandings_keepsMainDriverWithoutAnyRace()
    {
        var league = BuildLeague(("Stamm ohne Rennen", false, 1));

        var visible = ReserveDriverFilter.VisibleStandings(league);

        Assert.Equal(new[] { "Stamm ohne Rennen" }, visible.Select(s => s.Driver));
    }

    [Fact]
    public void VisibleStandings_showsReserveAfterDnf()
    {
        var league = BuildLeague(("Ersatz", true, 1));
        // Position 0 = DNF: gefahren, aber nicht ins Ziel gekommen.
        AddRace(league, ("Ersatz", 0));

        var visible = ReserveDriverFilter.VisibleStandings(league);

        Assert.Equal(new[] { "Ersatz" }, visible.Select(s => s.Driver));
    }

    [Fact]
    public void VisibleStandings_isScopedToTheLeague()
    {
        var league = BuildLeague(("Ersatz", true, 1));
        AddRace(league, ("Jemand anderes", 1));

        Assert.Empty(ReserveDriverFilter.VisibleStandings(league));
    }

    [Fact]
    public void VisibleStandings_matchesNameCaseInsensitivelyAndTrimmed()
    {
        var league = BuildLeague(("  ERC | Ersatz  ", true, 1));
        AddRace(league, ("erc | ersatz", 3));

        var visible = ReserveDriverFilter.VisibleStandings(league);

        Assert.Single(visible);
    }

    [Fact]
    public void VisibleStandings_ordersByPosition()
    {
        var league = BuildLeague(("Zweiter", false, 2), ("Erster", false, 1), ("Ersatz", true, 3));
        AddRace(league, ("Erster", 1), ("Ersatz", 2));

        var visible = ReserveDriverFilter.VisibleStandings(league);

        Assert.Equal(new[] { "Erster", "Zweiter", "Ersatz" }, visible.Select(s => s.Driver));
    }

    [Fact]
    public void HasRaced_isFalseForEmptyName()
    {
        var league = BuildLeague(("Ersatz", true, 1));
        AddRace(league, ("Ersatz", 1));

        Assert.False(ReserveDriverFilter.HasRaced(league, null));
        Assert.False(ReserveDriverFilter.HasRaced(league, "   "));
        Assert.True(ReserveDriverFilter.HasRaced(league, "Ersatz"));
    }
}
