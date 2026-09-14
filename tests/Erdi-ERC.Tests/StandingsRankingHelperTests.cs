using Erdi_ERC.Helpers;
using Erdi_ERC.Models;
using Xunit;

namespace Erdi_ERC.Tests;

/// <summary>
/// Unit-Tests für den F1-Tiebreaker der Meisterschafts-Sortierung:
/// Bei gleicher Punktzahl entscheidet, wer die meisten besseren Positionen hat
/// (meiste 1. Plätze, dann meiste 2., dann 3., …), zuletzt der Name.
/// </summary>
public class StandingsRankingHelperTests
{
    private static DriverStanding Standing(string driver, int points) =>
        new() { Driver = driver, Points = points };

    private static RaceFinish Finish(string driver, int position) =>
        new() { Driver = driver, Position = position };

    [Fact]
    public void Rank_samePoints_moreFirstPlacesWins()
    {
        // Arrange — beide 50 Punkte, aber Alice hat 2× P1, Bob nur 1× P1 + 1× P2.
        var standings = new List<DriverStanding> { Standing("Bob", 50), Standing("Alice", 50) };
        var counts = StandingsRankingHelper.BuildPositionCounts(new[]
        {
            Finish("Alice", 1), Finish("Alice", 1),
            Finish("Bob", 1), Finish("Bob", 2),
        });

        // Act
        var ranked = StandingsRankingHelper.Rank(standings, counts);

        // Assert
        Assert.Equal("Alice", ranked[0].Driver);
        Assert.Equal("Bob", ranked[1].Driver);
    }

    [Fact]
    public void Rank_samePointsAndFirstPlaces_moreSecondPlacesWins()
    {
        // Arrange — beide 50 Punkte und 1× P1, aber Alice hat zusätzlich 1× P2, Bob 1× P3.
        var standings = new List<DriverStanding> { Standing("Bob", 50), Standing("Alice", 50) };
        var counts = StandingsRankingHelper.BuildPositionCounts(new[]
        {
            Finish("Alice", 1), Finish("Alice", 2),
            Finish("Bob", 1), Finish("Bob", 3),
        });

        // Act
        var ranked = StandingsRankingHelper.Rank(standings, counts);

        // Assert
        Assert.Equal("Alice", ranked[0].Driver);
        Assert.Equal("Bob", ranked[1].Driver);
    }

    [Fact]
    public void Rank_samePointsNoTopPositions_nameDecides()
    {
        // Arrange — beide 10 Punkte, keine P1/P2/P3; Name entscheidet (ordinal, case-insensitive).
        var standings = new List<DriverStanding> { Standing("Zulu", 10), Standing("Alpha", 10) };
        var counts = StandingsRankingHelper.BuildPositionCounts(new[]
        {
            Finish("Zulu", 4), Finish("Alpha", 4),
        });

        // Act
        var ranked = StandingsRankingHelper.Rank(standings, counts);

        // Assert
        Assert.Equal("Alpha", ranked[0].Driver);
        Assert.Equal("Zulu", ranked[1].Driver);
    }

    [Fact]
    public void Rank_differentPoints_pointsDecideFirst()
    {
        // Arrange — Bob hat mehr Punkte, obwohl Alice mehr P1 hat. Punkte gehen vor.
        var standings = new List<DriverStanding> { Standing("Bob", 60), Standing("Alice", 50) };
        var counts = StandingsRankingHelper.BuildPositionCounts(new[]
        {
            Finish("Alice", 1), Finish("Alice", 1),
            Finish("Bob", 2), Finish("Bob", 2),
        });

        // Act
        var ranked = StandingsRankingHelper.Rank(standings, counts);

        // Assert
        Assert.Equal("Bob", ranked[0].Driver);
        Assert.Equal("Alice", ranked[1].Driver);
    }

    [Fact]
    public void BuildPositionCounts_countsPerPosition_caseInsensitive()
    {
        // Arrange — "alice" und "ALICE" zählen zusammen; Position 0 und > MaxPositions werden ignoriert.
        var finishes = new[]
        {
            Finish("alice", 1), Finish("ALICE", 1), Finish("alice", 2),
            Finish("Bob", 0), Finish("Bob", 21),
        };

        // Act
        var counts = StandingsRankingHelper.BuildPositionCounts(finishes);

        // Assert
        Assert.True(counts.ContainsKey("alice"));
        Assert.Equal(2, counts["alice"][0]); // 2× P1
        Assert.Equal(1, counts["alice"][1]); // 1× P2
        Assert.False(counts.ContainsKey("Bob")); // nur Position 0 / > 20 → kein Eintrag
    }

    [Fact]
    public void Rank_driverWithoutFinishes_ranksAfterDriversWithFinishes()
    {
        // Arrange — "NoRace" hat 50 Punkte, aber keine Finishes (z.B. Punkte-Übernahme).
        var standings = new List<DriverStanding> { Standing("NoRace", 50), Standing("Alice", 50) };
        var counts = StandingsRankingHelper.BuildPositionCounts(new[]
        {
            Finish("Alice", 1), Finish("Alice", 1),
        });

        // Act
        var ranked = StandingsRankingHelper.Rank(standings, counts);

        // Assert — Alice (2× P1) vor NoRace (keine Positionen).
        Assert.Equal("Alice", ranked[0].Driver);
        Assert.Equal("NoRace", ranked[1].Driver);
    }
}
