using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace <OWNER_HANDLE>_ERC.Helpers
{
    /// <summary>
    /// Extrahiert Text aus einer PDF-Datei und zerlegt ihn in Sektionen
    /// (eine pro Überschrift), die dann als doc-cards gerendert werden können.
    /// </summary>
    public static class PdfTextExtractor
    {
        // Muster für Überschriften im extrahierten PDF-Text:
        //   · Rein in Großbuchstaben (≥ 3 Zeichen, max. 120 Zeichen)
        //   · Nummerierte Abschnitte: "1.", "2.1", "3.1.2" am Zeilenanfang
        private static readonly Regex HeadingAllCaps =
            new(@"^\s*[A-ZÄÖÜ§\s\d\.]{3,120}\s*$", RegexOptions.Compiled);

        private static readonly Regex HeadingNumbered =
            new(@"^\s*\d+(\.\d+)*\.?\s+\S", RegexOptions.Compiled);

        public record Section(string Heading, string Body, int Index);

        /// <summary>
        /// Öffnet die PDF-Datei unter <paramref name="filePath"/>, extrahiert den Text
        /// aller Seiten (ohne Bilder) und gibt eine Liste von Sektionen zurück.
        /// </summary>
        public static List<Section> ExtractSections(string filePath)
        {
            // Gesamten Text aus dem PDF lesen
            var lines = ExtractLines(filePath);

            // Zeilen in Sektionen aufteilen
            return SplitIntoSections(lines);
        }

        // ── Hilfsmethoden ────────────────────────────────────────────────────

        private static List<string> ExtractLines(string filePath)
        {
            var lines = new List<string>();

            using var document = PdfDocument.Open(filePath);

            foreach (var page in document.GetPages())
            {
                // Wörter nach Y-Position (oben → unten) und X-Position (links → rechts) sortieren
                var words = page.GetWords()
                    .OrderByDescending(w => w.BoundingBox.Bottom)
                    .ThenBy(w => w.BoundingBox.Left)
                    .ToList();

                if (words.Count == 0) continue;

                // Wörter anhand ihrer Y-Position zu Zeilen zusammenfassen
                double lastY = double.MaxValue;
                var currentLine = new StringBuilder();
                const double lineTolerance = 3.0; // pt

                foreach (var word in words)
                {
                    double y = word.BoundingBox.Bottom;
                    if (Math.Abs(y - lastY) > lineTolerance && currentLine.Length > 0)
                    {
                        lines.Add(currentLine.ToString().Trim());
                        currentLine.Clear();
                    }
                    if (currentLine.Length > 0) currentLine.Append(' ');
                    currentLine.Append(word.Text);
                    lastY = y;
                }

                if (currentLine.Length > 0)
                    lines.Add(currentLine.ToString().Trim());

                // Seitenumbruch als Leerzeile markieren
                lines.Add(string.Empty);
            }

            return lines;
        }

        private static List<Section> SplitIntoSections(List<string> lines)
        {
            var sections = new List<Section>();
            var currentHeading = string.Empty;
            var bodyLines = new List<string>();
            int sectionIndex = 0;

            void Flush()
            {
                string body = BuildBody(bodyLines);
                if (!string.IsNullOrWhiteSpace(currentHeading) || !string.IsNullOrWhiteSpace(body))
                {
                    sections.Add(new Section(currentHeading, body, sectionIndex++));
                }
                bodyLines.Clear();
            }

            foreach (var line in lines)
            {
                if (IsHeading(line))
                {
                    Flush();
                    currentHeading = line.Trim();
                }
                else
                {
                    bodyLines.Add(line);
                }
            }

            Flush();
            return sections;
        }

        private static bool IsHeading(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            string trimmed = line.Trim();

            // Zu kurz oder zu lang → kein Heading
            if (trimmed.Length < 3 || trimmed.Length > 120) return false;

            // Nummeriertes Muster: "1. Abschnitt", "2.1 Regel"
            if (HeadingNumbered.IsMatch(trimmed)) return true;

            // Vollständig in Großbuchstaben (nur Buchstaben/Zahlen/§ – keine Satzzeichen)
            // und enthält mindestens 2 Buchstaben
            int letterCount = trimmed.Count(char.IsLetter);
            if (letterCount >= 2 && HeadingAllCaps.IsMatch(trimmed)
                && trimmed.All(c => char.IsUpper(c) || char.IsDigit(c) || c == ' '
                                    || c == '.' || c == '§' || c == 'Ä' || c == 'Ö'
                                    || c == 'Ü' || c == '\u00c4' || c == '\u00d6'
                                    || c == '\u00dc'))
            {
                return true;
            }

            return false;
        }

        private static string BuildBody(List<string> lines)
        {
            var sb = new StringBuilder();
            bool lastWasEmpty = false;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (!lastWasEmpty && sb.Length > 0)
                        sb.AppendLine();
                    lastWasEmpty = true;
                }
                else
                {
                    sb.AppendLine(line);
                    lastWasEmpty = false;
                }
            }

            return sb.ToString().Trim();
        }
    }
}
