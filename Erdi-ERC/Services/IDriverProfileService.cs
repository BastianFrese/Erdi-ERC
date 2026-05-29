using <OWNER_HANDLE>_ERC.Models;

namespace <OWNER_HANDLE>_ERC.Services
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
        /// Legt ein Profil an oder aktualisiert es und verknüpft den eingetragenen
        /// GamerTag der Bewerbung mit dem Discord-Profil.
        /// </summary>
        Task<DriverProfile> LinkApplicationAsync(ApplicationForm app, string? actorDiscordId, CancellationToken ct = default);

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
    }
}
