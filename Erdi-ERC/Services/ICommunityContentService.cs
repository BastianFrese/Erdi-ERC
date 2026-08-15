using Erdi_ERC.Models;

namespace Erdi_ERC.Services
{
    public interface ICommunityContentService
    {
        Task<List<CommunityNewsPost>> GetRecentNewsAsync(int count = 6);
        Task<List<CommunityVotePoll>> GetRecentVotesAsync(int count = 6);
        Task<List<RaceHighlightClip>> GetRecentHighlightsAsync(int count = 6);
        Task<List<LeaguePenalty>> GetPublicPenaltiesAsync(int count = 50);

        Task<CommunityNewsPost?> SaveNewsPostAsync(string title, string? category, string? summary, string content, string? authorName, bool isPinned = false, bool isPublished = true);
        Task<CommunityVotePoll?> SaveVotePollAsync(string title, string? category, string? description, string option1, string option2, string? option3 = null, string? option4 = null);
        Task<RaceHighlightClip?> SaveHighlightClipAsync(string title, string url, string? category, string? raceLabel, string? submittedById, string? submittedByName);
    }
}
