using <OWNER_HANDLE>_ERC.Models;

namespace <OWNER_HANDLE>_ERC.Services
{
    /// <summary>
    /// Read-only-Seite des Bewerbungssystems: Listen, Statistik, Metriken, Verlauf,
    /// Kapazitäten und CSV-Export. Alle Abfragen laufen ohne Change-Tracking.
    /// Schreibende Übergänge liegen in <see cref="IApplicationWorkflowService"/>.
    /// </summary>
    public interface IApplicationQueryService
    {
        /// <summary>Gefilterte, sortierte, paginierte Bewerbungsliste inkl. Gesamtzahl.</summary>
        Task<ApplicationSearchResult> SearchAsync(ApplicationSearchFilter filter);

        /// <summary>Einzelne Bewerbung (ohne Tracking) — für Anzeige, nicht zum Ändern.</summary>
        Task<ApplicationForm?> GetByIdAsync(int id);

        /// <summary>Bewerbung inkl. Audit-Verlauf.</summary>
        Task<ApplicationFormWithHistory?> GetWithHistoryAsync(int id);

        /// <summary>Status-Zähler und Verteilungen über alle Bewerbungen.</summary>
        Task<ApplicationStatistics> GetStatisticsAsync();

        /// <summary>Kennzahlen (Bearbeitungszeit, Annahmequote, Eingänge pro Woche/Monat).</summary>
        Task<ApplicationMetrics> GetMetricsAsync();

        /// <summary>Kapazitäts-/Wartelisten-Übersicht je Liga, die Bewerbungen annimmt.</summary>
        Task<List<LeagueCapacityRow>> GetLeagueCapacitiesAsync();

        /// <summary>Exportiert Bewerbungen als CSV, optional auf eine Liga gefiltert.</summary>
        Task<string> ExportCsvAsync(string? leagueId = null);
    }

    /// <summary>Filter für die Bewerbungsliste. Nicht gesetzte Felder filtern nicht.</summary>
    public record ApplicationSearchFilter
    {
        public ApplicationStatus? Status { get; init; }

        /// <summary>Nur zur Überprüfung markierte Bewerbungen.</summary>
        public bool FlaggedOnly { get; init; }

        /// <summary>Nur Bewerber mit mehr als einer Bewerbung (gleiche DiscordId).</summary>
        public bool DuplicatesOnly { get; init; }

        /// <summary>Nur Bewerbungen ohne auflösbare Liga (weder beworben noch zugewiesen).</summary>
        public bool WithoutLeagueOnly { get; init; }

        /// <summary>Liga-Filter: zugewiesene Liga, sonst beworbene Liga.</summary>
        public string? LeagueId { get; init; }

        /// <summary>Suche in Discord- und Gaming-Name.</summary>
        public string? Search { get; init; }

        /// <summary>"newest" | "oldest" | null = Triage (Liga-Reihenfolge → Rolle → neueste).</summary>
        public string? Sort { get; init; }

        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 50;
    }

    /// <summary>Seite einer Bewerbungsliste inkl. Gesamtzahl für die Pagination.</summary>
    public class ApplicationSearchResult
    {
        public List<ApplicationForm> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 1;
    }

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

    /// <summary>Audit-Eintrag für Bewerbungs-Änderungen.</summary>
    public class ApplicationAuditEntry
    {
        public int Id { get; set; }
        public int ApplicationId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Actor { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? Details { get; set; }
    }

    /// <summary>Metriken für Bewerbungs-Analyse.</summary>
    public class ApplicationMetrics
    {
        public double AverageProcessingTimeHours { get; set; }
        public double AcceptanceRate { get; set; }
        public double RejectionRate { get; set; }
        public int ApplicationsThisWeek { get; set; }
        public int ApplicationsThisMonth { get; set; }
    }

    /// <summary>Eine Zeile der Kapazitäts-Übersicht: belegte Stammplätze vs. Soll + Warteliste.</summary>
    public class LeagueCapacityRow
    {
        public string LeagueId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        /// <summary>Kurzinfo fürs Bewerbungsformular (z.B. "Freitags 20:00 · KI bis 105").</summary>
        public string? ApplicationInfo { get; set; }

        public int? Capacity { get; set; }
        public int Filled { get; set; }
        public int Pending { get; set; }

        public bool IsFull => Capacity.HasValue && Filled >= Capacity.Value;
        public int FreeSlots => Capacity.HasValue ? Math.Max(0, Capacity.Value - Filled) : int.MaxValue;
        public int FillPercent => Capacity.HasValue && Capacity.Value > 0
            ? (int)Math.Min(100, Math.Round(100.0 * Filled / Capacity.Value))
            : 0;
    }
}
