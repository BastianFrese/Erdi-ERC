using System.ComponentModel.DataAnnotations;

namespace <OWNER_HANDLE>_ERC.Models
{
    public class DiscordWebhook
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string Url { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Description { get; set; }

        /// <summary>Kategorie z.B. "Rennberichte", "Stewarding", "News"</summary>
        [MaxLength(64)]
        public string? Category { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUsedAt { get; set; }

        [MaxLength(100)]
        public string? CreatedBy { get; set; }
    }
}
