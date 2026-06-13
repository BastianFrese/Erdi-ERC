using System;

namespace <OWNER_HANDLE>_ERC.Models
{
    public class CustomAchievement
    {
        public int Id { get; set; }
        public string? LeagueId { get; set; }
        public string Driver { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Tone { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public DateTime AwardedAt { get; set; }
    }
}
