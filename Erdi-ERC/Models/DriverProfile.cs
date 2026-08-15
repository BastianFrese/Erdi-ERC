using System.ComponentModel.DataAnnotations;

namespace Erdi_ERC.Models
{
    /// <summary>
    /// Pro Discord-User ein Profil. Hält die verifizierten Gamer-Tags je Plattform und
    /// dient als zentrale Quelle für Auto-Vervollständigung beim Eintragen von Fahrern
    /// sowie für die öffentliche Profilseite (Achievements, Renn-Historie).
    /// </summary>
    public class DriverProfile
    {
        [Key]
        [MaxLength(32)]
        public string DiscordId { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string DiscordName { get; set; } = string.Empty;

        /// <summary>Vom Admin frei wählbar; Standard = primärer GamerTag.</summary>
        [MaxLength(128)]
        public string? DisplayName { get; set; }

        [MaxLength(128)]
        public string? FavoriteTrack { get; set; }

        [MaxLength(64)]
        public string? FavoriteTeam { get; set; }

        [MaxLength(64)]
        public string? InputDevice { get; set; }

        [MaxLength(64)]
        public string? PreferredPlatform { get; set; }

        [MaxLength(64)]
        public string? Nationality { get; set; }

        [MaxLength(512)]
        public string? Bio { get; set; }

        /// <summary>Optionales öffentliches Profilbild / Foto-URL.</summary>
        [MaxLength(512)]
        public string? PhotoUrl { get; set; }

        /// <summary>Optionales Alter (für Anzeige auf Profil-Karte).</summary>
        public int? Age { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<DriverGamerTag> GamerTags { get; set; } = new();

        // Akzeptanz der Widerrufsbelehrung / ausdrückliche Zustimmung, bevor exklusive
        // Subscriber-Setups angezeigt werden.
        public bool HasAcceptedExclusiveSetupTerms { get; set; } = false;
        public DateTime? ExclusiveSetupTermsAcceptedAt { get; set; }

        /// <summary>
        /// Manuell vom Admin vergebener Setup-Zugangs-Tier (1–5). Überschreibt
        /// den via Discord-Rollen aufgelösten Tier, wenn höher. Null = kein manueller Grant.
        /// </summary>
        public int? ManualSetupTier { get; set; }

        /// <summary>
        /// Vom Fahrer gewählte Farbe der Fahrernummer auf der Fahrer-Karte.
        /// Format #RRGGBB; null/empty → Team-Primary oder Default #e10600.
        /// </summary>
        [MaxLength(7)]
        public string? DriverNumberColor { get; set; }
    }

    /// <summary>Plattform-spezifischer Gamer-Tag eines Driver-Profils.</summary>
    public class DriverGamerTag
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(32)]
        public string DiscordId { get; set; } = string.Empty;

        [Required]
        [MaxLength(64)]
        public string Platform { get; set; } = string.Empty; // Steam / EA / Xbox / Playstation

        [Required]
        [MaxLength(128)]
        public string GamerTag { get; set; } = string.Empty;

        public bool IsPrimary { get; set; }

        public DateTime LinkedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Discord-Id des Admins, der den Tag verknüpft hat (optional, für Audit).</summary>
        [MaxLength(32)]
        public string? LinkedByDiscordId { get; set; }
    }
}
