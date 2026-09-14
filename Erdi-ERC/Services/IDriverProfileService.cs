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

        /// <summary>Findet ein Profil, dessen Anzeigename, DiscordName oder GamerTag zum Eintrag passt (case-insensitive).</summary>
        Task<DriverProfile?> FindByDriverNameAsync(string driverName, CancellationToken ct = default);

        /// <summary>
        /// Umbenennung eines Fahrers über die Ligaverwaltung (SaveAllStandings):
        /// Nur wenn der alte Name zu einem DriverProfile aufgelöst werden kann, wird
        /// systemweit propagiert (Fahrerkarte/Profil, andere Ligen, Renn-Ergebnisse).
        /// Ohne Profil-Match passiert nichts. Gibt die Anzahl geänderter Referenzen zurück.
        /// </summary>
        Task<int> RenameStandingDriverAsync(string oldName, string newName, string? actorDiscordId, CancellationToken ct = default);

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

        /// <summary>
        /// Liefert eine Liste von DriverStanding-Namen, fuer die es KEIN
        /// DriverProfile und KEINEN DriverGamerTag mit passendem Alias gibt.
        /// Pro Name aggregiert: Leagues, DriverNumber, Team, Anzahl Rennen.
        /// </summary>
        Task<IReadOnlyList<UnlinkedDriver>> GetUnlinkedDriversAsync(CancellationToken ct = default);

        /// <summary>
        /// Erstellt einen neuen DriverProfile + DriverGamerTag fuer einen
        /// bestehenden DriverStanding-Namen, OHNE die Standings umzubenennen.
        /// Alias-Match ueber GamerTag bewirkt, dass das Profil danach
        /// automatisch in /Profile/Driver/{name} und auf der Fahrerkarte
        /// gefunden wird. Idempotent: bereits verknuepfte Namen geben
        /// (false, existingDiscordId) zurueck.
        /// </summary>
        Task<(bool Created, string? ExistingDiscordId, string Message)> LinkDriverAsync(
            string driverName, string discordId, string discordName,
            string? platform, string? actorDiscordId, CancellationToken ct = default);

        /// <summary>
        /// Loescht einen DriverGamerTag + DriverProfile (wenn danach keine
        /// Tags mehr uebrig sind) und gibt die Anzahl geloeschter Tags zurueck.
        /// Safety: bei nicht-leerem DriverGamerTag.Set wird NICHHT geloescht,
        /// stattdessen Result=-1.
        /// </summary>
        Task<int> UnlinkDriverAsync(string discordId, CancellationToken ct = default);
    }

    /// <summary>Aggregierte Sicht auf einen Fahrer, dessen DriverStanding noch
    /// keinen Alias-Match zu einem DriverProfile hat.</summary>
    public record UnlinkedDriver(
        string Name,
        int OccurrenceCount,
        int? DriverNumber,
        string? Team,
        IReadOnlyList<string> LeagueIds,
        int RaceCount);
}
