using Erdi_ERC.Helpers;
using Xunit;

namespace Erdi_ERC.Tests.Helpers;

/// <summary>
/// Terminberechnung wiederkehrender Streams.
///
/// Der Kern des Zeitzonen-Bugs: gerechnet wurde mit <c>DateTime.UtcNow</c>, während
/// Wochentag und Uhrzeit aus dem Admin-Formular lokale Wanduhrzeit sind. Im Sommer lag
/// das Ergebnis dadurch 2 h daneben, und zwischen 00:00 und 02:00 lokal kippte der
/// UTC-Wochentag auf den Vortag — der Termin rutschte um eine ganze Woche.
/// </summary>
public class StreamScheduleMathTests
{
    // Fester Bezugspunkt statt DateTime.Now, damit die Fälle deterministisch sind.
    // 2026-09-30 ist ein Mittwoch.
    private static readonly DateTime WednesdayEarlyMorning = new(2026, 9, 30, 0, 30, 0);
    private static readonly DateTime WednesdayEvening = new(2026, 9, 30, 20, 0, 0);

    private const int Tuesday = 2;
    private const int Wednesday = 3;

    [Fact]
    public void ComputeNextOccurrence_sameWeekdayLaterToday_returnsToday()
    {
        var next = StreamScheduleMath.ComputeNextOccurrence(Wednesday, new TimeSpan(21, 0, 0), WednesdayEvening);

        Assert.Equal(new DateTime(2026, 9, 30, 21, 0, 0), next);
    }

    [Fact]
    public void ComputeNextOccurrence_sameWeekdayAlreadyPassed_rollsOneWeek()
    {
        // 20:00 lokal, Termin war 19:00 → nächste Woche.
        var next = StreamScheduleMath.ComputeNextOccurrence(Wednesday, new TimeSpan(19, 0, 0), WednesdayEvening);

        Assert.Equal(new DateTime(2026, 10, 7, 19, 0, 0), next);
    }

    [Fact]
    public void ComputeNextOccurrence_atExactTime_returnsThatTime()
    {
        var next = StreamScheduleMath.ComputeNextOccurrence(Wednesday, new TimeSpan(20, 0, 0), WednesdayEvening);

        Assert.Equal(WednesdayEvening, next);
    }

    [Fact]
    public void ComputeNextOccurrence_afterMidnightLocal_doesNotFlipToUtcWeekday()
    {
        // Regressionstest für den Wochentags-Kippler: lokal ist es Mittwoch 00:30, in UTC
        // aber noch Dienstag 22:30. Ein Dienstag-Stream um 23:00 ist der NÄCHSTE Dienstag
        // (in 6 Tagen) — die alte UTC-Rechnung hielt den Dienstag derselben UTC-Woche für
        // "heute" und lieferte einen bereits vergangenen Zeitpunkt.
        var next = StreamScheduleMath.ComputeNextOccurrence(Tuesday, new TimeSpan(23, 0, 0), WednesdayEarlyMorning);

        Assert.Equal(new DateTime(2026, 10, 6, 23, 0, 0), next);
        Assert.True(next > WednesdayEarlyMorning);
    }

    [Fact]
    public void ComputeNextOccurrence_otherWeekday_returnsNextWeek()
    {
        // Mittwoch → Montag ist in 5 Tagen.
        var next = StreamScheduleMath.ComputeNextOccurrence(1, new TimeSpan(20, 0, 0), WednesdayEvening);

        Assert.Equal(new DateTime(2026, 10, 5, 20, 0, 0), next);
    }

    [Fact]
    public void ComputeNextOccurrence_returnsWallClockKind()
    {
        // Ergebnis ist Wanduhrzeit (Unspecified), nicht UTC: nur so verhält sich der Wert
        // wie alle anderen StartAt-Werte und wird von ToLocalTime() nicht verschoben.
        var next = StreamScheduleMath.ComputeNextOccurrence(Wednesday, new TimeSpan(21, 0, 0), WednesdayEvening);

        Assert.Equal(DateTimeKind.Unspecified, next.Kind);
    }
}
