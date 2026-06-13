using System.Globalization;

namespace <OWNER_HANDLE>_ERC.Models;

/// <summary>
/// Beschreibt eine F1 25 Strecke mit voller Renndistanz, Standard-Tyre-Wear-Profil
/// und einer Pit-Loss-Schätzung in Sekunden. Wird vom Strategie-Generator genutzt,
/// um automatisch Stints und Pit-Fenster vorzuschlagen.
/// </summary>
public sealed class F1Track
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Country { get; init; }
    public required int FullRaceLaps { get; init; }
    /// <summary>low / medium / high – beeinflusst Anzahl der Stops bei langen Distanzen.</summary>
    public string TyreWear { get; init; } = "medium";
    /// <summary>Geschätzter Zeitverlust in Sekunden pro Boxenstopp (inkl. Boxengasse).</summary>
    public double PitLossSeconds { get; init; } = 22.0;
    /// <summary>Empfohlener Trockenreifen-Mix bei langem Rennen (Compounds).</summary>
    public string PreferredCompounds { get; init; } = "Medium → Hard";
}

/// <summary>
/// Auswahl an Renn-Längen analog zu den F1 25 Spiel-Optionen.
/// </summary>
public sealed class F1RaceLength
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    /// <summary>0 = absolute Runden via <see cref="FixedLaps"/>, sonst Anteil 0..1 der Volldistanz.</summary>
    public double Percent { get; init; }
    public int FixedLaps { get; init; }
    public string Icon { get; init; } = "🏁";

    public int LapsFor(F1Track track)
    {
        if (FixedLaps > 0) return FixedLaps;
        var laps = (int)Math.Round(track.FullRaceLaps * Percent, MidpointRounding.AwayFromZero);
        return Math.Max(1, laps);
    }
}

public sealed class F1StrategyStint
{
    public required string Compound { get; init; }
    public required int Laps { get; init; }
}

public sealed class F1StrategyPlan
{
    public required string TrackKey { get; init; }
    public required string TrackName { get; init; }
    public required string LengthKey { get; init; }
    public required string LengthLabel { get; init; }
    public required int TotalLaps { get; init; }
    public required int Stops { get; init; }
    public required IReadOnlyList<F1StrategyStint> Stints { get; init; }
    public required IReadOnlyList<int> PitWindow { get; init; }
    public required string Notes { get; init; }

    public string ToHumanReadable()
    {
        var ci = CultureInfo.InvariantCulture;
        var stintText = string.Join(" → ", Stints.Select(s => $"{s.Laps}× {s.Compound}"));
        var pitText = PitWindow.Count == 0
            ? "ohne Pflicht-Boxenstopp"
            : "Pit-Fenster: Runde " + string.Join(" / ", PitWindow.Select(l => l.ToString(ci)));
        return $"{TrackName} · {LengthLabel} · {TotalLaps} Runden · {Stops} Stop(s)\n"
             + $"Stints: {stintText}\n"
             + pitText
             + (string.IsNullOrWhiteSpace(Notes) ? string.Empty : "\nHinweis: " + Notes);
    }
}

