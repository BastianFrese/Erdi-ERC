using System.ComponentModel.DataAnnotations;

namespace <OWNER_HANDLE>_ERC.Models
{
    public class AdminAuditLog
    {
        [Key]
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(128)]
        public string Actor { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string Action { get; set; } = string.Empty;

        [MaxLength(128)]
        public string EntityType { get; set; } = string.Empty;

        [MaxLength(128)]
        public string EntityId { get; set; } = string.Empty;

        [MaxLength(2048)]
        public string Details { get; set; } = string.Empty;
    }
}
