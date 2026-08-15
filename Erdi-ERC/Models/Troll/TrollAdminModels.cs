using System.Text.Json;

namespace Erdi_ERC.Models.Troll;

/// <summary>Art eines admin-erstellten Inhalts-Gags.</summary>
public enum TrollCustomGagKind
{
    /// <summary>Reine Nachricht/Prank, wegklickbar (kein Server-Check).</summary>
    Message,
    /// <summary>Quiz mit Auswahl-Optionen, serverseitig validiert (Pflicht-Gag).</summary>
    Quiz
}

/// <summary>
/// Pro eingebautem Gag (Key aus <see cref="Services.TrollService"/>) ein optionaler
/// Override: an/aus + Gewicht. Existiert kein Eintrag, gelten die Code-Defaults.
/// </summary>
public sealed class TrollGagOverride
{
    /// <summary>Gag-Key des eingebauten Gags (Primärschlüssel).</summary>
    public required string Key { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>Auswahlgewicht; 0 = praktisch aus (aber Eintrag bleibt aktiviert).</summary>
    public int Weight { get; set; } = 10;
}

/// <summary>
/// Vom Admin in der UI angelegter Inhalts-Gag. Rendert generisch über
/// <c>Views/Troll/Gags/_Custom.cshtml</c>. Quiz-Gags sind blockierend und werden
/// wie die eingebauten Pflicht-Gags über <c>TrollController.Solve</c> geprüft.
/// </summary>
public sealed class TrollCustomGag
{
    public int Id { get; set; }

    /// <summary>Stabiler, eindeutiger Key (Slug), z. B. <c>custom-erdi-quiz</c>.</summary>
    public string Key { get; set; } = string.Empty;

    public TrollCustomGagKind Kind { get; set; } = TrollCustomGagKind.Message;

    public TrollGagCategory Category { get; set; } = TrollGagCategory.GlueckSprueche;

    /// <summary>Kleine Überschrift oben (z. B. „Erdi · Durchsage"). Optional.</summary>
    public string? Eyebrow { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>Einleitungstext unter dem Titel. Optional.</summary>
    public string? Lead { get; set; }

    /// <summary>Nachrichtentext (nur bei <see cref="TrollCustomGagKind.Message"/>).</summary>
    public string? Body { get; set; }

    /// <summary>Quizfrage (nur bei <see cref="TrollCustomGagKind.Quiz"/>).</summary>
    public string? Question { get; set; }

    /// <summary>Erwartete Antwort (Quiz). Tolerant geprüft (getrimmt, Groß-/Kleinschreibung egal).</summary>
    public string? Answer { get; set; }

    /// <summary>Antwort-Optionen als JSON-Array (Quiz). Eine davon muss <see cref="Answer"/> sein.</summary>
    public string? OptionsJson { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int Weight { get; set; } = 10;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; }

    /// <summary>Deserialisiert die Quiz-Optionen defensiv; bei kaputtem JSON leere Liste.</summary>
    public IReadOnlyList<string> Options()
    {
        if (string.IsNullOrWhiteSpace(OptionsJson)) return Array.Empty<string>();
        try
        {
            return JsonSerializer.Deserialize<string[]>(OptionsJson) ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}

/// <summary>
/// Globale Troll-Settings als Single-Row-Tabelle (Id = 1). Überschreibt zur Laufzeit
/// die appsettings-Werte aus <see cref="Options.TrollOptions"/>. Existiert keine Zeile,
/// gelten die appsettings/Code-Defaults (fail-open).
/// </summary>
public sealed class TrollSettingsEntity
{
    public int Id { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public double TriggerChance { get; set; } = 0.25;
    public int CooldownMinutes { get; set; } = 360;
    public bool ApplyToAdmins { get; set; } = true;
    public int MercyAfterAttempts { get; set; } = 3;
    public int MathMaxOperand { get; set; } = 9;
}

/// <summary>Eine Zeile der Admin-Übersicht für einen eingebauten Gag (Def + aktueller Override-Stand).</summary>
public sealed class TrollAdminGagRow
{
    public required TrollGagDefinition Def { get; init; }
    public bool IsEnabled { get; init; }
    public int Weight { get; init; }

    /// <summary>True, wenn ein DB-Override existiert (sonst gelten Code-Defaults).</summary>
    public bool IsOverridden { get; init; }
}

/// <summary>View-Model der Admin-Troll-Übersicht.</summary>
public sealed class TrollAdminViewModel
{
    public required TrollSettingsEntity Settings { get; init; }
    public required IReadOnlyList<TrollAdminGagRow> BuiltIns { get; init; }
    public required IReadOnlyList<TrollCustomGag> Custom { get; init; }
}
