using System.Text.Json;

namespace Erdi_ERC.Services;

/// <summary>
/// Ein einzelner Fahrer aus dem Telemetrie-Payload. <see cref="Position"/> ist
/// 1-basiert für Zielankünfte und 0 für DNF (gleiche Semantik wie <see cref="RaceFinish.Position"/>).
/// <para><see cref="NumLaps"/> ist die gefahrene Rundenzahl der Session (App-Feld <c>numLaps</c>)
/// und dient nur der Ableitung des Punkte-Faktors bei Rennabbruch — null, wenn die App sie
/// nicht mitschickt (ältere Builds, Zeitrennen).</para>
/// </summary>
public sealed record ParsedTelemetryFinish(
    int Position,
    string Driver,
    bool IsDnf,
    long? RaceTimeMs,
    int? QualifyingPosition,
    int? NumLaps = null);

/// <summary>
/// Kanonisch geparstes Telemetrie-Rennergebnis. Noch DB-frei — erst der
/// <see cref="TelemetryIngestService"/> persistiert es als Pending.
/// </summary>
public sealed class ParsedTelemetryResult
{
    public string? Track { get; init; }

    /// <summary>Renndatum/Startzeit (UTC).</summary>
    public DateTime? Date { get; set; }

    /// <summary>Liga-Vorschlag aus der App-Auswahl (Name, frei). Wird beim Ingest gegen die
    /// Sender-Mitgliedschaft geprüft (403, wenn der Sender dort kein aktiver Fahrer ist).</summary>
    public string? League { get; init; }

    public string? Season { get; init; }
    public string? FastestLap { get; init; }

    /// <summary>Soll-Distanz der Session in Runden (App-Feld <c>totalLaps</c>). Basis der
    /// Faktor-Ableitung zusammen mit den gefahrenen Runden der Fahrer; null bei Zeitrennen
    /// oder älteren App-Builds → der Review bleibt bei 100 %.</summary>
    public int? TotalLaps { get; init; }

    public List<ParsedTelemetryFinish> Finishes { get; init; } = new();
    public List<ParsedTelemetryAssignment> ReserveAssignments { get; init; } = new();
    public List<ParsedTelemetryAssignment> GuestAssignments { get; init; } = new();
}

public sealed record ParsedTelemetryAssignment(string Driver, string MainDriver);

