using Erdi_ERC.Controllers;
using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.FileProviders;
using IFormFile = Microsoft.AspNetCore.Http.IFormFile;

namespace Erdi_ERC.Tests.Infrastructure;

/// <summary>
/// Baut <see cref="AdminCommunityController"/> mit No-op-Diensten für Controller-Tests
/// und hält die Stubs an einer Stelle (vorher dreifach in den Testklassen kopiert).
///
/// <paramref name="audit"/> kann durch einen echten <see cref="AdminAuditService"/>
/// ersetzt werden — nötig, wenn ein Test prüfen soll, dass ein Audit-Eintrag
/// tatsächlich geschrieben wird (LogAsync vs. LogAndSaveAsync).
/// </summary>
public static class AdminCommunityTestHarness
{
    public static AdminCommunityController BuildController(
        SqliteTestContext ctx,
        HttpContext? httpCtx = null,
        IAdminAuditService? audit = null)
    {
        httpCtx ??= TestAuthHelper.CreateAdminContext("admin1", "AdminUser");
        var ctrl = new AdminCommunityController(
            new NoopContentService(),
            new NoopMediaService(),
            ctx.Db,
            new StubWebHostEnvironment(),
            audit ?? new NoopAudit(),
            new NoopWebhook(),
            new NoopStats(),
            new NoopStaticCache());
        TestAuthHelper.AttachContext(ctrl, httpCtx);
        ctrl.TempData = new TempDataDictionary(httpCtx, new NullTempDataProvider());
        return ctrl;
    }

    public sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }

    public sealed class NoopAudit : IAdminAuditService
    {
        public Task LogAsync(string action, string entityType, string entityId, string details) => Task.CompletedTask;
        public Task LogAndSaveAsync(string action, string entityType, string entityId, string details) => Task.CompletedTask;
    }

    public sealed class NoopWebhook : IWebhookAutomationService
    {
        public Task FireAsync(string eventType, Dictionary<string, string> vars) => Task.CompletedTask;
        public Task<(bool Success, string? Error)> TestRuleAsync(int ruleId, Dictionary<string, string> vars, CancellationToken ct = default)
            => Task.FromResult((true, (string?)null));
    }

    public sealed class NoopStats : IStatsService
    {
        public Task RebuildLeagueStandingsAsync(string leagueId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RebuildAllLeagueStandingsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public sealed class NoopStaticCache : IStaticDataCache
    {
        public Task<IReadOnlyList<AchievementDefinition>> GetActiveAchievementDefinitionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AchievementDefinition>>(Array.Empty<AchievementDefinition>());
        public Task<IReadOnlyList<League>> GetAllLeaguesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<League>>(Array.Empty<League>());
        public Task<IReadOnlyList<League>> GetApplicationLeaguesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<League>>(Array.Empty<League>());
        public void InvalidateAchievementDefinitions() { }
        public void InvalidateLeagues() { }
    }

    public sealed class NoopContentService : ICommunityContentService
    {
        public Task<List<CommunityNewsPost>> GetRecentNewsAsync(int count = 6) => Task.FromResult(new List<CommunityNewsPost>());
        public Task<List<CommunityVotePoll>> GetRecentVotesAsync(int count = 6) => Task.FromResult(new List<CommunityVotePoll>());
        public Task<List<RaceHighlightClip>> GetRecentHighlightsAsync(int count = 6) => Task.FromResult(new List<RaceHighlightClip>());
        public Task<List<LeaguePenalty>> GetPublicPenaltiesAsync(int count = 50) => Task.FromResult(new List<LeaguePenalty>());
        public Task<CommunityNewsPost?> SaveNewsPostAsync(string title, string? category, string? summary, string content, string? authorName, bool isPinned = false, bool isPublished = true) => Task.FromResult<CommunityNewsPost?>(null);
        public Task<CommunityVotePoll?> SaveVotePollAsync(string title, string? category, string? description, string option1, string option2, string? option3 = null, string? option4 = null) => Task.FromResult<CommunityVotePoll?>(null);
        public Task<RaceHighlightClip?> SaveHighlightClipAsync(string title, string url, string? category, string? raceLabel, string? submittedById, string? submittedByName) => Task.FromResult<RaceHighlightClip?>(null);
    }

    public sealed class NoopMediaService : IMediaService
    {
        public Task<List<BackgroundMusicFileViewModel>> GetBackgroundMusicFilesAsync() => Task.FromResult(new List<BackgroundMusicFileViewModel>());
        public Task<bool> UploadBackgroundMusicAsync(IFormFile? musicFile) => Task.FromResult(false);
        public Task<bool> DeleteBackgroundMusicAsync(string fileName) => Task.FromResult(false);
        public Task<string?> SaveAboutImageAsync(IFormFile image, string slot) => Task.FromResult<string?>(null);
        public void TryDeleteAboutImage(string fileName) { }
        public Task<string?> SaveDriverPhotoAsync(IFormFile image, string discordId) => Task.FromResult<string?>(null);
        public void TryDeleteDriverPhoto(string url) { }
        public Task<string?> SaveEventImageAsync(IFormFile image) => Task.FromResult<string?>(null);
        public void TryDeleteEventImage(string fileName) { }
        public Task<string?> SaveCalendarBackgroundAsync(IFormFile image) => Task.FromResult<string?>(null);
        public void TryDeleteCalendarBackground(string fileName) { }
        public Task<bool> UploadEwigeListeAsync(IFormFile? workbook) => Task.FromResult(false);
    }

    public sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Erdi-ERC.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Testing";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
    }
}
