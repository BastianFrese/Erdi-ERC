using System.ComponentModel.DataAnnotations;

namespace <OWNER_HANDLE>_ERC.Models
{
    public class ApplicationForm
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Range(13, 99, ErrorMessage = "Bitte ein gültiges Alter zwischen 13 und 99 angeben.")]
        public int Age { get; set; }

        [Required]
        [MaxLength(128)]
        public string DiscordName { get; set; } = string.Empty;

        /// <summary>Discord-Snowflake-Id des Bewerbers (für die Profil-Verknüpfung beim Annehmen).</summary>
        [MaxLength(32)]
        public string? DiscordId { get; set; }

        [Required]
        [MaxLength(64)]
        public string Role { get; set; } = string.Empty; // Stammfahrer / Ersatzfahrer

        public bool JoinedCommunityDiscord { get; set; }
        public bool JoinedLeagueDiscord { get; set; }

        [Required]
        [MaxLength(128)]
        public string GamingName { get; set; } = string.Empty;

        [Required]
        [MaxLength(64)]
        public string Platform { get; set; } = string.Empty; // Steam / EA / Xbox / Playstation

        [Required]
        [MaxLength(64)]
        public string AiLevel { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string Division { get; set; } = string.Empty;

        public bool IsAccepted { get; set; }
        public DateTime? AcceptedAt { get; set; }

        /// <summary>
        /// Review-Status des Bewerbungs-Workflows.
        /// Open → InReview → Accepted / Rejected
        /// </summary>
        public ApplicationReviewStatus ReviewStatus { get; set; } = ApplicationReviewStatus.Open;

        /// <summary>Zugewiesene Fahrer-Rolle nach Annahme (Stammfahrer, Ersatzfahrer, Academy).</summary>
        [MaxLength(64)]
        public string? AssignedRole { get; set; }

        /// <summary>Kommentar/Notiz der Admins zum Review.</summary>
        [MaxLength(1000)]
        public string? ReviewNote { get; set; }

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>Status einer Bewerbung im Review-Workflow.</summary>
    public enum ApplicationReviewStatus
    {
        Open = 0,
        InReview = 1,
        Accepted = 2,
        Rejected = 3
    }
}
