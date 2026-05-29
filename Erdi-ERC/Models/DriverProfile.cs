using System.ComponentModel.DataAnnotations;

namespace <OWNER_HANDLE>_ERC.Models
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
