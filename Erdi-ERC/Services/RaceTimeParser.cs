using System.Globalization;

namespace Erdi_ERC.Services;

/// <summary>
/// Einzige Implementierung für das Parsen von Renn-Zeitfeldern in Millisekunden.
/// Wird sowohl vom CSV-Rennimport (<see cref="RaceCsvParser"/>) als auch vom
/// Save-Pfad in <c>AdminLeagueController</c> genutzt — damit Zeitformate nie
/// auseinanderdrifteten, wenn sie erweitert werden.
/// Formate: <c>[H:]MM:SS.mmm</c>, <c>MM:SS.mmm</c> oder nackte Sekunden (<c>12.423</c>).
/// Komma gilt als Dezimaltrenner und wird auf "." normalisiert.
/// </summary>
public static class RaceTimeParser
{
    public static long? ParseMs(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var s = raw.Trim().Replace(",", ".");
        if (s.Length == 0) return null;

        if (s.Contains(':'))
        {
            var parts = s.Split(':');
            if (parts.Length == 3
                && int.TryParse(parts[0], out var hours)
                && int.TryParse(parts[1], out var mins)
                && double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var secs))
                return (long)((hours * 3600 + mins * 60 + secs) * 1000);

            if (parts.Length == 2
                && int.TryParse(parts[0], out var mins2)
                && double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var secs2))
                return (long)((mins2 * 60 + secs2) * 1000);

            return null;
        }

        return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var secs3)
            ? (long)(secs3 * 1000)
            : null;
    }
}
