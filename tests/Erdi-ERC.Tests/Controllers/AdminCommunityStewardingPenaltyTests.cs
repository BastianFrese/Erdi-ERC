using Erdi_ERC.Controllers;
using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Xunit;
using IFormFile = Microsoft.AspNetCore.Http.IFormFile;

namespace Erdi_ERC.Tests.Controllers;

/// <summary>
/// Stewarding-Snapshot: Beim ANLEGEN eines Steward-Dokuments wird der Strafpunkte-
/// Gesamtstand des Fahrers (in der Liga, inkl. der neuen Punkte) fest auf
/// LeaguePenalty.DriverPointsTotal gespeichert. Alte Berichte duerfen den Wert
/// NICHT aktualisieren — weder durch Bearbeitung des Alt-Dokuments noch durch
/// spaeter angelegte Berichte desselben Fahrers (User-Anforderung 2026-09-14).
/// </summary>
public class AdminCommunityStewardingPenaltyTests
{
    private static void SeedLeagueAndDriver(SqliteTestContext ctx, string leagueId, string driver, int number = 1)
    {
        if (!ctx.Db.Leagues.Any(l => l.Id == leagueId))
            ctx.Db.Leagues.Add(new League { Id = leagueId, Name = leagueId });
        ctx.Db.DriverStandings.Add(new DriverStanding
        {
            LeagueId = leagueId,
            Driver = driver,
            DriverNumber = number,
            Position = 1,
            Team = "T"
        });
        ctx.Db.SaveChanges();
    }

    private static void SeedPenalty(SqliteTestContext ctx, string leagueId, string driver, int points, string reason)
    {
        ctx.Db.LeaguePenalties.Add(new LeaguePenalty
        {
            LeagueId = leagueId,
            Driver = driver,
            PenaltyType = "Zeitstrafe + Strafpunkte",
            Points = points,
            Reason = reason,
            Date = new DateTime(2026, 8, 1),
            IsPublic = true,
        });
        ctx.Db.SaveChanges();
    }

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

    [Fact]
    public async Task SavePenalty_create_setsDriverPointsTotalIncludingOwnPoints()
    {
        using var ctx = new SqliteTestContext();
        SeedLeagueAndDriver(ctx, "l1", "Alpha");
        SeedPenalty(ctx, "l1", "Alpha", 5, "vorher");

        var ctrl = BuildController(ctx);
        var result = await ctrl.SavePenalty(
            id: null,
            leagueId: "l1",
            date: new DateTime(2026, 8, 2),
            driver: "Alpha",
            penaltyType: "Zeitstrafe + Strafpunkte",
            points: 3,
            raceTrack: null,
            secondDriver: null,
            isBetweenTwoDrivers: false,
            incident: null,
            reason: "neue Strafe",
            isPublic: true);

        Assert.IsType<RedirectToActionResult>(result);

        var doc = ctx.NewContext().LeaguePenalties.Single(p => p.Reason == "neue Strafe");
        Assert.Equal(8, doc.DriverPointsTotal); // 5 Bestand + 3 neu
    }

    [Fact]
    public async Task SavePenalty_create_scopeIsPerLeague()
    {
        using var ctx = new SqliteTestContext();
        SeedLeagueAndDriver(ctx, "l1", "Alpha");
        SeedLeagueAndDriver(ctx, "l2", "Alpha");
        SeedPenalty(ctx, "l1", "Alpha", 5, "in l1");

        var ctrl = BuildController(ctx);
        var result = await ctrl.SavePenalty(
            id: null, leagueId: "l2", date: new DateTime(2026, 8, 2),
            driver: "Alpha", penaltyType: "Zeitstrafe + Strafpunkte", points: 3,
            raceTrack: null, secondDriver: null, isBetweenTwoDrivers: false,
            incident: null, reason: "in l2", isPublic: true);

        Assert.IsType<RedirectToActionResult>(result);

        // Andere Liga ⇒ eigener Strafpunkte-Stand, NICHT die 5 aus l1.
        var doc = ctx.NewContext().LeaguePenalties.Single(p => p.Reason == "in l2");
        Assert.Equal(3, doc.DriverPointsTotal);
    }

    [Fact]
    public async Task SavePenalty_create_scopeIsPerDriver()
    {
        using var ctx = new SqliteTestContext();
        SeedLeagueAndDriver(ctx, "l1", "Alpha", number: 1);
        SeedLeagueAndDriver(ctx, "l1", "Beta", number: 2);
        SeedPenalty(ctx, "l1", "Alpha", 5, "Alpha alt");

        var ctrl = BuildController(ctx);
        var result = await ctrl.SavePenalty(
            id: null, leagueId: "l1", date: new DateTime(2026, 8, 2),
            driver: "Beta", penaltyType: "Zeitstrafe + Strafpunkte", points: 3,
            raceTrack: null, secondDriver: null, isBetweenTwoDrivers: false,
            incident: null, reason: "Beta neu", isPublic: true);

        Assert.IsType<RedirectToActionResult>(result);

        // Beta hat keinen Bestand ⇒ nur eigene Punkte zaehlen.
        var doc = ctx.NewContext().LeaguePenalties.Single(p => p.Reason == "Beta neu");
        Assert.Equal(3, doc.DriverPointsTotal);
    }

