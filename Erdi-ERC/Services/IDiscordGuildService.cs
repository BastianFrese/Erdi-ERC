namespace <OWNER_HANDLE>_ERC.Services
{
    /// <summary>Ausgang der Discord-Guild-Verifikation.</summary>
    public enum DiscordGuildCheckStatus
    {
        /// <summary>Abfrage erfolgreich — Mitgliedschafts-Flags sind gültig.</summary>
        Ok,

        /// <summary>Access-Token fehlt oder ist abgelaufen — Re-Login nötig.</summary>
        LoginExpired,

        /// <summary>Discord-API nicht erreichbar oder Fehlerantwort.</summary>
        Unavailable
    }

    /// <summary>Ergebnis der Guild-Verifikation eines Bewerbers.</summary>
    public record DiscordGuildCheckResult(
        DiscordGuildCheckStatus Status,
        bool JoinedCommunity,
        bool JoinedLeague,
        string? ErrorMessage)
    {
        public bool IsOk => Status == DiscordGuildCheckStatus.Ok;
    }

    /// <summary>
    /// Prüft server-seitig gegen die Discord-API, ob ein Nutzer Mitglied im
    /// Community- und im Liga-Discord ist. Client-seitig gemeldete Flags werden
    /// im Bewerbungs-Flow grundsätzlich ignoriert und hierüber frisch verifiziert.
    /// </summary>
    public interface IDiscordGuildService
    {
        /// <summary>
        /// Fragt die Guild-Mitgliedschaften des Nutzers mit dessen OAuth-Access-Token ab.
        /// Ein leerer/fehlender Token liefert <see cref="DiscordGuildCheckStatus.LoginExpired"/>.
        /// </summary>
        Task<DiscordGuildCheckResult> CheckMembershipAsync(string? accessToken, CancellationToken ct = default);
    }
}
