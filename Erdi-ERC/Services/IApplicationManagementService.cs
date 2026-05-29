using <OWNER_HANDLE>_ERC.Models;

namespace <OWNER_HANDLE>_ERC.Services
{
    /// <summary>
    /// Umfassender Service für die Verwaltung von Bewerbungen mit Status-Tracking,
    /// Validierung und strukturiertem Workflow.
    /// </summary>
    public interface IApplicationManagementService
    {
        // ---- Status & Tracking ----
        /// <summary>
        /// Ruft alle Bewerbungen mit optionalen Filtern ab.
        /// </summary>
        Task<List<ApplicationForm>> GetAllApplicationsAsync(
            string? divisionFilter = null,
            bool? acceptedFilter = null,
            int pageSize = 100,
            int pageNumber = 1);

        /// <summary>
        /// Ruft Bewerbungsstatistiken ab (offene, akzeptierte, pro Division).
        /// </summary>
        Task<ApplicationStatistics> GetStatisticsAsync();

        /// <summary>
        /// Ruft eine einzelne Bewerbung mit Audit-History ab.
        /// </summary>
        Task<ApplicationFormWithHistory?> GetApplicationWithHistoryAsync(int id);

        // ---- Workflow ----
        /// <summary>
        /// Akzeptiert eine Bewerbung und erstellt das Fahrer-Profil.
        /// </summary>
        Task<ApplicationActionResult> AcceptApplicationAsync(int id, string actorId);

        /// <summary>
        /// Lehnt eine Bewerbung ab (mit Grund).
        /// </summary>
        Task<ApplicationActionResult> RejectApplicationAsync(int id, string reason, string actorId);

        /// <summary>
        /// Stellt eine angenommene Bewerbung wieder her (z.B. bei Fehler).
        /// </summary>
        Task<ApplicationActionResult> UnacceptApplicationAsync(int id, string reason, string actorId);

        /// <summary>
        /// Löscht eine Bewerbung (nur in bestimmten States).
        /// </summary>
        Task<ApplicationActionResult> DeleteApplicationAsync(int id, string actorId);

        /// <summary>
        /// Markiert eine Bewerbung als "Review erforderlich".
        /// </summary>
        Task<ApplicationActionResult> FlagForReviewAsync(int id, string reason, string actorId);

        // ---- Automatisierung ----
        /// <summary>
        /// Entfernt abgelaufene akzeptierte Bewerbungen (48h).
        /// </summary>
        Task<int> RemoveExpiredApplicationsAsync();

        /// <summary>
        /// Batch-Verarbeitung: Akzeptiert mehrere Bewerbungen gleichzeitig.
        /// </summary>
        Task<BatchApplicationResult> AcceptMultipleAsync(int[] applicationIds, string actorId);

        // ---- Reporting ----
        /// <summary>
        /// Exportiert Bewerbungen als CSV.
        /// </summary>
        Task<string> ExportAsCSVAsync(string? divisionFilter = null);

        /// <summary>
        /// Ruft Bewerbungs-Metriken ab (Response-Zeit, Annahmequote, etc.).
        /// </summary>
        Task<ApplicationMetrics> GetMetricsAsync();

        /// <summary>Ruft eine einzelne Bewerbung anhand ihrer ID ab.</summary>
        Task<ApplicationForm?> GetApplicationByIdAsync(int id);

        /// <summary>Speichert direkte Änderungen an einer Bewerbung (Review-Status, Rolle, Notiz).</summary>
        Task UpdateApplicationDirectAsync(ApplicationForm app, string actorId);
    }

    // ---- DTOs / Response Models ----

    /// <summary>Statistiken über alle Bewerbungen.</summary>
    public class ApplicationStatistics
    {
        public int TotalApplications { get; set; }
        public int OpenApplications { get; set; }
        public int AcceptedApplications { get; set; }
        public int RejectedApplications { get; set; }
        public int FlaggedForReviewApplications { get; set; }
        public Dictionary<string, int> ApplicationsByDivision { get; set; } = new();
        public Dictionary<string, int> ApplicationsByRole { get; set; } = new();
        public DateTime? LastApplicationTime { get; set; }
    }

    /// <summary>Bewerbung mit Audit-History.</summary>
    public class ApplicationFormWithHistory
    {
        public ApplicationForm Application { get; set; } = null!;
        public List<ApplicationAuditEntry> AuditHistory { get; set; } = new();
        public ApplicationStatus Status { get; set; }
        public string? StatusReason { get; set; }
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

    /// <summary>Ergebnis einer Batch-Operation.</summary>
    public class BatchApplicationResult
    {
        public int TotalProcessed { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<ApplicationActionResult> Details { get; set; } = new();
    }

    /// <summary>Audit-Eintrag für Bewerbungs-Änderungen.</summary>
    public class ApplicationAuditEntry
    {
        public int Id { get; set; }
        public int ApplicationId { get; set; }
        public string Action { get; set; } = string.Empty; // Accept, Reject, Unflag, etc.
        public string Actor { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? Details { get; set; }
    }

    /// <summary>Status einer Bewerbung.</summary>
    public enum ApplicationStatus
    {
        Pending = 0,
        Accepted = 1,
        Rejected = 2,
        FlaggedForReview = 3,
        Expired = 4
    }

    /// <summary>Metriken für Bewerbungs-Analyse.</summary>
    public class ApplicationMetrics
    {
        public double AverageProcessingTimeHours { get; set; }
        public double AcceptanceRate { get; set; }
        public double RejectionRate { get; set; }
        public int ApplicationsThisWeek { get; set; }
        public int ApplicationsThisMonth { get; set; }
        public DateTime? AverageTimeToAcceptance { get; set; }
    }
}
