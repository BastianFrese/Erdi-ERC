using Erdi_ERC.Models;

namespace Erdi_ERC.Services
{
    public record DriverNameSuggestion(
        string DiscordId,
        string Platform,
        string GamerTag,
        string DiscordName,
        int Distance,
        bool ExactMatch);

    public record DriverNameMatch(
        string ResolvedName,
        string? DiscordId,
        string? Platform,
        bool ExactMatch,
        IReadOnlyList<DriverNameSuggestion> Suggestions);

    public interface IDriverProfileService
    {
        /// <summary>
        /// Liefert Vorschläge zu einer Eingabe (Exact-Match + Fuzzy via Levenshtein).
        /// </summary>
        Task<IReadOnlyList<DriverNameSuggestion>> SuggestAsync(string query, CancellationToken ct = default);

        /// <summary>
        /// Versucht einen eingetragenen Namen kanonisch aufzulösen (z. B. beim Speichern eines Standings).
        /// </summary>
        Task<DriverNameMatch> ResolveAsync(string input, CancellationToken ct = default);

        /// <summary>Profil inkl. aller Tags laden.</summary>
        Task<DriverProfile?> GetByDiscordIdAsync(string discordId, CancellationToken ct = default);

        /// <summary>Findet ein Profil, dessen Anzeigename oder GamerTag exakt zum Eintrag passt.</summary>
        Task<DriverProfile?> FindByDriverNameAsync(string driverName, CancellationToken ct = default);

        /// <summary>
        /// Benennt den Ingame-Namen (plattformunabhängig) um und propagiert die Änderung
        /// in alle abhängigen Tabellen (Standings, Finishes, Penalties, Achievements, …).
        /// Gibt die Anzahl geänderter Referenzen zurück.
        /// </summary>
        Task<int> RenameIngameNameAsync(string discordId, string newName, string? actorDiscordId, CancellationToken ct = default);

        /// <summary>
        /// Liefert die effektive Farbe der Fahrernummer: explizit gespeicherte Farbe,
        /// sonst Team-Primary-Farbe, sonst Default #e10600.
        /// </summary>
        string ResolveDriverNumberColor(DriverProfile profile);

        /// <inheritdoc cref="RenameIngameNameAsync"/>
        [Obsolete("Use RenameIngameNameAsync")]
        Task<int> RenameEaNameAsync(string discordId, string newName, string? actorDiscordId, CancellationToken ct = default);
    }
}
