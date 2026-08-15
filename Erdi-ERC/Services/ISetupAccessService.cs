namespace Erdi_ERC.Services
{
    public interface ISetupAccessService
    {
        Task<SetupAccessResolution> ResolveSetupAccessAsync(string? accessToken, CancellationToken cancellationToken = default);
    }

    public sealed class SetupAccessResolution
    {
        public int Tier { get; init; }
        public string? RoleLabel { get; init; }
        public bool Success { get; init; }
        public bool IsOnCommunityGuild { get; init; }
        /// <summary>
        /// True wenn der Fehler auf ein Netzwerk-/API-Problem zurückzuführen ist (Discord nicht erreichbar).
        /// In diesem Fall soll der User NICHT ausgeloggt werden – wir versuchen es beim nächsten Request erneut.
        /// </summary>
        public bool IsTransientError { get; init; }

        /// <summary>
        /// Zeitpunkt des aktuellen Guild-Beitritts (laut Discord). Wird beim Re-Join zurückgesetzt.
        /// Null wenn der User nicht auf der Guild ist oder Discord das Feld nicht zurückgibt.
        /// </summary>
        public DateTimeOffset? GuildJoinedAtUtc { get; init; }

        /// <summary>
        /// True wenn der User auf der Guild ist, aber die Mindest-Mitgliedschaftsdauer
        /// noch nicht erfüllt hat. In diesem Fall ist <see cref="Tier"/> auf 0 reduziert.
        /// </summary>
        public bool IsPendingTenure { get; init; }
    }
}
