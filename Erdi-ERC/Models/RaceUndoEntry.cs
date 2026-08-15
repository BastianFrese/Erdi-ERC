using System.ComponentModel.DataAnnotations;

namespace Erdi_ERC.Models
{
    public class RaceUndoEntry
    {
        [Key]
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(128)]
        public string Actor { get; set; } = string.Empty;

        [Required]
        [MaxLength(64)]
        public string LeagueId { get; set; } = string.Empty;

        public int OriginalRaceId { get; set; }

        [Required]
        public string PayloadJson { get; set; } = string.Empty;

        public bool IsUsed { get; set; }
        public DateTime? UsedAt { get; set; }
    }
}