    [Fact]
    public async Task SavePenalty_edit_doesNotUpdateSnapshot()
    {
        using var ctx = new SqliteTestContext();
        SeedLeagueAndDriver(ctx, "l1", "Alpha");
        SeedPenalty(ctx, "l1", "Alpha", 5, "vorher");

        var ctrl = BuildController(ctx);

        await ctrl.SavePenalty(
            id: null, leagueId: "l1", date: new DateTime(2026, 8, 2),
            driver: "Alpha", penaltyType: "Zeitstrafe + Strafpunkte", points: 3,
            raceTrack: null, secondDriver: null, isBetweenTwoDrivers: false,
            incident: null, reason: "neue Strafe", isPublic: true);

        var created = ctx.NewContext().LeaguePenalties.Single(p => p.Reason == "neue Strafe");
        Assert.Equal(8, created.DriverPointsTotal);

        // Alt-Dokument bearbeiten: Punkte aendern (3 → 1), Reason aendern.
        var editResult = await ctrl.SavePenalty(
            id: created.Id, leagueId: "l1", date: new DateTime(2026, 8, 2),
            driver: "Alpha", penaltyType: "Zeitstrafe + Strafpunkte", points: 1,
            raceTrack: null, secondDriver: null, isBetweenTwoDrivers: false,
            incident: null, reason: "geaendert", isPublic: true);

        Assert.IsType<RedirectToActionResult>(editResult);

        var edited = ctx.NewContext().LeaguePenalties.Single(p => p.Id == created.Id);
        Assert.Equal(1, edited.Points);
        // Snapshot bleibt eingefroren — NICHT auf 6 (5 Bestand + 1) neu berechnet.
        Assert.Equal(8, edited.DriverPointsTotal);
    }

    [Fact]
    public async Task SavePenalty_laterCreate_doesNotUpdateEarlierSnapshot()
    {
        using var ctx = new SqliteTestContext();
        SeedLeagueAndDriver(ctx, "l1", "Alpha");

        var ctrl = BuildController(ctx);

        await ctrl.SavePenalty(
            id: null, leagueId: "l1", date: new DateTime(2026, 8, 2),
            driver: "Alpha", penaltyType: "Zeitstrafe + Strafpunkte", points: 3,
            raceTrack: null, secondDriver: null, isBetweenTwoDrivers: false,
            incident: null, reason: "Doku 1", isPublic: true);

        await ctrl.SavePenalty(
            id: null, leagueId: "l1", date: new DateTime(2026, 8, 9),
            driver: "Alpha", penaltyType: "Zeitstrafe + Strafpunkte", points: 2,
            raceTrack: null, secondDriver: null, isBetweenTwoDrivers: false,
            incident: null, reason: "Doku 2", isPublic: true);

        var fresh = ctx.NewContext();
        Assert.Equal(3, fresh.LeaguePenalties.Single(p => p.Reason == "Doku 1").DriverPointsTotal);
        Assert.Equal(5, fresh.LeaguePenalties.Single(p => p.Reason == "Doku 2").DriverPointsTotal);
    }

    [Fact]
    public async Task SavePenalty_create_zeroPoints_keepsPriorStanding()
    {
        using var ctx = new SqliteTestContext();
        SeedLeagueAndDriver(ctx, "l1", "Alpha");
        SeedPenalty(ctx, "l1", "Alpha", 5, "vorher");

        var ctrl = BuildController(ctx);
        var result = await ctrl.SavePenalty(
            id: null, leagueId: "l1", date: new DateTime(2026, 8, 2),
            driver: "Alpha", penaltyType: "DSQ", points: 0,
            raceTrack: null, secondDriver: null, isBetweenTwoDrivers: false,
            incident: null, reason: "DSQ ohne Punkte", isPublic: true);

        Assert.IsType<RedirectToActionResult>(result);

        // 0 Punkte-Doku zieht den Bestand NICHT runter — Snapshot = Stand (5).
        var doc = ctx.NewContext().LeaguePenalties.Single(p => p.Reason == "DSQ ohne Punkte");
        Assert.Equal(5, doc.DriverPointsTotal);
    }

    // ── Stubs ──────────────────────────────────────────────────────────────────

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
