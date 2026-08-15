using System.ComponentModel.DataAnnotations;

namespace Erdi_ERC.Models
{
    /// <summary>Über-mich-Profil des Admins / der Person die sich vorstellt.</summary>
    public class AboutMeProfile
    {
        public int Id { get; set; }

        /// <summary>Eindeutiger Slug, z.B. "erdi" für /About/erdi</summary>
        [Required, MaxLength(80)]
        public string Slug { get; set; } = string.Empty;

        [Required, MaxLength(120)]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Kurze Tagline unter dem Namen</summary>
        [MaxLength(200)]
        public string? Tagline { get; set; }

        /// <summary>URL zum Profilbild (oder relativer Upload-Pfad)</summary>
        public string? AvatarUrl { get; set; }

        /// <summary>URL zum Banner/Hintergrundbild (oder relativer Upload-Pfad)</summary>
        public string? BannerUrl { get; set; }

        /// <summary>Banner auf der About-Seite anzeigen?</summary>
        public bool ShowBanner { get; set; } = true;

        /// <summary>URL zum Seitenhintergrund-Bild (hinter dem gesamten Inhalt)</summary>
        public string? BackgroundUrl { get; set; }

        /// <summary>Markdown-formatierter Haupttext</summary>
        public string? Bio { get; set; }

        /// <summary>Kurzer Text für die Kartenvorschau</summary>
        [MaxLength(300)]
        public string? ShortBio { get; set; }

        // Social Links
        [MaxLength(200)] public string? DiscordUsername { get; set; }
        [MaxLength(200)] public string? TwitchUrl       { get; set; }
        [MaxLength(200)] public string? YouTubeUrl      { get; set; }
        [MaxLength(200)] public string? InstagramUrl    { get; set; }
        [MaxLength(200)] public string? TwitterUrl      { get; set; }
        [MaxLength(200)] public string? SteamUrl        { get; set; }

        /// <summary>Lieblingsstrecke</summary>
        [MaxLength(120)]
        public string? FavoriteTrack { get; set; }

        /// <summary>Lieblingsauto / Setup-Stil</summary>
        [MaxLength(120)]
        public string? FavoriteCar { get; set; }

        /// <summary>Rennfahrer-Nummer</summary>
        [MaxLength(10)]
        public string? RacingNumber { get; set; }

        /// <summary>Akzentfarbe (HEX) für die Profilseite</summary>
        [MaxLength(7)]
        public string? AccentColor { get; set; } = "#e10600";

        /// <summary>Profil öffentlich sichtbar?</summary>
        public bool IsPublic { get; set; } = true;

        /// <summary>Sortierungsreihenfolge im Team-Listing</summary>
        public int SortOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string? LastEditedBy { get; set; }
    }
}
