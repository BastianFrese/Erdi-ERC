using Erdi_ERC.Controllers;
using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.FileProviders;
using Xunit;
using IFormFile = Microsoft.AspNetCore.Http.IFormFile;

namespace Erdi_ERC.Tests.Controllers;

/// <summary>
/// Giveaway-CRUD im Admin (Community → Giveaways): Anlegen/Bearbeiten/Löschen der
/// Startseiten-Infotafel-Einträge. Sichtbarkeit ist zeitraum-basiert (StartAt/EndAt),
/// der Dienst selbst filtert in <see cref="HomeIndexDataService"/> — hier nur die
/// Persistenz + Validierung (Titel-Pflicht, EndAt &gt; StartAt, http(s)-Link).
/// </summary>
public class AdminCommunityGiveawayTests
{
    private static AdminCommunityController BuildController(SqliteTestContext ctx)
    {
        var httpCtx = TestAuthHelper.CreateAdminContext("admin1", "AdminUser");
        var ctrl = new AdminCommunityController(
            new NoopContentService(),
            new NoopMediaService(),
            ctx.Db,
            new StubWebHostEnvironment(),
            new NoopAudit(),
            new NoopWebhook(),
            new NoopStats(),
            new NoopStaticCache());
        TestAuthHelper.AttachContext(ctrl, httpCtx);
        ctrl.TempData = new TempDataDictionary(httpCtx, new NullTempDataProvider());
        return ctrl;
    }

    private static DateTime FixedNow() => new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SaveGiveaway_create_persistsAllFields()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = BuildController(ctx);
        var now = FixedNow();

        var result = await ctrl.SaveGiveaway(
            id: null,
            title: "Fanatec-Lenkrad verlosen",
            description: "Einfach im Chat teilnehmen.",
            prize: "Fanatec-Pedalset",
            startAt: now.AddDays(-1),
            endAt: now.AddDays(7),
            link: "https://discord.gg/erc");

        Assert.IsType<RedirectToActionResult>(result);

