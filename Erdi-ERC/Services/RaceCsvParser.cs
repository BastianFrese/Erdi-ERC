using System.Globalization;
using System.Text;

namespace Erdi_ERC.Services;

/// <summary>
/// Eine Zeile des CSV-Rennimports (F1-Spiel-Export, deutsch oder englisch).
/// <see cref="TotalTimeMs"/> ist die berechnete GESAMT-Rennzeit (Siegerzeit + Gap zum
/// Vordermann) in Millisekunden — <c>null</c> bei überrundeten Fahrern, DNF oder wenn
/// die Basis (Siegerzeit) fehlt. <see cref="LappedText"/> trägt bei überrundeten Fahrern
/// den normalisierten CSV-Text (z. B. "+1 Runde"), damit die View ihn ins Zeitfeld setzt.
/// </summary>
public sealed record RaceCsvEntry(
    int Position,
    string Driver,
    string Team,
    long? TotalTimeMs,
    bool IsDnf,
    string? LappedText);

/// <summary>Ergebnis von <see cref="RaceCsvParser.Parse"/>.</summary>
public sealed class RaceCsvParseResult
{
    public List<RaceCsvEntry> Entries { get; init; } = new();

    /// <summary>Nicht <c>null</c> ⇒ die Eingabe war komplett unverwendbar.</summary>
    public string? Error { get; init; }

    /// <summary>Übersprungene Vor-/Kopf-/Müllzeilen (keine gültigen Positions-Zeilen).</summary>
    public int SkippedLines { get; init; }
}

/// <summary>
/// Parst den CSV-Export des F1-Spiels für das Eintragen von Renn-Ergebnissen.
/// Kernlogik „Gesamtzeit": Die Siegerzeit (Z. <c>45:31,798</c>) ist die Basis; für jeden
/// weiteren Fahrer wird der Gap (<c>+12,423</c>, <c>+1:02,309</c>) zur Gesamtzeit dazu-
/// addiert. Kommas sind Dezimaltrenner. Überrundet (<c>+ 1 Runde</c>) ist nicht
/// berechenbar und liefert nur <see cref="RaceCsvEntry.LappedText"/>; <c>DNF</c> wird als
/// solches markiert. DB-frei und statisch → 1:1 unit-testbar.
/// </summary>
public static class RaceCsvParser
{
    /// <summary>Schutz vor unsinnig großen Payloads (das Spiel-Export ist &lt; 5 KB).</summary>
    private const int MaxInputLength = 50_000;

    public static RaceCsvParseResult Parse(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return new RaceCsvParseResult { Error = "Keine CSV-Daten eingegeben." };

        if (csv.Length > MaxInputLength)
            return new RaceCsvParseResult { Error = "Eingabe ist zu groß (max. 50 KB)." };

        var lines = csv.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        var entries = new List<RaceCsvEntry>();
        int skipped = 0;

        // Spalten-Indizes: pro Zeile per Header erkannt werden (deutsch/englisch),
        // sonst gelten feste Default-Indizes 0/1/6/2 (Pos, Fahrer, Zeit, Team).
        int posIdx = 0, driverIdx = 1, teamIdx = 2, zeitIdx = 6;
        bool headerApplied = false;

        // Laufende Basis = letzte absolute Siegerzeit; Gaps werden darauf addiert.
        long? baseMs = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0) { skipped++; continue; }

            var fields = SplitCsvLine(line);
            if (fields.Count < 7 || posIdx >= fields.Count || driverIdx >= fields.Count || zeitIdx >= fields.Count)
            {
                skipped++;
                continue;
            }

            // Kopfzeile: erstes Feld ist nicht numerisch → Spalten-Mapping über den Header.
            // Nur echte Header (mit bekannten Spalten-Tokens) gelten als solche — eine
            // Preambel-/Titelzeile mit ≥7 Feldern wird übersprungen statt als Header verbraucht,
            // sonst würden die Indizes auf die Defaults fallen und spätere Zeilen falsch mappen.
            if (!headerApplied && !int.TryParse(fields[posIdx], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                if (HasHeaderTokens(fields))
                {
                    ApplyHeader(fields, ref posIdx, ref driverIdx, ref teamIdx, ref zeitIdx);
                    headerApplied = true;
                }
                skipped++;
                continue;
            }

            if (!int.TryParse(fields[posIdx].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var position) || position < 1)
            {
                skipped++;
                continue;
            }

            var driver = fields[driverIdx].Trim();
            var team = teamIdx < fields.Count ? fields[teamIdx].Trim() : string.Empty;
            var zeitRaw = fields[zeitIdx].Trim();

            var (timeMs, isDnf, lappedText, isAbsolute) = InterpretZeit(zeitRaw, baseMs);
            if (isAbsolute && timeMs.HasValue) baseMs = timeMs.Value;

            entries.Add(new RaceCsvEntry(
                position,
                driver,
                team,
                timeMs,
                isDnf,
                lappedText));
        }

