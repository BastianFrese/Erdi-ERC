using System.Globalization;

namespace Erdi_ERC.Helpers
{
    /// <summary>
    /// Einheitliche Darstellung von Punkten in der Oberfläche. Punkte sind seit den
    /// abgebrochenen Rennen dezimal (50 % aus 25 = 12,5), deshalb darf nirgends mehr
    /// direkt interpoliert werden — sonst steht je nach Kultur "12.5" statt "12,5".
    ///
    /// Die JSON-API bleibt bewusst invariant (Zahl <c>12.5</c>); nur die Anzeige läuft hier durch.
    /// </summary>
    public static class PointsFormatHelper
    {
        private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("de-DE");

        /// <summary>Formatiert einen Punktwert mit deutschem Dezimalkomma und ohne
        /// überflüssige Nullen: 12 → "12", 12,5 → "12,5", 18,75 → "18,75".</summary>
        public static string Format(decimal value) => value.ToString("0.##", Culture);

        /// <summary>Wie <see cref="Format(decimal)"/>, aber null-sicher für ViewModels
        /// mit optionalen Punktwerten.</summary>
        public static string Format(decimal? value) => value.HasValue ? Format(value.Value) : "-";

        /// <summary>
        /// Punktwert für ein <c>&lt;input type="number"&gt;</c>: OHNE Tausendertrenner und
        /// invariant („12.5"), weil Browser in Zahlenfeldern nur den Punkt als Dezimaltrenner
        /// akzeptieren — „12,5" würde das Feld verwerfen.
        /// </summary>
        public static string FormatForInput(decimal value) =>
            value.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>
        /// Liest einen Punktwert aus einer Texteingabe oder Tabellenzelle — „12,5" UND
        /// „12.5" ergeben beide 12,5. Unlesbares ergibt 0.
        /// </summary>
        /// <remarks>
        /// Bewusst OHNE Tausendertrennzeichen: mit Gruppierung wäre „1.234" je nach Kultur
        /// 1234 oder 1,234. Unsere eigenen Sheets schreiben über <see cref="Format(decimal)"/>
        /// ohne Gruppierung, gruppierte Punktewerte kommen also nicht vor.
        /// </remarks>
        public static decimal Parse(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0m;

            const NumberStyles Styles = NumberStyles.AllowDecimalPoint
                | NumberStyles.AllowLeadingSign
                | NumberStyles.AllowLeadingWhite
                | NumberStyles.AllowTrailingWhite;

            var trimmed = raw.Trim();

            // Reihenfolge = Priorität: Invariant zuerst (so schreiben Formulare und Sheets
            // im Invariant-Kontext), danach de-DE für händisch erfasste Komma-Werte.
            if (decimal.TryParse(trimmed, Styles, CultureInfo.InvariantCulture, out var invariant))
                return invariant;

            if (decimal.TryParse(trimmed, Styles, Culture, out var german))
                return german;

            // Fallback für Zellen mit Beiwerk („25 Pkt."): Ziffern, Vorzeichen und
            // Dezimaltrenner behalten — die Trenner dürfen NICHT wegfallen.
            var cleaned = new string(trimmed.Where(c => char.IsDigit(c) || c is '-' or '+' or '.' or ',').ToArray());

            if (decimal.TryParse(cleaned, Styles, CultureInfo.InvariantCulture, out var cleanedInvariant))
                return cleanedInvariant;

            return decimal.TryParse(cleaned, Styles, Culture, out var cleanedGerman) ? cleanedGerman : 0m;
        }

        /// <summary>Wie <see cref="Parse(string)"/>, aber null bei leerer/fehlender Eingabe —
        /// für Formularfelder, bei denen „nicht angegeben" von 0 unterschieden wird.</summary>
        public static decimal? ParseOptional(string? raw)
            => string.IsNullOrWhiteSpace(raw) ? null : Parse(raw);
    }
}
