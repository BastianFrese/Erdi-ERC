using Erdi_ERC.Models;

namespace Erdi_ERC.Helpers
{
    /// <summary>
    /// Ein Fahrer kann in Ligen, Standings und Finishes unter verschiedenen Namen stehen:
    /// GamerTags, DisplayName und DiscordName. Alle drei gehören ins Match-Set — fehlt einer,
    /// findet der jeweilige Auswerter die Ergebnisse nicht (leeres Fahrer-Profil, zu niedrige
    /// Fahrer-Level). Genau diese Liste lief zuvor pro Aufrufer auseinander.
    /// </summary>
    public static class DriverAliasHelper
    {
        /// <summary>
        /// Getrimmte, nicht-leere Aliase des Profils. Case-insensitiv, weil die Auswertung
        /// (Standings/Finishes) ebenfalls ohne Rücksicht auf Groß-/Kleinschreibung matcht.
        /// Leere Namen fallen raus, damit ein Platzhalter-GamerTag nicht auf Ergebnis-Zeilen
        /// mit leerem Fahrernamen matcht.
        /// </summary>
        public static HashSet<string> Build(DriverProfile profile) =>
            profile.GamerTags
                .Select(t => t.GamerTag.Trim())
                .Append(profile.DisplayName?.Trim() ?? string.Empty)
                .Append(profile.DiscordName.Trim())
                .Where(a => a.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
