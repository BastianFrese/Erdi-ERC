using <OWNER_HANDLE>_ERC.Models;

namespace <OWNER_HANDLE>_ERC.Services
{
    /// <summary>
    /// Schreibender Bewerbungs-Workflow als State-Machine mit genau EINEM Pfad je Übergang:
    /// Open → Accepted (<see cref="AcceptAsync"/>, inkl. Liga-Eintrag),
    /// Open → Rejected (<see cref="RejectAsync"/>),
    /// Accepted/Rejected → Open (<see cref="ReopenAsync"/>).
    /// Alle Übergänge sind transaktional und auditiert. Lesezugriffe liegen in
    /// <see cref="IApplicationQueryService"/>.
    /// </summary>
    public interface IApplicationWorkflowService
    {
        /// <summary>
        /// Nimmt eine Bewerbung an: verknüpft das Fahrer-Profil, trägt den Fahrer in die
        /// Ziel-Liga ein (<paramref name="leagueId"/>, sonst die beworbene Liga) und
        /// persistiert die tatsächlich zugewiesene Liga in
        /// <see cref="ApplicationForm.AssignedLeagueId"/>.
        /// </summary>
        Task<ApplicationActionResult> AcceptAsync(int id, string? leagueId, string? assignedRole, string actorId);

        /// <summary>Lehnt eine nicht angenommene Bewerbung ab (mit Grund).</summary>
        Task<ApplicationActionResult> RejectAsync(int id, string reason, string actorId);

        /// <summary>
        /// Setzt eine angenommene ODER abgelehnte Bewerbung zurück auf "offen".
        /// Bei Angenommenen wird der leere Auto-Standing-Eintrag der zugewiesenen Liga
        /// entfernt; bei Abgelehnten greift der Dedup-Guard (keine zweite aktive Bewerbung).
        /// </summary>
        Task<ApplicationActionResult> ReopenAsync(int id, string? note, string actorId);

        /// <summary>Zieht einen angenommenen Fahrer in eine andere Liga um (inkl. Standings-Pflege).</summary>
        Task<ApplicationActionResult> MoveToLeagueAsync(int id, string leagueId, string? assignedRole, string actorId);

        /// <summary>Löscht eine Bewerbung endgültig; leere Auto-Standings werden mit abgeräumt.</summary>
        Task<ApplicationActionResult> DeleteAsync(int id, string actorId);

        /// <summary>Markiert eine offene Bewerbung zur Überprüfung (Grund landet in der Review-Notiz).</summary>
        Task<ApplicationActionResult> FlagAsync(int id, string reason, string actorId);

        /// <summary>Hebt die Überprüfungs-Markierung wieder auf.</summary>
        Task<ApplicationActionResult> UnflagAsync(int id, string actorId);

        /// <summary>Setzt oder beendet die Probezeit einer (angenommenen) Bewerbung.</summary>
        Task<ApplicationActionResult> SetTrialAsync(int id, bool onTrial, int? days, string actorId);

        /// <summary>Entfernt abgelehnte Bewerbungen, die älter als 30 Tage sind. Liefert die Anzahl.</summary>
        Task<int> CleanupExpiredAsync(string actorId);
    }

    /// <summary>Ergebnis einer Bewerbungs-Operation.</summary>
    public class ApplicationActionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ErrorCode { get; set; }
        public ApplicationForm? UpdatedApplication { get; set; }

        public static ApplicationActionResult SuccessResult(string message, ApplicationForm? app = null)
            => new() { Success = true, Message = message, UpdatedApplication = app };

        public static ApplicationActionResult ErrorResult(string message, string errorCode)
            => new() { Success = false, Message = message, ErrorCode = errorCode };
    }
}