        return new RaceCsvParseResult { Entries = entries, SkippedLines = skipped };
    }

    /// <summary>
    /// Interpretiert das Zeit-Feld: absoluter Wert ⇒ Siegerzeit/Gesamtzeit-Basis; Gap mit
    /// <c>+</c>-Präfix ⇒ Basis + Gap; <c>+ N Runde(n)</c>/<c>Lap(s)</c> ⇒ überrundet; <c>DNF</c> ⇒ Ausfall.
    /// </summary>
    private static (long? TotalMs, bool IsDnf, string? LappedText, bool IsAbsolute) InterpretZeit(string zeitRaw, long? baseMs)
    {
        var text = zeitRaw.Trim();
        if (text.Length == 0) return (null, false, null, false);

        // Überrundet: "Runde"/"Runden"/"Lap"/"Laps" (case-insensitive) → Zahl davor.
        var rounds = MatchLapCount(text);
        if (rounds.HasValue)
        {
            var lapText = rounds.Value == 1 ? "+1 Runde" : $"+{rounds} Runden";
            return (null, false, lapText, false);
        }

        if (text.Contains("DNF", StringComparison.OrdinalIgnoreCase))
            return (null, true, null, false);

        var isGap = text.StartsWith('+');
        if (isGap) text = text[1..].Trim();

        var ms = RaceTimeParser.ParseMs(text);
        if (!ms.HasValue) return (null, false, null, false);

        if (isGap)
            return baseMs.HasValue ? (baseMs.Value + ms.Value, false, null, false) : (null, false, null, false);

        return (ms.Value, false, null, true);
    }

    /// <summary>Extrahiert die Rundenzahl aus übertrundeten Zeitfeldern (z. B. "+ 1 Runde" → 1).</summary>
    private static int? MatchLapCount(string text)
    {
        var lower = text.ToLowerInvariant();
        int markerIdx;
        if (lower.Contains("runde")) markerIdx = lower.IndexOf("runde");
        else if (lower.Contains("lap")) markerIdx = lower.IndexOf("lap");
        else return null;

        var prefix = text[..markerIdx].Trim().TrimStart('+').Trim();
        return int.TryParse(prefix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    /// <summary>
    /// Setzt die Spalten-Indizes aus der Kopfzeile (case-insensitive, deutsch/englisch).
    /// Nicht gefundene Spalten behalten ihren bisherigen Wert (Default 0/1/6/2).
    /// </summary>
    private static void ApplyHeader(List<string> fields, ref int posIdx, ref int driverIdx, ref int teamIdx, ref int zeitIdx)
    {
        for (int i = 0; i < fields.Count; i++)
        {
            var name = fields[i].Trim().Trim('"').ToLowerInvariant();
            if (IsAnyOf(name, "pos.", "pos", "position", "platz")) posIdx = i;
            else if (IsAnyOf(name, "fahrer", "driver", "name")) driverIdx = i;
            else if (IsAnyOf(name, "zeit", "time")) zeitIdx = i;
            else if (name == "team") teamIdx = i;
        }
    }

    /// <summary>
    /// Erkennt an einer Zeile mit nicht-numerischem ersten Feld, ob es sich um eine echte
    /// Kopfzeile handelt (enthält bekannte Spalten-Tokens) — verhindert, dass eine
    /// Preambel-/Titelzeile als Header verbraucht wird.
    /// </summary>
    private static bool HasHeaderTokens(List<string> fields)
        => fields.Any(f => IsAnyOf(f.Trim().Trim('"'),
            "pos.", "pos", "position", "platz",
            "fahrer", "driver", "name",
            "zeit", "time",
            "team"));

    private static bool IsAnyOf(string value, params string[] candidates)
        => candidates.Any(c => string.Equals(value, c, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Zerlegt eine CSV-Zeile in ihre Felder — quoting-sicher, damit Dezimal-Kommas in einem
    /// gequoteten Feld (<c>"1:20,126"</c>) NICHT als Trennzeichen zählen. Umgibt Anführungs-
    /// zeichen werden entfernt; verdoppelte <c>""</c> innerhalb eines Felds = ein Literal-Zeichen.
    /// </summary>
    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        result.Add(sb.ToString());
        return result;
    }
}
