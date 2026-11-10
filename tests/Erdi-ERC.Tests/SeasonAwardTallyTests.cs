using Erdi_ERC.Models;
using Xunit;

namespace Erdi_ERC.Tests;

/// <summary>
/// Auswertung der Season-Awards-Stimmen. Die Regel: pro Liga gewinnt die höchste
/// Stimmenzahl; ein Gleichstand wird nicht stillschweigend entschieden, sondern
/// als Gleichstand ausgewiesen (mehrere Gewinner).
/// </summary>
public class SeasonAwardTallyTests
{
    private static SeasonAwardNomination Vote(string league, string driver, string user) =>
        new() { LeagueId = league, Driver = driver, DiscordId = user };

    [Fact]
    public void Winners_mostVotes_wins()
    {
        // Arrange — Max 2 Stimmen, Lena 1.
        var votes = new[]
        {
            Vote("f1", "Max", "u1"),
            Vote("f1", "Max", "u2"),
            Vote("f1", "Lena", "u3"),
        };

        // Act
        var winners = SeasonAwardTally.Winners(votes);

        // Assert
        var winner = Assert.Single(winners);
        Assert.Equal("Max", winner.Driver);
        Assert.Equal(2, winner.Votes);
        Assert.False(winner.Tied);
    }

    [Fact]
    public void Winners_tie_returnsBothAndMarksTied()
    {
        // Arrange — je eine Stimme für beide.
        var votes = new[]
        {
            Vote("f1", "Max", "u1"),
            Vote("f1", "Lena", "u2"),
        };

        // Act
        var winners = SeasonAwardTally.Winners(votes);

        // Assert
        Assert.Equal(2, winners.Count);
        Assert.All(winners, w => Assert.True(w.Tied));
        Assert.Contains(winners, w => w.Driver == "Max");
        Assert.Contains(winners, w => w.Driver == "Lena");
    }

    [Fact]
    public void Winners_groupsPerLeague()
    {
        // Arrange — Max gewinnt in f1, Lena in gt3.
        var votes = new[]
        {
            Vote("f1", "Max", "u1"),
            Vote("f1", "Max", "u2"),
            Vote("gt3", "Lena", "u3"),
        };

        // Act
        var winners = SeasonAwardTally.Winners(votes);

        // Assert
        Assert.Equal(2, winners.Count);
        Assert.Equal("Max", winners.Single(w => w.LeagueId == "f1").Driver);
        Assert.Equal("Lena", winners.Single(w => w.LeagueId == "gt3").Driver);
    }

    [Fact]
    public void Winners_sameNameDifferentCase_countsTogether()
    {
        // Arrange — Schreibweisen-Wechsel darf nicht zu zwei Einzelstimmen führen.
        var votes = new[]
        {
            Vote("f1", "ERC | Max", "u1"),
            Vote("f1", "erc | max", "u2"),
        };

        // Act
        var winners = SeasonAwardTally.Winners(votes);

        // Assert
        var winner = Assert.Single(winners);
        Assert.Equal(2, winner.Votes);
    }

    [Fact]
    public void Winners_noVotes_returnsEmpty()
    {
        Assert.Empty(SeasonAwardTally.Winners(Array.Empty<SeasonAwardNomination>()));
    }
}