/// <summary>Result-Type: Entweder geparstes Ergebnis oder eine deutsche Fehlermeldung (nie beides).</summary>
public sealed class TelemetryRaceParseResult
{
    public ParsedTelemetryResult? Result { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Parser für den Telemetrie-Payload (telemetrie.erdi-erc.de) — statisch & DB-frei,
/// analog zum CSV-Rennimport (<see cref="RaceCsvParser"/>). Validiert das Schema und
/// liefert im Fehlerfall eine klare Meldung für den API-Client.
/// </summary>
public static class TelemetryRaceParser
{
    public const int MaxInputLength = 50_000;

    /// <summary>Obergrenze für Fahrer pro Rennen — schützt vor absurden Payloads (Grid + Ersatzfahrer).</summary>
    public const int MaxFinishes = 60;

    public static TelemetryRaceParseResult Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new TelemetryRaceParseResult { Error = "Kein JSON-Body empfangen." };
        }

        if (json.Length > MaxInputLength)
        {
            return new TelemetryRaceParseResult
            {
                Error = $"Payload zu groß ({json.Length} Zeichen, max. {MaxInputLength}).",
            };
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return new TelemetryRaceParseResult { Error = "Ungültiges JSON." };
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new TelemetryRaceParseResult { Error = "Payload muss ein JSON-Objekt sein." };
            }

            var result = new ParsedTelemetryResult
            {
                Track = ReadOptionalString(root, "track"),
                League = ReadOptionalString(root, "league"),
                Season = ReadOptionalString(root, "season"),
                FastestLap = ReadOptionalString(root, "fastestLap"),
                TotalLaps = ReadOptionalPositiveInt(root, "totalLaps"),
            };

            var error = TryReadDate(root, out var date);
            if (error != null)
            {
                return new TelemetryRaceParseResult { Error = error };
            }
            result.Date = date;

            error = TryReadFinishes(root, result);
            if (error != null)
            {
                return new TelemetryRaceParseResult { Error = error };
            }

            error = TryReadAssignments(root, "reserveAssignments", "reserveDriver", result.ReserveAssignments);
            if (error != null)
            {
                return new TelemetryRaceParseResult { Error = error };
            }

            error = TryReadAssignments(root, "guestAssignments", "guestDriver", result.GuestAssignments);
            if (error != null)
            {
                return new TelemetryRaceParseResult { Error = error };
            }

            return new TelemetryRaceParseResult { Result = result };
        }
    }

    private static string? ReadOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var el) || el.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = el.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>Positive Ganzzahl oder null. Ungültige Werte werden still verworfen statt den
    /// Import abzulehnen — die Distanz ist nur ein Vorschlag für den Punkte-Faktor.</summary>
    private static int? ReadOptionalPositiveInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var el) || el.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return el.TryGetInt32(out var value) && value > 0 ? value : null;
    }

    private static string? TryReadDate(JsonElement root, out DateTime? date)
    {
        date = null;
        if (!root.TryGetProperty("date", out var el))
        {
            return null;
        }

        if (el.ValueKind != JsonValueKind.String)
        {
            return "Feld 'date' muss ein ISO-8601-String sein.";
        }

        var raw = el.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return "Ungültiges Datumsformat in 'date' (erwartet ISO-8601, z. B. 2026-09-09T20:15:00Z).";
        }

        date = parsed;
        return null;
    }

    /// <summary>Liefert null bei Erfolg, sonst eine deutsche Fehlermeldung.</summary>
    private static string? TryReadFinishes(JsonElement root, ParsedTelemetryResult result)
    {
        if (!root.TryGetProperty("finishes", out var finishesEl))
        {
            return "Feld 'finishes' fehlt — mindestens ein Fahrer ist Pflicht.";
        }

        if (finishesEl.ValueKind != JsonValueKind.Array || finishesEl.GetArrayLength() == 0)
        {
            return "Feld 'finishes' muss ein nicht-leeres Array sein.";
        }

        if (finishesEl.GetArrayLength() > MaxFinishes)
        {
            return $"Zu viele Fahrer ({finishesEl.GetArrayLength()}, max. {MaxFinishes}).";
        }

        var seenPositions = new HashSet<int>();
        result.Finishes.Clear();

        foreach (var item in finishesEl.EnumerateArray())
        {
            var error = TryReadFinish(item, seenPositions, out var finish);
            if (error != null)
            {
                return error;
            }

            result.Finishes.Add(finish);
        }

        if (result.Finishes.All(f => f.IsDnf))
        {
            return "Mindestens ein Fahrer muss das Ziel erreicht haben (nicht alle DNF).";
        }

        return null;
    }

    private static string? TryReadFinish(JsonElement item, HashSet<int> seenPositions, out ParsedTelemetryFinish finish)
    {
        finish = null!;

        if (item.ValueKind != JsonValueKind.Object)
        {
            return "Jeder Eintrag in 'finishes' muss ein Objekt sein.";
        }

        if (!item.TryGetProperty("driver", out var driverEl) || driverEl.ValueKind != JsonValueKind.String)
        {
            return "Fahrer-Eintrag fehlt 'driver'.";
        }

        var driver = driverEl.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(driver))
        {
            return "Fahrer-Eintrag hat leeren 'driver'.";
        }

        if (!item.TryGetProperty("position", out var positionEl) || positionEl.ValueKind != JsonValueKind.Number)
        {
            return $"Fahrer '{driver}' fehlt 'position' (0 = DNF).";
        }

        if (!positionEl.TryGetInt32(out var position) || position < 0)
        {
            return $"Fahrer '{driver}' hat ungültige 'position' (0 = DNF, sonst >= 1).";
        }

        // DNF-Semantik wie EnterRace: position 0 ODER explizites 'dnf'-Flag.
        var isDnf = position == 0 || ReadOptionalBool(item, "dnf");

        if (!isDnf)
        {
            if (!seenPositions.Add(position))
            {
                return $"Doppelte Zielposition {position} (Fahrer '{driver}').";
            }
        }

        long? raceTimeMs = null;
        if (item.TryGetProperty("raceTimeMs", out var timeEl)
            && timeEl.ValueKind == JsonValueKind.Number
            && timeEl.TryGetInt64(out var time)
            && time > 0)
        {
            raceTimeMs = time;
        }

        int? qualifyingPosition = null;
        if (item.TryGetProperty("qualifyingPosition", out var qualiEl)
            && qualiEl.ValueKind == JsonValueKind.Number
            && qualiEl.TryGetInt32(out var quali)
            && quali > 0)
        {
            qualifyingPosition = quali;
        }

        finish = new ParsedTelemetryFinish(
            Position: isDnf ? 0 : position,
            Driver: driver,
            IsDnf: isDnf,
            RaceTimeMs: raceTimeMs,
            QualifyingPosition: qualifyingPosition,
            NumLaps: ReadOptionalPositiveInt(item, "numLaps"));
        return null;
    }

    private static string? TryReadAssignments(
        JsonElement root,
        string propertyName,
        string driverPropertyName,
        List<ParsedTelemetryAssignment> target)
    {
        if (!root.TryGetProperty(propertyName, out var el))
        {
            return null; // optional
        }

        if (el.ValueKind != JsonValueKind.Array)
        {
            return $"Feld '{propertyName}' muss ein Array sein.";
        }

        target.Clear();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                return $"Jeder Eintrag in '{propertyName}' muss ein Objekt sein.";
            }

            var driver = ReadOptionalString(item, driverPropertyName);
            if (string.IsNullOrWhiteSpace(driver))
            {
                return $"Eintrag in '{propertyName}' fehlt '{driverPropertyName}'.";
            }

            var mainDriver = ReadOptionalString(item, "mainDriver");
            if (string.IsNullOrWhiteSpace(mainDriver))
            {
                return $"Eintrag in '{propertyName}' fehlt 'mainDriver'.";
            }

            target.Add(new ParsedTelemetryAssignment(driver, mainDriver));
        }

        return null;
    }

    private static bool ReadOptionalBool(JsonElement item, string propertyName)
    {
        return item.TryGetProperty(propertyName, out var el) && el.ValueKind == JsonValueKind.True;
    }
}
