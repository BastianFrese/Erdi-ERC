using Erdi_ERC.Controllers;
using Erdi_ERC.Models;
using Erdi_ERC.Options;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erdi_ERC.Tests.Controllers;

/// <summary>
/// Regression für leere Fahrer-Profile (Prod-Befund 2026-09-17) auf Controller-Ebene:
///  1. Fahrernamen mit '|' ("ERC | Max") dürfen die Statistik nicht mehr auf 0 setzen
///     (Ursache war der Split des Alias-Sets im <see cref="ProfileHistoryService"/>).
///  2. Der DiscordName muss als Alias zählen — sonst hat ein Fahrer, dessen Ergebnisse
///     unter dem Discord-Namen eingetragen wurden, auf /fahrerkarten Stats, im Profil
///     aber keine (die Fahrerkarten nahmen den DiscordName schon immer mit).
/// </summary>
public class ProfileControllerTests
{
    private static ProfileController CreateController(SqliteTestContext ctx)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new ProfileController(
            ctx.Db,
            new DriverProfileService(ctx.Db, Microsoft.Extensions.Options.Options.Create(new DriverMatchingOptions())),
            new NoopAudit(),
            new NoopStaticCache(),
            new NoopMediaService(),
            NullLogger<ProfileController>.Instance,
            new ProfileHistoryService(ctx.Db, cache));

        // Index() liest den eingeloggten Discord-Id für die "eigenes Profil"-Erkennung.
        TestAuthHelper.AttachContext(controller, TestAuthHelper.CreateAuthenticatedContext("999", "tester"));
        return controller;
    }

    private static void SeedLeague(SqliteTestContext ctx, string leagueId, string driver, int points, int position)
    {
        var league = new League { Id = leagueId, Name = leagueId };
        league.Standings.Add(new DriverStanding
        {
            LeagueId = leagueId,
            Driver = driver,
            Team = "Racing Bulls",
            Position = 1,
            Points = points
        });

        var race = new RaceResult
        {
            LeagueId = leagueId,
            Date = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Track = "Bahrain",
            Winner = driver
        };
        race.Finishes.Add(new RaceFinish { Driver = driver, Position = position, FastestLap = position == 1 });
        league.Races.Add(race);

        ctx.Db.Leagues.Add(league);
        ctx.Db.SaveChanges();
    }

    [Fact]
    public async Task Index_showsStatsForDriverNameContainingPipeDelimiter()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.DriverProfiles.Add(new DriverProfile
        {
            DiscordId = "42",
            DiscordName = "max",
            DisplayName = "ERC | Max",
            GamerTags =
            {
                new DriverGamerTag { DiscordId = "42", Platform = "EA", GamerTag = "ERC | Max", IsPrimary = true }
            }
        });
        ctx.Db.SaveChanges();
        SeedLeague(ctx, "div-main", "ERC | Max", points: 75, position: 1);

        var result = await CreateController(ctx).Index("42");

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<DriverDetailViewModel>(view.Model);
        Assert.Equal(75, vm.TotalPoints);
        Assert.Equal(1, vm.Wins);
        Assert.Equal(1, vm.Podiums);
        var race = Assert.Single(vm.Races);
        Assert.Equal(1, race.Position);
    }

    [Fact]
    public async Task Index_matchesResultsEnteredUnderDiscordName()
    {
        using var ctx = new SqliteTestContext();
        // Weder GamerTag noch DisplayName passen zum Ergebnis — nur der DiscordName verbindet beides.
        ctx.Db.DriverProfiles.Add(new DriverProfile { DiscordId = "7", DiscordName = "Jilreth" });
        ctx.Db.SaveChanges();
        SeedLeague(ctx, "div-main", "Jilreth", points: 10, position: 2);

        var result = await CreateController(ctx).Index("7");

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<DriverDetailViewModel>(view.Model);
        Assert.Equal(10, vm.TotalPoints);
        Assert.Equal(1, vm.Podiums);
        Assert.Single(vm.Races);
    }

    private sealed class NoopAudit : IAdminAuditService
    {
        public Task LogAsync(string action, string entityType, string entityId, string details) => Task.CompletedTask;
        public Task LogAndSaveAsync(string action, string entityType, string entityId, string details) => Task.CompletedTask;
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
}
