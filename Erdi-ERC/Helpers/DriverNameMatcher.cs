using System.Globalization;
using System.Text;

namespace Erdi_ERC.Helpers;

/// <summary>
/// Bildet kurze, fehlerhafte Chat-Eingaben auf gespeicherte Fahrernamen ab.
///
/// Gespeicherte Namen tragen das Clantag („ERC | Wiener"). Der Chat schreibt
/// „wiener", „wienr" oder „müller". Die Normalisierung entfernt Clantag, Umlaute
/// und Satzzeichen; <see cref="Match"/> probiert danach vier Stufen von sicher
/// nach tolerant. Mehrdeutige Eingaben werden abgelehnt — nie ein zufälliger Fahrer.
/// </summary>
public static class DriverNameMatcher
{
    /// <summary>Ergebnis eines Abgleichs. <see cref="Driver"/> ist der volle
    /// gespeicherte Name oder null; <see cref="Ambiguous"/> meldet Mehrdeutigkeit.</summary>
    public sealed record MatchResult(string? Driver, bool Ambiguous);

    /// <summary>Kleinschreibung, Umlaut-Transkription, Diakritika weg, Clantag weg.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = value.Trim().ToLowerInvariant()
            .Replace("ä", "ae")
            .Replace("ö", "oe")
            .Replace("ü", "ue")
            .Replace("ß", "ss");

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        var normalized = string.Join(' ',
            builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (normalized == "erc")
        {
            return string.Empty;
        }

        if (normalized.StartsWith("erc ", StringComparison.Ordinal))
        {
            normalized = normalized[4..].TrimStart();
        }

        return normalized.Trim();
    }

    /// <summary>Case-insensitive Levenshtein-Distanz.</summary>
    public static int Levenshtein(string a, string b)
    {
        a ??= string.Empty;
        b ??= string.Empty;
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        a = a.ToLowerInvariant();
        b = b.ToLowerInvariant();

        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }

            (prev, curr) = (curr, prev);
        }

        return prev[b.Length];
    }

    /// <summary>
    /// Sucht den eindeutigen Fahrer zu einer Chat-Eingabe. Stufen: exakter Name →
    /// exaktes Namenswort → Teilwort → Tippfehler. Mehrere Treffer derselben Stufe
    /// gelten als mehrdeutig.
    /// </summary>
    public static MatchResult Match(string query, IReadOnlyList<string> candidateFullNames)
    {
        var normalizedQuery = Normalize(query);
        if (normalizedQuery.Length == 0 || candidateFullNames.Count == 0)
        {
            return new MatchResult(null, false);
        }

        var candidates = candidateFullNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name =>
            {
                var display = Normalize(DriverNameHelper.Display(name));
                var aliases = new[] { Normalize(name), display }
                    .Where(alias => alias.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                var words = display.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return new Candidate(name, aliases, words);
            })
            .ToArray();

        var exact = candidates
            .Where(c => c.Aliases.Any(alias => alias == normalizedQuery))
            .Select(c => c.Full)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (exact.Length > 0) return Resolve(exact);

        var word = candidates
            .Where(c => c.Words.Any(w => w.Length >= 2 && w == normalizedQuery))
            .Select(c => c.Full)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (word.Length > 0) return Resolve(word);

        if (normalizedQuery.Length >= 3)
        {
            var contains = candidates
                .Where(c => c.Aliases.Any(alias =>
                    alias.Contains(normalizedQuery, StringComparison.Ordinal) ||
                    (alias.Length >= 3 && normalizedQuery.Contains(alias, StringComparison.Ordinal))))
                .Select(c => c.Full)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (contains.Length > 0) return Resolve(contains);
        }

        var typo = candidates
            .Where(c => c.Aliases.Any(alias => Levenshtein(alias, normalizedQuery) <= 2) ||
                        c.Words.Any(w => w.Length >= 3 && Levenshtein(w, normalizedQuery) <= 1))
            .Select(c => c.Full)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return typo.Length > 0 ? Resolve(typo) : new MatchResult(null, false);
    }

    private static MatchResult Resolve(string[] matches)
        => matches.Length == 1
            ? new MatchResult(matches[0], false)
            : new MatchResult(null, true);

    private sealed record Candidate(string Full, string[] Aliases, string[] Words);
}
