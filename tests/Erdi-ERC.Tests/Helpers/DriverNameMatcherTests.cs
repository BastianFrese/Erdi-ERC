using Erdi_ERC.Helpers;
using Xunit;

namespace Erdi_ERC.Tests.Helpers;

/// <summary>
/// Chat-Namen sind kürzer und fehlerhafter als die gespeicherten Standings-Namen.
/// Der Matcher muss „wiener" auf „ERC | Wiener" abbilden, bei Mehrdeutigkeit aber
/// ablehnen — niemals einen zufälligen Fahrer wählen.
/// </summary>
public class DriverNameMatcherTests
{
    [Theory]
    [InlineData("ERC | Müller", "mueller")]
    [InlineData("  Max   Wiener ", "max wiener")]
    [InlineData("ERC |", "")]
    [InlineData("Élodie", "elodie")]
    public void Normalize_entfernt_clantag_umlaute_und_leerzeichen(string input, string expected)
        => Assert.Equal(expected, DriverNameMatcher.Normalize(input));

    [Fact]
    public void Match_findet_exakten_anzeigenamen()
    {
        var r = DriverNameMatcher.Match("Wiener", new[] { "ERC | Wiener", "ERC | Müller" });

        Assert.Equal("ERC | Wiener", r.Driver);
        Assert.False(r.Ambiguous);
    }

    [Fact]
    public void Match_findet_einen_Namensbestandteil()
    {
        var r = DriverNameMatcher.Match(
            "wiener", new[] { "ERC | Max Wiener", "ERC | Max Berger" });

        Assert.Equal("ERC | Max Wiener", r.Driver);
        Assert.False(r.Ambiguous);
    }

    [Fact]
    public void Match_findet_ein_eindeutiges_Teilwort()
    {
        var r = DriverNameMatcher.Match("wien", new[] { "ERC | Wiener", "ERC | Müller" });

        Assert.Equal("ERC | Wiener", r.Driver);
        Assert.False(r.Ambiguous);
    }

    [Fact]
    public void Match_toleriert_einen_tippfehler()
    {
        var r = DriverNameMatcher.Match("wienr", new[] { "ERC | Wiener" });

        Assert.Equal("ERC | Wiener", r.Driver);
    }

    [Fact]
    public void Match_verwirft_mehrdeutige_eingabe()
    {
        var r = DriverNameMatcher.Match("max", new[] { "ERC | Max Wiener", "ERC | Max Berger" });

        Assert.Null(r.Driver);
        Assert.True(r.Ambiguous);
    }

    [Fact]
    public void Match_verwirft_unbekannten_namen()
    {
        var r = DriverNameMatcher.Match("xyz", new[] { "ERC | Wiener" });

        Assert.Null(r.Driver);
        Assert.False(r.Ambiguous);
    }
}
