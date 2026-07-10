namespace <OWNER_HANDLE>_ERC.Models.Troll;

/// <summary>Thematische Gruppierung der Gags (für Übersicht &amp; Tuning).</summary>
public enum TrollGagCategory
{
    MatheKoepfchen,
    FakeSystem,
    ReaktionGeschick,
    GlueckSprueche
}

/// <summary>Registry-Eintrag: statische Metadaten eines einzelnen Troll-Gags.</summary>
public sealed class TrollGagDefinition
{
    /// <summary>Stabiler Key (TempData, Weights-Override, Partial-Auswahl), z. B. <c>"math"</c>.</summary>
    public required string Key { get; init; }

    /// <summary>Partial-View relativ zu <c>/Views/Troll/</c>, z. B. <c>"Gags/_Math"</c>.</summary>
    public required string PartialName { get; init; }

    public TrollGagCategory Category { get; init; }

    /// <summary>True = serverseitig validiert (muss gelöst werden): Mathe, Quiz, Captcha.</summary>
    public bool IsBlocking { get; init; }

    /// <summary>Relatives Auswahlgewicht, falls in <see cref="Options.TrollOptions.Weights"/> nicht überschrieben.</summary>
    public int DefaultWeight { get; init; } = 10;
}

/// <summary>Konkrete, pro Login erzeugte Ausprägung eines Gags.</summary>
public sealed class TrollChallenge
{
    public required string GagKey { get; init; }

    /// <summary>Anzeige-Prompt (z. B. <c>"7 + 5"</c>); null bei Gags ohne dynamischen Prompt.</summary>
    public string? Prompt { get; init; }

    /// <summary>Erwartete Antwort bei Blocking-Gags (z. B. <c>"12"</c> oder <c>"<OWNER_HANDLE>"</c>); null sonst.</summary>
    public string? ExpectedAnswer { get; init; }
}

/// <summary>Eine F1-Quizfrage für den <c>f1-trivia</c>-Gag: Frage, korrekte Antwort und Auswahl-Optionen.</summary>
public sealed record TrollTriviaQuestion(string Question, string Answer, IReadOnlyList<string> Options);

/// <summary>
/// Statische Fragenbank für den <c>f1-trivia</c>-Pflicht-Gag. Bewusst Daten-only und ohne RNG,
/// damit sowohl <see cref="Services.TrollService.BuildChallenge"/> (zieht zufällig eine Frage)
/// als auch das Partial (rendert die Optionen anhand der gespeicherten Frage) dieselbe Quelle nutzen.
/// </summary>
public static class TrollTrivia
{
    public static readonly IReadOnlyList<TrollTriviaQuestion> Questions = new[]
    {
        new TrollTriviaQuestion("In welchem Land liegt die Strecke Monza?", "Italien",
            new[] { "Italien", "Spanien", "Belgien", "Österreich" }),
        new TrollTriviaQuestion("Wie viele Punkte gibt es für einen Sieg im aktuellen F1-System?", "25",
            new[] { "10", "18", "25", "50" }),
        new TrollTriviaQuestion("Welche Flagge signalisiert das Rennende?", "Schwarz-weiß kariert",
            new[] { "Gelb", "Schwarz-weiß kariert", "Rot", "Blau" }),
        new TrollTriviaQuestion("Was musst du tun, wenn dir eine blaue Flagge gezeigt wird?", "Platz machen",
            new[] { "Boxengasse anfahren", "Platz machen", "Anhalten", "Beschleunigen" }),
        new TrollTriviaQuestion("Wie viele Räder hat ein Formel-1-Auto?", "4",
            new[] { "2", "3", "4", "6" }),
        new TrollTriviaQuestion("Wer ist laut <OWNER_HANDLE> der beste Fahrer aller Zeiten?", "<OWNER_HANDLE>",
            new[] { "Max Verstappen", "<OWNER_HANDLE>", "Ayrton Senna", "Michael Schumacher" })
    };

    /// <summary>Findet eine Frage anhand ihres exakten Fragetexts (aus TempData rekonstruiert); null wenn unbekannt.</summary>
    public static TrollTriviaQuestion? ByQuestion(string? question)
        => string.IsNullOrEmpty(question) ? null : Questions.FirstOrDefault(q => q.Question == question);
}

/// <summary>View-Model der Gate-Seite; wird an das gewählte Gag-Partial weitergereicht.</summary>
public sealed class TrollGateViewModel
{
    public required TrollGagDefinition Gag { get; init; }

    /// <summary>Partial-View dieses Gags (Convenience für <c>Gate.cshtml</c>).</summary>
    public string PartialName => Gag.PartialName;

    /// <summary>Dynamischer Prompt (z. B. die Mathe-Aufgabe oder die Custom-Quizfrage).</summary>
    public string? Prompt { get; init; }

    /// <summary>Bei admin-erstellten Inhalts-Gags die zugehörigen Daten (Titel/Text/Optionen); sonst null.</summary>
    public TrollCustomGag? Custom { get; init; }

    /// <summary>Bisherige Fehlversuche bei Blocking-Gags.</summary>
    public int Attempts { get; init; }

    /// <summary>True, sobald mindestens ein Fehlversuch vorliegt — zeigt „<OWNER_HANDLE> schüttelt den Kopf".</summary>
    public bool ShowWrongAnswer => Gag.IsBlocking && Attempts > 0;

    /// <summary>True, sobald <OWNER_HANDLE> gnädig wird und einen Durchlass-Button anbietet.</summary>
    public bool MercyOffered { get; init; }
}
