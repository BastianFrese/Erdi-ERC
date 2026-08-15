using Erdi_ERC.Models.Troll;

namespace Erdi_ERC.Services;

/// <summary>
/// Logik des Erdi-Troll-Systems: Auslöse-Würfel, gewichtete Gag-Auswahl und
/// Challenge-Erzeugung. Bewusst HTTP-frei (kein TempData/Cookie hier) – damit sind
/// die reinen Roll-/Pick-Helfer deterministisch unit-testbar.
/// </summary>
public interface ITrollService
{
    /// <summary>Master-Schalter (effektiv: DB-Settings überschreiben appsettings, sonst Default).</summary>
    bool IsEnabled { get; }

    /// <summary>Alle fest eingebauten Gags (Code-Katalog) – für die Admin-Übersicht.</summary>
    IReadOnlyList<TrollGagDefinition> BuiltInCatalog { get; }

    /// <summary>Cooldown-Dauer; in dieser Zeit wird ein User nach einem Troll nicht erneut getrollt.</summary>
    TimeSpan Cooldown { get; }

    /// <summary>Fehlversuche bei Pflicht-Gags, ab denen der Gnaden-Durchlass angeboten wird.</summary>
    int MercyAfterAttempts { get; }

    /// <summary>Ob auch Admins getrollt werden (Config). Dank Gnaden-Ausweg droht kein Aussperren.</summary>
    bool AppliesToAdmins { get; }

    /// <summary>Würfelt anhand der konfigurierten Wahrscheinlichkeit, ob überhaupt ein Troll auslöst.</summary>
    bool RollShouldTrigger();

    /// <summary>Wählt gewichtet einen aktiven Gag aus dem Katalog.</summary>
    TrollGagDefinition PickGag();

    /// <summary>Findet einen Gag per Key (z. B. aus TempData rekonstruiert); null wenn unbekannt.</summary>
    TrollGagDefinition? FindGag(string? key);

    /// <summary>Erzeugt die konkrete Challenge (Mathe-Aufgabe, erwartete Antwort …).</summary>
    TrollChallenge BuildChallenge(TrollGagDefinition gag);

    /// <summary>Prüft die User-Antwort tolerant (getrimmt, Groß-/Kleinschreibung egal) gegen die erwartete.</summary>
    bool IsAnswerCorrect(string? expected, string? userAnswer);

    /// <summary>Findet einen aktiven, admin-erstellten Inhalts-Gag per Key (für das Rendering); null sonst.</summary>
    Models.Troll.TrollCustomGag? FindCustomGag(string? key);

    /// <summary>Verwirft den Katalog-/Settings-Cache, sodass Admin-Änderungen sofort greifen.</summary>
    void InvalidateCache();
}
