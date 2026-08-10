using <OWNER_HANDLE>_ERC.Models;

namespace <OWNER_HANDLE>_ERC.Services
{
    /// <summary>
    /// Geschäftslogik für eingehende Bewerbungen. Kapselt Dedup, Wartelisten-Ausleitung,
    /// transaktionales Accept (Profil + GamerTag + Standing + Audit + Webhook) und
    /// Promotion von Wartelisten-Einträgen zu richtigen Bewerbungen.
    /// </summary>
    public interface IApplicationService
    {
        /// <summary>
        /// Reicht eine Bewerbung ein. Leitet Stammfahrer-Bewerbungen auf voller Liga
        /// automatisch auf die Warteliste um. Dedup: pro (DiscordId, Liga) nur ein
        /// offener Eintrag (Application.Status=Pending ODER WaitlistEntry).
        /// </summary>
        Task<SubmitApplicationResult> SubmitAsync(SubmitApplicationCommand cmd, CancellationToken ct);

        Task<Application?> GetByIdAsync(string id, CancellationToken ct);

        Task<IReadOnlyList<Application>> ListAsync(
            ApplicationStatus? statusFilter,
            string? leagueFilter,
            int skip,
            int take,
            CancellationToken ct);

        /// <summary>
        /// Transaktional: upsert DriverProfile + DriverGamerTag + (optional) DriverStanding
        /// + Status=Accepted + 2× Audit + Webhook <see cref="WebhookEvents.ApplicationAccepted"/>.
        /// Auch für Status=Rejected erlaubt (Admin-Re-Activate). Idempotent bzgl.
        /// DriverStanding via Trim+ToLower-Dedup.
        /// </summary>
        Task<AcceptRejectResult> AcceptAsync(string applicationId, string adminDiscordId, string? note, CancellationToken ct);

        /// <summary>Status=Rejected. Auch für Status=Pending und Status=Accepted erlaubt (Admin-Korrektur).</summary>
        Task<AcceptRejectResult> RejectAsync(string applicationId, string adminDiscordId, string? note, CancellationToken ct);

        Task<bool> HasOpenApplicationAsync(string discordId, string targetLeagueId, CancellationToken ct);

        Task<IReadOnlyList<WaitlistEntry>> ListWaitlistAsync(string leagueId, CancellationToken ct);

        /// <summary>
        /// Kopiert einen Wartelisten-Eintrag in eine echte Application (Status=Pending),
        /// setzt <see cref="WaitlistEntry.PromotedToApplicationId"/>. Eintrag bleibt in der
        /// Warteliste sichtbar (Audit-Spur).
        /// </summary>
        Task<PromoteResult> PromoteFromWaitlistAsync(string waitlistEntryId, string adminDiscordId, CancellationToken ct);
    }

    /// <summary>Eingabe für <see cref="IApplicationService.SubmitAsync"/>.</summary>
    public record SubmitApplicationCommand(
        string DiscordId,
        string DiscordName,
        string GamerTag,
        string Platform,
        string TargetLeagueId,
        string Role,
        string? Motivation,
        bool DiscordJoinWarning,
        string? DiscordJoinWarningDetail);

    /// <summary>Ergebnis einer Bewerbungs-Einreichung.</summary>
    public record SubmitApplicationResult(
        SubmitOutcome Outcome,
        Application? Application,
        WaitlistEntry? WaitlistEntry)
    {
        public static SubmitApplicationResult Submitted(Application app) =>
            new(SubmitOutcome.Submitted, app, null);

        public static SubmitApplicationResult Waitlisted(WaitlistEntry entry) =>
            new(SubmitOutcome.Waitlisted, null, entry);

        public static SubmitApplicationResult AlreadyPending(Application existing) =>
            new(SubmitOutcome.AlreadyPending, existing, null);

        public static SubmitApplicationResult AlreadyWaitlisted(WaitlistEntry existing) =>
            new(SubmitOutcome.AlreadyWaitlisted, null, existing);
    }

    public enum SubmitOutcome
    {
        Submitted = 0,
        Waitlisted = 1,
        AlreadyPending = 2,
        AlreadyWaitlisted = 3,
    }

    /// <summary>Ergebnis einer Accept/Reject-Operation.</summary>
    public record AcceptRejectResult(
        AcceptRejectOutcome Outcome,
        Application? Application,
        string? Error)
    {
        public static AcceptRejectResult Ok(Application app) =>
            new(AcceptRejectOutcome.Ok, app, null);

        public static AcceptRejectResult NotFound() =>
            new(AcceptRejectOutcome.NotFound, null, "Bewerbung nicht gefunden.");
    }

    public enum AcceptRejectOutcome
    {
        Ok = 0,
        NotFound = 1,
    }

    /// <summary>Ergebnis einer Waitlist-Promotion.</summary>
    public record PromoteResult(
        PromoteOutcome Outcome,
        Application? Application,
        string? Error)
    {
        public static PromoteResult Ok(Application app) =>
            new(PromoteOutcome.Ok, app, null);

        public static PromoteResult NotFound(string err) =>
            new(PromoteOutcome.NotFound, null, err);
    }

    public enum PromoteOutcome
    {
        Ok = 0,
        NotFound = 1,
    }
}