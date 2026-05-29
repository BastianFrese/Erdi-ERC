using System.ComponentModel.DataAnnotations;

namespace <OWNER_HANDLE>_ERC.Models
{
    public class CommunityNewsPost
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(160)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(96)]
        public string Category { get; set; } = "News";

        [MaxLength(480)]
        public string? Summary { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        [MaxLength(128)]
        public string AuthorName { get; set; } = string.Empty;

        public bool IsPinned { get; set; }
        public bool IsPublished { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    }

    public class ProfileWallMessage
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(32)]
        public string ProfileDiscordId { get; set; } = string.Empty;

        [Required]
        [MaxLength(32)]
        public string AuthorDiscordId { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string AuthorName { get; set; } = string.Empty;

        [Required]
        [MaxLength(600)]
        public string Message { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class RaceAvailabilityEntry
    {
        public int Id { get; set; }
        public int EventId { get; set; }

        [Required]
        [MaxLength(32)]
        public string DiscordId { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string DiscordName { get; set; } = string.Empty;

        [Required]
        [MaxLength(24)]
        public string Status { get; set; } = string.Empty;

        [MaxLength(240)]
        public string? Note { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class SetupComment
    {
        public int Id { get; set; }
        public int TrackSetupId { get; set; }

        [Required]
        [MaxLength(32)]
        public string AuthorDiscordId { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string AuthorName { get; set; } = string.Empty;

        [Required]
        [MaxLength(600)]
        public string Message { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class SetupLike
    {
        public int Id { get; set; }
        public int TrackSetupId { get; set; }

        [Required]
        [MaxLength(32)]
        public string DiscordId { get; set; } = string.Empty;

        [MaxLength(128)]
        public string DiscordName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CommunityNewsViewModel
    {
        public List<CommunityNewsPost> Posts { get; set; } = new();
    }
}