public static class F1RaceCatalog
{
    // F1 25 Kalender mit Standard-Renndistanz (volle Distanz). Werte basieren auf FIA-Renndistanzen.
    public static IReadOnlyList<F1Track> Tracks { get; } =
    [
        new F1Track { Key = "bahrain",       Name = "Bahrain International Circuit",   Country = "Bahrain",       FullRaceLaps = 57, TyreWear = "high",   PitLossSeconds = 22.5, PreferredCompounds = "Soft → Hard → Hard" },
        new F1Track { Key = "jeddah",        Name = "Jeddah Corniche Circuit",         Country = "Saudi-Arabien", FullRaceLaps = 50, TyreWear = "low",    PitLossSeconds = 19.5, PreferredCompounds = "Medium → Hard" },
        new F1Track { Key = "australia",     Name = "Albert Park Circuit",             Country = "Australien",    FullRaceLaps = 58, TyreWear = "medium", PitLossSeconds = 21.0, PreferredCompounds = "Medium → Hard" },
        new F1Track { Key = "japan",         Name = "Suzuka International Racing Course", Country = "Japan",      FullRaceLaps = 53, TyreWear = "high",   PitLossSeconds = 22.0, PreferredCompounds = "Medium → Hard → Hard" },
        new F1Track { Key = "china",         Name = "Shanghai International Circuit",  Country = "China",         FullRaceLaps = 56, TyreWear = "high",   PitLossSeconds = 22.0, PreferredCompounds = "Medium → Hard → Hard" },
        new F1Track { Key = "miami",         Name = "Miami International Autodrome",   Country = "USA",           FullRaceLaps = 57, TyreWear = "medium", PitLossSeconds = 22.0, PreferredCompounds = "Medium → Hard" },
        new F1Track { Key = "imola",         Name = "Autodromo Enzo e Dino Ferrari",   Country = "Italien",       FullRaceLaps = 63, TyreWear = "low",    PitLossSeconds = 26.0, PreferredCompounds = "Medium → Hard" },
        new F1Track { Key = "monaco",        Name = "Circuit de Monaco",               Country = "Monaco",        FullRaceLaps = 78, TyreWear = "low",    PitLossSeconds = 22.0, PreferredCompounds = "Medium → Hard" },
        new F1Track { Key = "canada",        Name = "Circuit Gilles Villeneuve",       Country = "Kanada",        FullRaceLaps = 70, TyreWear = "medium", PitLossSeconds = 18.0, PreferredCompounds = "Medium → Hard" },
        new F1Track { Key = "spain",         Name = "Circuit de Barcelona-Catalunya",  Country = "Spanien",       FullRaceLaps = 66, TyreWear = "high",   PitLossSeconds = 22.0, PreferredCompounds = "Medium → Hard → Hard" },
        new F1Track { Key = "austria",       Name = "Red Bull Ring",                   Country = "Österreich",    FullRaceLaps = 71, TyreWear = "medium", PitLossSeconds = 21.0, PreferredCompounds = "Medium → Hard" },
        new F1Track { Key = "silverstone",   Name = "Silverstone Circuit",             Country = "Großbritannien", FullRaceLaps = 52, TyreWear = "high",  PitLossSeconds = 22.0, PreferredCompounds = "Medium → Hard → Hard" },
        new F1Track { Key = "hungary",       Name = "Hungaroring",                     Country = "Ungarn",        FullRaceLaps = 70, TyreWear = "medium", PitLossSeconds = 21.0, PreferredCompounds = "Medium → Hard" },
        new F1Track { Key = "belgium",       Name = "Circuit de Spa-Francorchamps",    Country = "Belgien",       FullRaceLaps = 44, TyreWear = "medium", PitLossSeconds = 21.0, PreferredCompounds = "Medium → Hard" },
        new F1Track { Key = "netherlands",   Name = "Circuit Zandvoort",               Country = "Niederlande",   FullRaceLaps = 72, TyreWear = "medium", PitLossSeconds = 21.0, PreferredCompounds = "Medium → Hard" },
        new F1Track { Key = "italy",         Name = "Autodromo Nazionale Monza",       Country = "Italien",       FullRaceLaps = 53, TyreWear = "low",    PitLossSeconds = 22.0, PreferredCompounds = "Medium → Hard" },
        new F1Track { Key = "azerbaijan",    Name = "Baku City Circuit",               Country = "Aserbaidschan", FullRaceLaps = 51, TyreWear = "low",    PitLossSeconds = 19.5, PreferredCompounds = "Medium → Hard" },
        new F1Track { Key = "singapore",     Name = "Marina Bay Street Circuit",       Country = "Singapur",      FullRaceLaps = 62, TyreWear = "medium", PitLossSeconds = 27.5, PreferredCompounds = "Medium → Hard" },
        new F1Track { Key = "usa",           Name = "Circuit of the Americas",         Country = "USA",           FullRaceLaps = 56, TyreWear = "high",   PitLossSeconds = 22.0, PreferredCompounds = "Medium → Hard → Hard" },
        new F1Track { Key = "mexico",        Name = "Autódromo Hermanos Rodríguez",    Country = "Mexiko",        FullRaceLaps = 71, TyreWear = "medium", PitLossSeconds = 22.5, PreferredCompounds = "Medium → Hard" },
        new F1Track { Key = "brazil",        Name = "Autódromo José Carlos Pace (Interlagos)", Country = "Brasilien", FullRaceLaps = 71, TyreWear = "medium", PitLossSeconds = 22.0, PreferredCompounds = "Medium → Hard" },
        new F1Track { Key = "lasvegas",      Name = "Las Vegas Strip Circuit",         Country = "USA",           FullRaceLaps = 50, TyreWear = "low",    PitLossSeconds = 21.0, PreferredCompounds = "Medium → Hard" },
        new F1Track { Key = "qatar",         Name = "Lusail International Circuit",    Country = "Katar",         FullRaceLaps = 57, TyreWear = "high",   PitLossSeconds = 22.0, PreferredCompounds = "Medium → Hard → Hard" },
        new F1Track { Key = "abudhabi",      Name = "Yas Marina Circuit",              Country = "UAE",           FullRaceLaps = 58, TyreWear = "medium", PitLossSeconds = 22.0, PreferredCompounds = "Medium → Hard" },
        new F1Track { Key = "madrid",         Name = "Circuito de Madrid",              Country = "Spanien",       FullRaceLaps = 57, TyreWear = "medium", PitLossSeconds = 22.0, PreferredCompounds = "Medium → Hard" }
    ];

