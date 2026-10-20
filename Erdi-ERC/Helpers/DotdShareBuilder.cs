using Erdi_ERC.Models;

namespace Erdi_ERC.Helpers;

/// <summary>
/// Baut die Balken für das Driver-of-the-Day-Overlay.
///
/// Während der Wahl verlassen nur gerundete Anteile (5-%-Schritte) plus leichtes
/// Rauschen den Server — nie eine Stimmenzahl. Die Auswahl bleibt bei höchstens acht
/// Fahrern, die Ausgabe steht alphabetisch, damit nur die Längen wandern.
/// </summary>
public static class DotdShareBuilder
{
    public const int MaxBars = 8;
    public const int BucketPercent = 5;

    /// <summary>Offene Wahl: gerundete, verrauschte Anteile.</summary>
    public static IReadOnlyList<DotdBar> BuildOpenBars(
        IReadOnlyDictionary<string, int> counts,
        Func<int>? noise = null)
    {
        var total = counts.Values.Where(v => v > 0).Sum();
        if (total == 0)
        {
            return Array.Empty<DotdBar>();
        }

        noise ??= () => Random.Shared.Next(-3, 4);

        return SelectTop(counts)
            .Select(entry =>
            {
                var bucket = (int)Math.Round(entry.Votes * 100.0 / total / BucketPercent) * BucketPercent;
                bucket = Math.Max(BucketPercent, bucket);
                var share = Math.Clamp(bucket + noise(), 0, 100);
                return new DotdBar(DriverNameHelper.Display(entry.Name), share);
            })
            .OrderBy(bar => bar.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(bar => bar.Name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Geschlossene Wahl: echte Prozente, ohne Rauschen.</summary>
    public static IReadOnlyList<DotdBar> BuildFinalBars(IReadOnlyDictionary<string, int> counts)
    {
        var total = counts.Values.Where(v => v > 0).Sum();
        if (total == 0)
        {
            return Array.Empty<DotdBar>();
        }

        return SelectTop(counts)
            .Select(entry => new DotdBar(
                DriverNameHelper.Display(entry.Name),
                (int)Math.Round(entry.Votes * 100.0 / total, MidpointRounding.AwayFromZero)))
            .OrderBy(bar => bar.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(bar => bar.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<(string Name, int Votes)> SelectTop(
        IReadOnlyDictionary<string, int> counts)
        => counts
            .Where(entry => entry.Value > 0)
            .Select(entry => (Name: entry.Key, Votes: entry.Value))
            .OrderByDescending(entry => entry.Votes)
            .ThenBy(entry => DriverNameHelper.Display(entry.Name), StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Take(MaxBars)
            .ToArray();
}
