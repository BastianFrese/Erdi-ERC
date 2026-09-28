using Erdi_ERC.Helpers;
using Xunit;

namespace Erdi_ERC.Tests;

/// <summary>
/// Sichert <see cref="PointsFormatHelper"/> ab — die einzige Stelle, die Punkte formatiert
/// und parst. Kritisch ist das Parsen: <c>"12,5"</c> muss 12,5 ergeben. Genau hier entstand
/// der Ewige-Liste-Bug, bei dem der alte <c>char.IsDigit</c>-Filter daraus 125 machte.
/// </summary>
public class PointsFormatHelperTests
{
    [Theory]
    [InlineData(12.5, "12,5")]
    [InlineData(18.75, "18,75")]
    [InlineData(25, "25")]
    [InlineData(0, "0")]
    [InlineData(33.5, "33,5")]
    [InlineData(-5, "-5")]
    public void Format_usesGermanDecimalCommaWithoutTrailingZeros(decimal value, string expected)
    {
        Assert.Equal(expected, PointsFormatHelper.Format(value));
    }

    [Fact]
    public void Format_nullable_rendersDashWhenMissing()
    {
        Assert.Equal("-", PointsFormatHelper.Format(null));
        Assert.Equal("12,5", PointsFormatHelper.Format((decimal?)12.5));
    }

    [Theory]
    [InlineData(12.5, "12.5")]
    [InlineData(18.75, "18.75")]
    [InlineData(25, "25")]
    public void FormatForInput_usesInvariantPointForNumberInputs(decimal value, string expected)
    {
        // Browser-Zahlenfelder akzeptieren nur den Punkt — niemals den Tausendertrenner.
        Assert.Equal(expected, PointsFormatHelper.FormatForInput(value));
    }

    [Theory]
    [InlineData("12,5", 12.5)]     // de-DE: Komma ist das Dezimalzeichen …
    [InlineData("12.5", 12.5)]     // … und der Punkt ebenfalls (Invariant zuerst geprüft)
    [InlineData("18.75", 18.75)]
    [InlineData("25", 25)]
    [InlineData("-5", -5)]
    [InlineData(" 12,5 ", 12.5)]   // Leerraum wird getrimmt
    [InlineData("", 0)]
    [InlineData(null, 0)]
    [InlineData("keine Zahl", 0)]
    public void Parse_handlesBothDecimalSeparators(string? raw, decimal expected)
    {
        Assert.Equal(expected, PointsFormatHelper.Parse(raw));
    }

    [Fact]
    public void Parse_doesNotTurnDecimalCommaIntoThousands()
    {
        // Regression: der frühere Digit-Filter machte aus "12,5" die Zahl 125.
        Assert.Equal(12.5m, PointsFormatHelper.Parse("12,5"));
        Assert.NotEqual(125m, PointsFormatHelper.Parse("12,5"));
    }

    [Fact]
    public void ParseOptional_returnsNullForEmptyInput()
    {
        Assert.Null(PointsFormatHelper.ParseOptional(""));
        Assert.Null(PointsFormatHelper.ParseOptional("   "));
        Assert.Null(PointsFormatHelper.ParseOptional(null));
        Assert.Equal(12.5m, PointsFormatHelper.ParseOptional("12,5"));
    }

    [Fact]
    public void FormatAndParse_roundTripKeepsTheValue()
    {
        foreach (var value in new[] { 12.5m, 18.75m, 25m, 0.5m, -3.25m })
        {
            Assert.Equal(value, PointsFormatHelper.Parse(PointsFormatHelper.Format(value)));
        }
    }
}
