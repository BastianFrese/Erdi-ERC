using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        /// <summary>Sim-Hardware (Lenkrad/Pad-Marke), wird beim Annehmen ins Profil (InputDevice) übernommen.</summary>
        [MaxLength(64)]
        public string? SimHardware { get; set; }

        /// <summary>Wunsch-Fahrernummer; beim Annehmen für den Standings-Eintrag verwendet, falls frei.</summary>
        [Range(0, 999, ErrorMessage = "Fahrernummer muss zwischen 0 und 999 liegen.")]
        public int? PreferredNumber { get; set; }

        /// <summary>Wunsch-Team; beim Annehmen ins Profil (FavoriteTeam) und in den Standings-Eintrag übernommen.</summary>
        [MaxLength(64)]
        public string? PreferredTeam { get; set; }

        /// <summary>Pace-Referenz / Erfahrung (z.B. Bestzeit auf einer Referenzstrecke, Vorerfahrung).</summary>
        [MaxLength(256)]
        public string? PaceReference { get; set; }

        [Required]
        [MaxLength(64)]
        public string Platform { get; set; } = string.Empty; // Steam / EA / Xbox / Playstation

        [Required]
        [MaxLength(64)]
        public string AiLevel { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string Division { get; set; } = string.Empty;

        /// <summary>Liga, für die sich der Bewerber beworben hat (bleibt als Bewerbungs-Historie unverändert).</summary>
        [MaxLength(64)]
        public string? AppliedLeagueId { get; set; }

        /// <summary>Liga, in die der Fahrer bei der Annahme tatsächlich eingetragen wurde.
        /// Kann von <see cref="AppliedLeagueId"/> abweichen, wenn der Admin eine andere Liga wählt.
        /// Null = (noch) nicht zugewiesen.</summary>
        [MaxLength(64)]
        public string? AssignedLeagueId { get; set; }

        /// <summary>Bewerbungs-Status (offen/akzeptiert/abgelehnt) — genau EIN Zustand.</summary>
        public ApplicationStatus Status { get; set; } = ApplicationStatus.Open;

        public DateTime? AcceptedAt { get; set; }

        /// <summary>Angenommener Fahrer auf Probe (Probezeit). Wird nach Bestätigung aufgehoben.</summary>
        public bool IsOnTrial { get; set; }

        /// <summary>Ende der Probezeit (informativ; null = unbefristet bzw. nicht auf Probe).</summary>
        public DateTime? TrialEndsAt { get; set; }

        public DateTime? RejectedAt { get; set; }

        /// <summary>Komfort-Ableitung für Views/In-Memory-Logik. In EF-Queries <see cref="Status"/> verwenden!</summary>
        [NotMapped]
        public bool IsAccepted => Status == ApplicationStatus.Accepted;

        /// <summary>Komfort-Ableitung für Views/In-Memory-Logik. In EF-Queries <see cref="Status"/> verwenden!</summary>
        [NotMapped]
        public bool IsRejected => Status == ApplicationStatus.Rejected;

        /// <summary>Vom Admin zur Überprüfung markiert (Grund steht in <see cref="ReviewNote"/>).</summary>
        public bool IsFlagged { get; set; }
        public DateTime? FlaggedAt { get; set; }

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
}