        var saved = ctx.NewContext().Giveaways.Single(g => g.Title == "Fanatec-Lenkrad verlosen");
        Assert.Equal("Fanatec-Pedalset", saved.Prize);
        Assert.Equal("Einfach im Chat teilnehmen.", saved.Description);
        Assert.Equal("https://discord.gg/erc", saved.Link);
        Assert.Equal(now.AddDays(-1), saved.StartAt);
        Assert.Equal(now.AddDays(7), saved.EndAt);
    }

    [Fact]
    public async Task SaveGiveaway_create_withoutLink_keepsLinkNull()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = BuildController(ctx);
        var now = FixedNow();

        var result = await ctrl.SaveGiveaway(
            id: null, title: "Ohne Link", description: null, prize: null,
            startAt: now, endAt: now.AddDays(3), link: null);

        Assert.IsType<RedirectToActionResult>(result);
        var saved = ctx.NewContext().Giveaways.Single(g => g.Title == "Ohne Link");
        Assert.Null(saved.Link);
    }

    [Fact]
    public async Task SaveGiveaway_create_withoutTitle_isRejected()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = BuildController(ctx);
        var now = FixedNow();

        var result = await ctrl.SaveGiveaway(
            id: null, title: "   ", description: null, prize: null,
            startAt: now, endAt: now.AddDays(3), link: null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Giveaways", redirect.ActionName);
        Assert.Equal("Titel ist erforderlich.", ctrl.TempData["AdminMessage"]);
        Assert.Equal(0, ctx.NewContext().Giveaways.Count());
    }

    [Fact]
    public async Task SaveGiveaway_create_endBeforeStart_isRejected()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = BuildController(ctx);
        var now = FixedNow();

        var result = await ctrl.SaveGiveaway(
            id: null, title: "Falscher Zeitraum",
            description: null, prize: null,
            startAt: now.AddDays(5), endAt: now.AddDays(1), link: null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Giveaways", redirect.ActionName);
        Assert.Contains("Enddatum", ctrl.TempData["AdminMessage"] as string);
        Assert.Equal(0, ctx.NewContext().Giveaways.Count());
    }

    [Fact]
    public async Task SaveGiveaway_rejectsInvalidLink()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = BuildController(ctx);
        var now = FixedNow();

        var result = await ctrl.SaveGiveaway(
            id: null, title: "Kaputter Link",
            description: null, prize: null,
            startAt: now, endAt: now.AddDays(3), link: "kein-link");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Giveaways", redirect.ActionName);
        Assert.Contains("Link", ctrl.TempData["AdminMessage"] as string);
        Assert.Equal(0, ctx.NewContext().Giveaways.Count());
    }

    [Fact]
    public async Task SaveGiveaway_edit_updatesExisting()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Giveaways.Add(new Giveaway
        {
            Title = "Alt-Titel",
            StartAt = FixedNow(),
            EndAt = FixedNow().AddDays(3),
        });
        ctx.Db.SaveChanges();

        var ctrl = BuildController(ctx);
        var existingId = ctx.NewContext().Giveaways.Single().Id;

        var result = await ctrl.SaveGiveaway(
            id: existingId,
            title: "Neuer Titel",
            description: "Geändert",
            prize: "Neu",
            startAt: FixedNow().AddDays(-2),
            endAt: FixedNow().AddDays(10),
            link: "https://example.com");

        Assert.IsType<RedirectToActionResult>(result);

        var edited = ctx.NewContext().Giveaways.Single(g => g.Id == existingId);
        Assert.Equal("Neuer Titel", edited.Title);
        Assert.Equal("Geändert", edited.Description);
        Assert.Equal("Neu", edited.Prize);
        Assert.Equal(1, ctx.NewContext().Giveaways.Count()); // kein neuer Eintrag
    }

    [Fact]
    public async Task DeleteGiveaway_removesEntity()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Giveaways.Add(new Giveaway
        {
            Title = "Zu löschen",
            StartAt = FixedNow(),
            EndAt = FixedNow().AddDays(2),
        });
        ctx.Db.SaveChanges();
        var id = ctx.NewContext().Giveaways.Single().Id;

        var ctrl = BuildController(ctx);
        var result = await ctrl.DeleteGiveaway(id);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Giveaways", redirect.ActionName);
        Assert.Equal("Giveaway gelöscht.", ctrl.TempData["AdminMessage"]);
        Assert.Equal(0, ctx.NewContext().Giveaways.Count());
    }

    // ── Stubs (identisch zu AdminCommunityStewardingPenaltyTests) ──────────────────

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }

    private sealed class NoopAudit : IAdminAuditService
    {
        public Task LogAsync(string action, string entityType, string entityId, string details) => Task.CompletedTask;
        public Task LogAndSaveAsync(string action, string entityType, string entityId, string details) => Task.CompletedTask;
    }

    private sealed class NoopWebhook : IWebhookAutomationService
    {
        public Task FireAsync(string eventType, Dictionary<string, string> vars) => Task.CompletedTask;
        public Task<(bool Success, string? Error)> TestRuleAsync(int ruleId, Dictionary<string, string> vars, CancellationToken ct = default)
            => Task.FromResult((true, (string?)null));
    }

    private sealed class NoopStats : IStatsService
    {
        public Task RebuildLeagueStandingsAsync(string leagueId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RebuildAllLeagueStandingsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoopStaticCache : IStaticDataCache
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

    private sealed class NoopContentService : ICommunityContentService
    {
        public Task<List<CommunityNewsPost>> GetRecentNewsAsync(int count = 6) => Task.FromResult(new List<CommunityNewsPost>());
        public Task<List<CommunityVotePoll>> GetRecentVotesAsync(int count = 6) => Task.FromResult(new List<CommunityVotePoll>());
        public Task<List<RaceHighlightClip>> GetRecentHighlightsAsync(int count = 6) => Task.FromResult(new List<RaceHighlightClip>());
        public Task<List<LeaguePenalty>> GetPublicPenaltiesAsync(int count = 50) => Task.FromResult(new List<LeaguePenalty>());
        public Task<CommunityNewsPost?> SaveNewsPostAsync(string title, string? category, string? summary, string content, string? authorName, bool isPinned = false, bool isPublished = true) => Task.FromResult<CommunityNewsPost?>(null);
        public Task<CommunityVotePoll?> SaveVotePollAsync(string title, string? category, string? description, string option1, string option2, string? option3 = null, string? option4 = null) => Task.FromResult<CommunityVotePoll?>(null);
        public Task<RaceHighlightClip?> SaveHighlightClipAsync(string title, string url, string? category, string? raceLabel, string? submittedById, string? submittedByName) => Task.FromResult<RaceHighlightClip?>(null);
    }

    private sealed class NoopMediaService : IMediaService
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

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Erdi-ERC.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Testing";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
    }
}