    // Renn-Längen wie sie im F1 25 Spielmenü zur Auswahl stehen.
    public static IReadOnlyList<F1RaceLength> Lengths { get; } =
    [
        new F1RaceLength { Key = "laps3",   Label = "3 Runde",       FixedLaps = 3,  Icon = "⚡" },
        new F1RaceLength { Key = "laps5",   Label = "5 Runden",     FixedLaps = 5,  Icon = "⚡" },
        new F1RaceLength { Key = "pct25",   Label = "25% Distanz",  Percent = 0.25, Icon = "🟢" },
        new F1RaceLength { Key = "pct35",   Label = "35% Distanz",  Percent = 0.35, Icon = "🟢" },
        new F1RaceLength { Key = "pct50",   Label = "50% Distanz",  Percent = 0.50, Icon = "🟡" },
        new F1RaceLength { Key = "pct100",  Label = "100% Distanz", Percent = 1.00, Icon = "🔴" }
    ];

    public static F1Track? FindTrack(string? key)
        => string.IsNullOrWhiteSpace(key) ? null : Tracks.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));

    public static F1RaceLength? FindLength(string? key)
        => string.IsNullOrWhiteSpace(key) ? null : Lengths.FirstOrDefault(l => string.Equals(l.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Generiert einen heuristischen Strategie-Vorschlag (Stints + Pit-Fenster) für die Kombination
    /// aus Strecke und Renn-Länge. Bewusst konservativ – soll als Vorlage im Editor dienen.
    /// </summary>
    public static F1StrategyPlan BuildPlan(F1Track track, F1RaceLength length)
    {
        var laps = length.LapsFor(track);
        var wear = (track.TyreWear ?? "medium").ToLowerInvariant();

        // Anzahl der Stops abhängig von Distanz und Reifenverschleiß.
        int stops = laps switch
        {
            <= 6 => 0,
            <= 12 => wear == "high" ? 1 : 0,
            <= 20 => wear == "high" ? 1 : (wear == "low" ? 0 : 1),
            <= 35 => wear == "high" ? 2 : 1,
            <= 55 => wear == "low" ? 1 : 2,
            _ => wear == "high" ? 3 : 2
        };

        var stints = SplitStints(laps, stops);
        var compounds = PickCompounds(stints.Count, wear, laps);
        var stintList = stints
            .Select((s, i) => new F1StrategyStint { Compound = compounds[i], Laps = s })
            .ToList();

        var pitWindow = new List<int>();
        var run = 0;
        for (var i = 0; i < stintList.Count - 1; i++)
        {
            run += stintList[i].Laps;
            pitWindow.Add(run);
        }

        var notes = stops == 0
            ? "Kein Pflicht-Stop notwendig – auf Reifenmanagement achten."
            : $"Pit-Loss ca. {track.PitLossSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s. Bevorzugter Mix: {track.PreferredCompounds}.";

        return new F1StrategyPlan
        {
            TrackKey = track.Key,
            TrackName = track.Name,
            LengthKey = length.Key,
            LengthLabel = length.Label,
            TotalLaps = laps,
            Stops = stops,
            Stints = stintList,
            PitWindow = pitWindow,
            Notes = notes
        };
    }

    private static List<int> SplitStints(int totalLaps, int stops)
    {
        var stintCount = stops + 1;
        var baseLen = totalLaps / stintCount;
        var remainder = totalLaps - (baseLen * stintCount);
        var result = new List<int>(stintCount);
        for (var i = 0; i < stintCount; i++)
        {
            var len = baseLen + (i < remainder ? 1 : 0);
            result.Add(Math.Max(1, len));
        }
        return result;
    }

    private static List<string> PickCompounds(int stintCount, string wear, int laps)
    {
        // Sehr kurze Sprints: nur Soft. Mittlere Distanzen: Medium/Hard. Lange Distanzen: Medium → Hard (→ Hard).
        if (laps <= 6)
        {
            return Enumerable.Repeat("Soft", stintCount).ToList();
        }

        return stintCount switch
        {
            1 => [wear == "high" ? "Hard" : "Medium"],
            2 => ["Medium", "Hard"],
            3 => wear == "high" ? ["Soft", "Medium", "Hard"] : ["Medium", "Hard", "Hard"],
            4 => ["Soft", "Medium", "Hard", "Hard"],
            _ => Enumerable.Repeat("Hard", stintCount).ToList()
        };
    }
}
