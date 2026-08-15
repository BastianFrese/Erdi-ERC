using System.ComponentModel.DataAnnotations;

namespace Erdi_ERC.Models
{
    public class CommunityVotePoll
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(160)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(96)]
        public string Category { get; set; } = string.Empty;

        [MaxLength(480)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
        public List<CommunityVoteOption> Options { get; set; } = new();
    }

    public class CommunityVoteOption
    {
        public int Id { get; set; }
        public int PollId { get; set; }

        [Required]
        [MaxLength(160)]
        public string Label { get; set; } = string.Empty;
    }

    public class CommunityVoteResponse
    {
        public int Id { get; set; }
        public int PollId { get; set; }
        public int OptionId { get; set; }

        [Required]
        [MaxLength(32)]
        public string DiscordId { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string DiscordName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class RaceHighlightClip
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(160)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(512)]
        public string Url { get; set; } = string.Empty;

        [MaxLength(96)]
        public string Category { get; set; } = "Highlight";

        [MaxLength(160)]
        public string? RaceLabel { get; set; }

        [MaxLength(32)]
        public string? SubmittedByDiscordId { get; set; }

        [MaxLength(128)]
        public string SubmittedByName { get; set; } = string.Empty;

        public bool IsApproved { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CommunityVotePollCardViewModel
    {
        public CommunityVotePoll Poll { get; set; } = new();
        public Dictionary<int, int> VoteCounts { get; set; } = new();
        public int TotalVotes { get; set; }
        public int? CurrentOptionId { get; set; }
    }

    public class CommunityVotesPageViewModel
    {
        public List<CommunityVotePollCardViewModel> Polls { get; set; } = new();
    }
    public class HighlightsPageViewModel
    {
        public List<RaceHighlightClip> Highlights { get; set; } = new();
    }

    public class DriverLevelEntryViewModel
    {
        public string Driver { get; set; } = string.Empty;
        public string? DiscordId { get; set; }
        public int Level { get; set; }
        public int Xp { get; set; }
        public int XpToNext { get; set; }
        public int Races { get; set; }
        public int Wins { get; set; }
        public int Podiums { get; set; }
        public int CommunityScore { get; set; }
    }

    public class DriverLevelsPageViewModel
    {
        public List<DriverLevelEntryViewModel> Entries { get; set; } = new();
    }
}