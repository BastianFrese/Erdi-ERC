using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using Microsoft.EntityFrameworkCore;

namespace <OWNER_HANDLE>_ERC.Services
{
    public class CommunityContentService : ICommunityContentService
    {
        private readonly AppDbContext _db;
        private readonly IAdminAuditService _audit;

        public CommunityContentService(AppDbContext db, IAdminAuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        public async Task<List<CommunityNewsPost>> GetRecentNewsAsync(int count = 6)
        {
            return await _db.CommunityNewsPosts
                .OrderByDescending(x => x.PublishedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<CommunityVotePoll>> GetRecentVotesAsync(int count = 6)
        {
            return await _db.CommunityVotePolls
                .Include(x => x.Options)
                .OrderByDescending(x => x.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<RaceHighlightClip>> GetRecentHighlightsAsync(int count = 6)
        {
            return await _db.RaceHighlightClips
                .OrderByDescending(x => x.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<LeaguePenalty>> GetPublicPenaltiesAsync(int count = 50)
        {
            return await _db.LeaguePenalties
                .Where(x => x.IsPublic)
                .OrderByDescending(x => x.Date)
                .Take(count)
                .ToListAsync();
        }

        public async Task<CommunityNewsPost?> SaveNewsPostAsync(string title, string? category, string? summary, string content, string? authorName, bool isPinned = false, bool isPublished = true)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
                return null;

            var entity = new CommunityNewsPost
            {
                Title = title.Trim(),
                Category = string.IsNullOrWhiteSpace(category) ? "News" : category.Trim(),
                Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim(),
                Content = content.Trim(),
                AuthorName = string.IsNullOrWhiteSpace(authorName) ? "Admin" : authorName.Trim(),
                IsPinned = isPinned,
                IsPublished = isPublished,
                CreatedAt = DateTime.UtcNow,
                PublishedAt = DateTime.UtcNow
            };

            _db.CommunityNewsPosts.Add(entity);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("SaveCommunityNewsPost", "CommunityNewsPost", entity.Id.ToString(), $"Title={title}");

            // Discord-Notification erfolgt über WebhookAutomation in AdminCommunityController
            // (Event: NewsPostPublished). Hier KEINE doppelten Sends mehr.
            return entity;
        }

        public async Task<CommunityVotePoll?> SaveVotePollAsync(string title, string? category, string? description, string option1, string option2, string? option3 = null, string? option4 = null)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(option1) || string.IsNullOrWhiteSpace(option2))
                return null;

            var poll = new CommunityVotePoll
            {
                Title = title.Trim(),
                Category = string.IsNullOrWhiteSpace(category) ? "Voting" : category.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Options = new List<CommunityVoteOption>
                {
                    new() { Label = option1.Trim() },
                    new() { Label = option2.Trim() }
                }
            };

            if (!string.IsNullOrWhiteSpace(option3)) poll.Options.Add(new CommunityVoteOption { Label = option3.Trim() });
            if (!string.IsNullOrWhiteSpace(option4)) poll.Options.Add(new CommunityVoteOption { Label = option4.Trim() });

            _db.CommunityVotePolls.Add(poll);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("SaveCommunityVotePoll", "CommunityVotePoll", poll.Id.ToString(), $"Title={poll.Title}");

            // Discord-Notification über WebhookAutomation in AdminCommunityController (VotePollPublished).
            return poll;
        }

        public async Task<RaceHighlightClip?> SaveHighlightClipAsync(string title, string url, string? category, string? raceLabel, string? submittedById, string? submittedByName)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
                return null;

            var clip = new RaceHighlightClip
            {
                Title = title.Trim(),
                Url = url.Trim(),
                Category = string.IsNullOrWhiteSpace(category) ? "Highlight" : category.Trim(),
                RaceLabel = string.IsNullOrWhiteSpace(raceLabel) ? null : raceLabel.Trim(),
                SubmittedByDiscordId = submittedById,
                SubmittedByName = submittedByName ?? "Admin",
                IsApproved = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.RaceHighlightClips.Add(clip);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("SaveHighlightClip", "RaceHighlightClip", clip.Id.ToString(), $"Title={title}");

            // Discord-Notification über WebhookAutomation in AdminCommunityController (HighlightApproved).
            return clip;
        }
    }
}
