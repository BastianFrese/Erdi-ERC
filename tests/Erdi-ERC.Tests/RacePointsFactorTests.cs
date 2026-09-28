using Erdi_ERC.Helpers;
using Xunit;

namespace Erdi_ERC.Tests;

/// <summary>
/// Sichert die zentrale Rennabbruch-Regel ab: welcher Anteil bei welcher gefahrenen
/// Distanz vergeben wird und dass die Multiplikation exakt bleibt (50 % aus 25 = 12,5,
/// 75 % aus 25 = 18,75 — der User will Bruchteile, keine Rundung).
/// </summary>
public class RacePointsFactorTests
{
    [Theory]
    [InlineData(100, 25, 25)]      // voll gewertet
    [InlineData(75, 25, 18.75)]    // 75 % aus 25
    [InlineData(50, 25, 12.5)]     // 50 % aus 25
    [InlineData(50, 21, 10.5)]     // 50 % aus P2
    [InlineData(75, 21, 15.75)]    // 75 % aus P2
    [InlineData(50, 18, 9)]        // glatt aufgehend
    [InlineData(50, 1, 0.5)]       // letzter Punkterang
    [InlineData(50, 0, 0)]         // punktelose Position bleibt 0
    public void Apply_scalesBasePointsExactly(int percent, int basePoints, decimal expected)
    {
        Assert.Equal(expected, RacePointsFactor.Apply(basePoints, percent));
    }

    [Theory]
    [InlineData(0, 100)]      // 0 ist kein erlaubter Wert → voll (fail-open)
    [InlineData(33, 100)]     // erfundene Zwischenwerte werden verworfen
    [InlineData(-50, 100)]
    [InlineData(100, 100)]
    public void Normalize_fallsBackToFullForUnknownValues(int input, int expected)
    {
        Assert.Equal(expected, RacePointsFactor.Normalize(input));
    }

    [Theory]
    [InlineData(100, 100)]
    [InlineData(75, 75)]
    [InlineData(50, 50)]
    public void Normalize_keepsAllowedValues(int input, int expected)
    {
        Assert.Equal(expected, RacePointsFactor.Normalize(input));
    }

    [Theory]
    [InlineData(44, 44, 100)]   // regulär beendet
    [InlineData(44, 44 + 3, 100)] // mehr Runden als Soll (Rundenzähler-Drift) → trotzdem 100
    [InlineData(44, 33, 75)]    // exakt 75 %
    [InlineData(44, 40, 75)]    // 90 %
    [InlineData(44, 32, 50)]    // 72,7 % → unter der Schwelle
    [InlineData(44, 1, 50)]     // Abbruch in der ersten Runde
    public void DeriveFromDistance_mapsRatioToTier(int totalLaps, int completedLaps, int expected)
    {
        Assert.Equal(expected, RacePointsFactor.DeriveFromDistance(totalLaps, completedLaps));
    }

    [Theory]
    [InlineData(null, 20)]     // Soll-Distanz unbekannt (Zeitrennen)
    [InlineData(20, null)]     // keine gewerteten Fahrer → keine gefahrene Rundenzahl
    [InlineData(null, null)]
    [InlineData(0, 20)]        // Zeitrennen: 0 Runden als Soll
    [InlineData(20, 0)]
    public void DeriveFromDistance_withoutUsableDistance_staysFull(int? totalLaps, int? completedLaps)
    {
        // Ein fehlendes Signal darf nie Punkte kosten — Altdaten und Zeitrennen
        // werden voll gewertet.
        Assert.Equal(RacePointsFactor.Full, RacePointsFactor.DeriveFromDistance(totalLaps, completedLaps));
    }

    [Theory]
    [InlineData(44, 31, 70)]   // 70,5 % → gerundet 70
    [InlineData(44, 33, 75)]
    [InlineData(44, 44, 100)]
    public void PercentCompleted_roundsToWholePercent(int totalLaps, int completedLaps, int expected)
    {
        Assert.Equal(expected, RacePointsFactor.PercentCompleted(totalLaps, completedLaps));
    }

    [Theory]
    [InlineData(null, 31)]
    [InlineData(44, null)]
    [InlineData(0, 31)]
    public void PercentCompleted_withoutDistance_isNull(int? totalLaps, int? completedLaps)
    {
        Assert.Null(RacePointsFactor.PercentCompleted(totalLaps, completedLaps));
    }
}
