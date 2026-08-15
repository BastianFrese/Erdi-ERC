using System.ComponentModel.DataAnnotations;

namespace Erdi_ERC.Models
{
    /// <summary>
    /// Protokolliert jeden Rollenwechsel eines Fahrers innerhalb einer Saison/Liga.
    /// Ermöglicht vollständige Rollen-Historie (z.B. Aufstieg Ersatz → Stamm).
    /// </summary>
    public class DriverRoleHistory
    {
        public int Id { get; set; }

        [Required, MaxLength(64)]
        public string LeagueId { get; set; } = string.Empty;

        [Required, MaxLength(128)]
        public string Driver { get; set; } = string.Empty;

        /// <summary>Vorherige Rolle (z.B. "Ersatzfahrer")</summary>
        [MaxLength(64)]
        public string? PreviousRole { get; set; }

        /// <summary>Neue Rolle (z.B. "Stammfahrer")</summary>
        [Required, MaxLength(64)]
        public string NewRole { get; set; } = string.Empty;

        /// <summary>Optionale Begründung</summary>
        [MaxLength(500)]
        public string? Reason { get; set; }

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(128)]
        public string? ChangedBy { get; set; }
    }
}
