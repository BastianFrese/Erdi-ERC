using System.ComponentModel.DataAnnotations;

namespace Erdi_ERC.Models
{
    /// <summary>
    /// Ein versioniertes Regelwerk-Dokument.
    /// Admins können PDFs oder Markdown-Dateien hochladen.
    /// Ältere Versionen werden archiviert (IsActive = false).
    /// </summary>
    public class RegelwerkDocument
    {
        public int Id { get; set; }

        /// <summary>Anzeigename, z.B. "Regelwerk Saison 3 v5.1"</summary>
        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        /// <summary>Versionsnummer als Text, z.B. "5.1"</summary>
        [MaxLength(20)]
        public string Version { get; set; } = "1.0";

        /// <summary>Kurze Beschreibung / Änderungshistorie für diese Version</summary>
        [MaxLength(1000)]
        public string? Description { get; set; }

        /// <summary>Relativer Pfad zur hochgeladenen Datei (PDF oder Markdown).</summary>
        [Required, MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Originalname der hochgeladenen Datei.</summary>
        [MaxLength(260)]
        public string OriginalFileName { get; set; } = string.Empty;

        /// <summary>MIME-Type der Datei, z.B. application/pdf oder text/markdown</summary>
        [MaxLength(100)]
        public string ContentType { get; set; } = "application/pdf";

        /// <summary>Ist dieses Dokument das aktuell aktive Regelwerk?</summary>
        public bool IsActive { get; set; } = false;

        /// <summary>Archiviert = ältere Version, nicht mehr aktiv, aber einsehbar.</summary>
        public bool IsArchived { get; set; } = false;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(128)]
        public string? UploadedBy { get; set; }
    }
}
