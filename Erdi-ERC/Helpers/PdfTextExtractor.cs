using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace <OWNER_HANDLE>_ERC.Helpers
{
    /// <summary>
    /// Extrahiert Text aus einer PDF-Datei und zerlegt ihn in Sektionen
    /// (eine pro Überschrift), die dann als doc-cards gerendert werden können.
    /// Erkennt Tabellen anhand ausgerichteter Wort-X-Positionen und gibt sie
    /// als HTML-&lt;table&gt; aus.
    /// </summary>
    public static class PdfTextExtractor
    {
        // Muster für Überschriften im extrahierten PDF-Text:
        //   · Rein in Großbuchstaben (≥ 3 Zeichen, max. 120 Zeichen)
        //   · Nummerierte Abschnitte: "1.", "2.1", "3.1.2" am Zeilenanfang
        //     (Punkt nach der ersten Zahl ist Pflicht, damit Tabellenzeilen wie
        //     "10 Strafpunkte" nicht als Überschrift erkannt werden)
        private static readonly Regex HeadingAllCaps =
            new(@"^\s*[A-ZÄÖÜ§\s\d\.]{3,120}\s*$", RegexOptions.Compiled);

        // "1. Abschnitt" – einfache Nummerierung mit Pflicht-Punkt
        private static readonly Regex HeadingNumbered =
            new(@"^\s*\d+\.(?!\d)\s+\S", RegexOptions.Compiled);

        // "2.1 Abschnitt", "6.2.2 Abschnitt" – mehrstufige Nummerierung
        private static readonly Regex HeadingSubNumbered =
            new(@"^\s*\d+(\.\d+)+\s+\S", RegexOptions.Compiled);

        // ── Interne Hilfstypen ────────────────────────────────────────────────

        private record WordInfo(string Text, double LeftX);

        private record LineWithPositions(string Text, List<WordInfo> Words, bool IsEmpty);

        private record TableRegion(int StartLine, int EndLine, int ColumnCount);

        // ── Öffentliche API ───────────────────────────────────────────────────

        public record Section(string Heading, string Body, int Index);

        /// <summary>
        /// Öffnet die PDF-Datei unter <paramref name="filePath"/>, extrahiert den Text
        /// aller Seiten (ohne Bilder) und gibt eine Liste von Sektionen zurück.
        /// Der Body enthält HTML-Markup: Paragraphen als &lt;p&gt;, Tabellen als &lt;table&gt;.
        /// </summary>
        public static List<Section> ExtractSections(string filePath)
        {
            // Gesamten Text aus dem PDF lesen (mit Wort-Positionen)
            var lines = ExtractLinesWithPositions(filePath);

            // Tabellen erkennen
            var tables = DetectTableRegions(lines);

            // Zeilen in Sektionen aufteilen (Tabellenzeilen von Heading-Erkennung ausgeschlossen)
            return SplitIntoSections(lines, tables);
        }

        // ── PDF-Text-Extraktion mit Wort-Positionen ──────────────────────────

        private static List<LineWithPositions> ExtractLinesWithPositions(string filePath)
        {
            var lines = new List<LineWithPositions>();

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
                var currentWords = new List<WordInfo>();
                var currentText = new StringBuilder();
                const double lineTolerance = 3.0; // pt

                foreach (var word in words)
                {
                    double y = word.BoundingBox.Bottom;
                    if (Math.Abs(y - lastY) > lineTolerance && currentText.Length > 0)
                    {
                        lines.Add(new LineWithPositions(
                            currentText.ToString().Trim(),
                            new List<WordInfo>(currentWords),
                            false));
                        currentText.Clear();
                        currentWords.Clear();
                    }
                    if (currentText.Length > 0) currentText.Append(' ');
                    currentText.Append(word.Text);
                    currentWords.Add(new WordInfo(word.Text, word.BoundingBox.Left));
                    lastY = y;
                }

                if (currentText.Length > 0)
                    lines.Add(new LineWithPositions(
                        currentText.ToString().Trim(),
                        new List<WordInfo>(currentWords),
                        false));

                // Seitenumbruch als Leerzeile markieren
                lines.Add(new LineWithPositions(string.Empty, new List<WordInfo>(), true));
            }

            return lines;
        }

        // ── Tabellenerkennung ─────────────────────────────────────────────────

        /// <summary>
        /// Scannt alle Zeilen und erkennt Tabellenbereiche.
        /// Eine Tabelle liegt vor, wenn ≥2 aufeinanderfolgende Zeilen dieselbe
        /// Wortanzahl (≥2) haben und die X-Positionen der Wörter spaltenweise
        /// ausgerichtet sind.
        /// </summary>
        private static List<TableRegion> DetectTableRegions(List<LineWithPositions> lines)
        {
            var tables = new List<TableRegion>();
            const double columnAlignTolerance = 12.0; // pt

            int i = 0;
            while (i < lines.Count)
            {
                var line = lines[i];

                // Leerzeilen, Zeilen mit &lt;2 Wörtern oder Überschriften können keine Tabellenzeilen sein
                if (line.IsEmpty || line.Words.Count < 2 || IsHeading(line.Text))
                {
                    i++;
                    continue;
                }

                int columnCount = line.Words.Count;

                // Vorwärtssuche: wie viele Folgezeilen haben dieselbe Spaltenanzahl
                // und ausgerichtete X-Positionen?
                int j = i + 1;
                var tableRows = new List<int> { i };

                while (j < lines.Count)
                {
                    var nextLine = lines[j];

                    // Leerzeilen innerhalb der Tabelle überspringen
                    if (nextLine.IsEmpty)
                    {
                        j++;
                        continue;
                    }

                    // Überschriften beenden die Tabelle
                    if (IsHeading(nextLine.Text))
                        break;

                    // Abweichende Spaltenanzahl → kein Tabellen-Match
                    if (nextLine.Words.Count != columnCount)
                        break;

                    // X-Ausrichtung gegen die erste Zeile prüfen
                    bool aligned = true;
                    for (int c = 0; c < columnCount; c++)
                    {
                        if (Math.Abs(line.Words[c].LeftX - nextLine.Words[c].LeftX) > columnAlignTolerance)
                        {
                            aligned = false;
                            break;
                        }
                    }

                    if (!aligned) break;

                    tableRows.Add(j);
                    j++;
                }

                // Mindestens 2 Zeilen für eine Tabelle
                if (tableRows.Count >= 2)
                {
                    int endLine = tableRows[tableRows.Count - 1];
                    tables.Add(new TableRegion(i, endLine, columnCount));
                    i = endLine + 1;
                }
                else
                {
                    i++;
                }
            }

            return tables;
        }

        // ── Sektionsaufteilung ────────────────────────────────────────────────

        private static List<Section> SplitIntoSections(List<LineWithPositions> lines, List<TableRegion> tables)
        {
            var sections = new List<Section>();
            var currentHeading = string.Empty;
            var bodyLineIndices = new List<int>();
            int sectionIndex = 0;

            // Alle Zeilenindizes, die Teil einer Tabelle sind
            var tableLineIndices = new HashSet<int>();
            foreach (var table in tables)
            {
                for (int idx = table.StartLine; idx <= table.EndLine; idx++)
                    tableLineIndices.Add(idx);
            }

            void Flush()
            {
                string body = BuildHtmlBody(lines, bodyLineIndices, tables);
                if (!string.IsNullOrWhiteSpace(currentHeading) || !string.IsNullOrWhiteSpace(body))
                {
                    sections.Add(new Section(currentHeading, body, sectionIndex++));
                }
                bodyLineIndices.Clear();
            }

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                // Tabellenzeilen werden NICHT als Überschriften erkannt
                if (!tableLineIndices.Contains(i) && IsHeading(line.Text))
                {
                    Flush();
                    currentHeading = line.Text.Trim();
                }
                else
                {
                    bodyLineIndices.Add(i);
                }
            }

            Flush();
            return sections;
        }

        // ── HTML-Body-Generierung ─────────────────────────────────────────────

        /// <summary>
        /// Baut aus den Zeilen einer Sektion einen HTML-Body-String.
        /// Nicht-Tabellen-Zeilen werden zu &lt;p&gt;-Paragraphen gruppiert,
        /// Tabellen werden als &lt;table&gt; eingefügt.
        /// </summary>
        private static string BuildHtmlBody(
            List<LineWithPositions> lines,
            List<int> lineIndices,
            List<TableRegion> allTables)
        {
            if (lineIndices.Count == 0) return string.Empty;

            // Relevante Tabellen für diesen Zeilenbereich
            var relevantTables = allTables
                .Where(t => lineIndices.Any(i => i >= t.StartLine && i <= t.EndLine))
                .OrderBy(t => t.StartLine)
                .ToList();

            // Quick-Lookup: zu welcher Tabelle gehört ein Zeilenindex?
            var tableByLine = new Dictionary<int, TableRegion>();
            foreach (var t in relevantTables)
            {
                for (int idx = t.StartLine; idx <= t.EndLine; idx++)
                    tableByLine[idx] = t;
            }

            var sb = new StringBuilder();
            var paraBuf = new StringBuilder();
            bool paraOpen = false;

            void CloseParagraph()
            {
                if (!paraOpen) return;
                var text = paraBuf.ToString().Trim();
                if (text.Length > 0)
                {
                    sb.Append("<p>");
                    sb.Append(System.Net.WebUtility.HtmlEncode(text));
                    sb.Append("</p>");
                }
                paraBuf.Clear();
                paraOpen = false;
            }

            int currentIdx = 0;
            while (currentIdx < lineIndices.Count)
            {
                int lineIdx = lineIndices[currentIdx];

                // Prüfen, ob diese Zeile der Start einer Tabelle ist
                if (tableByLine.TryGetValue(lineIdx, out var table) && table.StartLine == lineIdx)
                {
                    CloseParagraph();
                    sb.Append(BuildHtmlTable(lines, table));
                    // Alle Zeilen dieser Tabelle überspringen
                    while (currentIdx < lineIndices.Count && lineIndices[currentIdx] <= table.EndLine)
                        currentIdx++;
                    continue;
                }

                // Zeile gehört zu einer Tabelle, ist aber nicht die Startzeile → überspringen
                if (tableByLine.ContainsKey(lineIdx))
                {
                    currentIdx++;
                    continue;
                }

                // Normale Textzeile
                var line = lines[lineIdx];
                if (line.IsEmpty)
                {
                    // Leerzeile → Paragraph abschließen
                    CloseParagraph();
                }
                else
                {
                    if (paraBuf.Length > 0) paraBuf.Append(' ');
                    paraBuf.Append(line.Text);
                    paraOpen = true;
                }
                currentIdx++;
            }

            CloseParagraph();
            return sb.ToString();
        }

        /// <summary>
        /// Erzeugt HTML-&lt;table&gt;-Markup aus einer erkannten Tabellenregion.
        /// Die erste Zeile wird als &lt;thead&gt; gerendert, alle weiteren als &lt;tbody&gt;.
        /// </summary>
        private static string BuildHtmlTable(List<LineWithPositions> lines, TableRegion table)
        {
            var sb = new StringBuilder();
            sb.Append("<table>");

            // Erste Zeile = Header
            sb.Append("<thead><tr>");
            var headerLine = lines[table.StartLine];
            foreach (var word in headerLine.Words)
            {
                sb.Append("<th>");
                sb.Append(System.Net.WebUtility.HtmlEncode(word.Text));
                sb.Append("</th>");
            }
            sb.Append("</tr></thead>");

            // Folgezeilen = Body
            sb.Append("<tbody>");
            for (int i = table.StartLine + 1; i <= table.EndLine; i++)
            {
                var rowLine = lines[i];
                if (rowLine.IsEmpty) continue;

                sb.Append("<tr>");
                // Verwende die tatsächliche Wortanzahl dieser Zeile
                // (kann bei leeren Zellen abweichen – wir orientieren uns an der Header-Spaltenzahl)
                int colCount = Math.Min(rowLine.Words.Count, table.ColumnCount);
                for (int c = 0; c < colCount; c++)
                {
                    sb.Append("<td>");
                    sb.Append(System.Net.WebUtility.HtmlEncode(rowLine.Words[c].Text));
                    sb.Append("</td>");
                }
                // Fehlende Spalten mit leeren &lt;td&gt; auffüllen
                for (int c = colCount; c < table.ColumnCount; c++)
                {
                    sb.Append("<td></td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</tbody>");

            sb.Append("</table>");
            return sb.ToString();
        }

        // ── Überschriftenerkennung ────────────────────────────────────────────

        private static bool IsHeading(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            string trimmed = line.Trim();

            // Zu kurz oder zu lang → kein Heading
            if (trimmed.Length < 3 || trimmed.Length > 120) return false;

            // Nummeriertes Muster: "1. Abschnitt", "2.1 Regel", "6.2.2 Unterpunkt"
            if (HeadingNumbered.IsMatch(trimmed) || HeadingSubNumbered.IsMatch(trimmed)) return true;

            // Vollständig in Großbuchstaben (nur Buchstaben/Zahlen/§ – keine Satzzeichen)
            // und enthält mindestens 2 Buchstaben
            int letterCount = trimmed.Count(char.IsLetter);
            if (letterCount >= 2 && HeadingAllCaps.IsMatch(trimmed)
                && trimmed.All(c => char.IsUpper(c) || char.IsDigit(c) || c == ' '
                                    || c == '.' || c == '§' || c == 'Ä' || c == 'Ö'
                                    || c == 'Ü'))
            {
                return true;
            }

            return false;
        }
    }
}
