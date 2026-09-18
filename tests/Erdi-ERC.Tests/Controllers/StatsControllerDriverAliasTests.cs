using Erdi_ERC.Controllers;
using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Erdi_ERC.Options;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erdi_ERC.Tests.Controllers;

/// <summary>
/// Alias-Auflösung in <see cref="StatsController"/>: Ein Fahrer kann in Finishes unter seinem
/// GamerTag, seinem DisplayName ODER seinem DiscordName stehen. /fahrerkarten nahm den DiscordName
/// schon immer mit, <c>BuildDriverLevelsAsync</c> nicht — dieselbe Inkonsistenz, die auf /Profile
/// zu leeren Stats geführt hat (Prod-Befund 2026-09-17). Beide Seiten müssen den DiscordName zählen.
/// </summary>
public class StatsControllerDriverAliasTests
{
    private const string DiscordId = "4711";

    private static StatsController CreateController(SqliteTestContext ctx)
        => new(
            ctx.Db,
            new StubWebHostEnvironment(),
            Microsoft.Extensions.Options.Options.Create(new ApplicationOptions()),
            Microsoft.Extensions.Options.Options.Create(new F1ScoringOptions()),
            NullLogger<StatsController>.Instance,
            new MemoryCache(new MemoryCacheOptions()));

    private const string LeagueId = "div-main";

    /// <summary>Legt die Liga an, der die Testrennen zugeordnet werden (RaceResult hat einen FK auf League).</summary>
    private static League EnsureLeague(SqliteTestContext ctx)
    {
        var existing = ctx.Db.Leagues.Local.FirstOrDefault(l => l.Id == LeagueId);
        if (existing is not null) return existing;

        var league = new League { Id = LeagueId, Name = "Division 1" };
        ctx.Db.Leagues.Add(league);
        return league;
    }

    private static void AddRace(SqliteTestContext ctx, string driver, int position, bool fastestLap = false)
    {
        var league = EnsureLeague(ctx);
        var race = new RaceResult
        {
            LeagueId = LeagueId,
            Date = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Track = "Bahrain",
            Winner = driver
        };
        race.Finishes.Add(new RaceFinish { Driver = driver, Position = position, FastestLap = fastestLap });
        league.Races.Add(race);
        ctx.Db.SaveChanges();
    }

    /// <summary>Profil, dessen Ergebnisse ausschließlich unter dem DiscordName eingetragen sind.</summary>
    private static void SeedProfileAndRaceUnderDiscordName(SqliteTestContext ctx)
    {
        ctx.Db.DriverProfiles.Add(new DriverProfile
        {
            DiscordId = DiscordId,
            DiscordName = "jilreth",
            DisplayName = null,
            GamerTags = { new DriverGamerTag { DiscordId = DiscordId, Platform = "EA", GamerTag = "   " } }
        });
        AddRace(ctx, "Jilreth", position: 1, fastestLap: true);
    }

    [Fact]
    public async Task DriverLevels_countsFinishesEnteredUnderDiscordName()
    {
        using var ctx = new SqliteTestContext();
        SeedProfileAndRaceUnderDiscordName(ctx);

        var result = await CreateController(ctx).DriverLevels();

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<DriverLevelsPageViewModel>(view.Model);
        var entry = Assert.Single(vm.Entries, e => e.DiscordId == DiscordId);
        Assert.Equal(1, entry.Races);
        Assert.Equal(1, entry.Wins);
        Assert.Equal(1, entry.Podiums);
    }

    [Fact]
    public async Task DriverLevels_countsOnlyTheDriversOwnFinishes()
    {
        using var ctx = new SqliteTestContext();
        SeedProfileAndRaceUnderDiscordName(ctx);
        // Zweiter Fahrer mit '|' im Namen — dessen Finish darf nicht beim DiscordName-Profil landen.
        ctx.Db.DriverProfiles.Add(new DriverProfile
        {
            DiscordId = "815",
            DiscordName = "max",
            DisplayName = "ERC | Max",
            GamerTags = { new DriverGamerTag { DiscordId = "815", Platform = "EA", GamerTag = "ERC | Max" } }
        });
        ctx.Db.SaveChanges();
        AddRace(ctx, "ERC | Max", position: 2);

        var result = await CreateController(ctx).DriverLevels();

        var vm = Assert.IsType<DriverLevelsPageViewModel>(Assert.IsType<ViewResult>(result).Model);
        var jilreth = Assert.Single(vm.Entries, e => e.DiscordId == DiscordId);
        var max = Assert.Single(vm.Entries, e => e.DiscordId == "815");
        Assert.Equal(1, jilreth.Races);
        Assert.Equal(1, jilreth.Wins);
        Assert.Equal(1, max.Races);
        Assert.Equal(0, max.Wins);
        Assert.Equal(1, max.Podiums);
    }

    [Fact]
    public async Task DriverLevels_profileWithoutAnyUsableName_countsNothing()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.DriverProfiles.Add(new DriverProfile
        {
            DiscordId = DiscordId,
            DiscordName = string.Empty,
            DisplayName = null,
            GamerTags = { new DriverGamerTag { DiscordId = DiscordId, Platform = "EA", GamerTag = "   " } }
        });
        ctx.Db.SaveChanges();
        AddRace(ctx, driver: string.Empty, position: 1);

        var result = await CreateController(ctx).DriverLevels();

        var vm = Assert.IsType<DriverLevelsPageViewModel>(Assert.IsType<ViewResult>(result).Model);
        var entry = Assert.Single(vm.Entries, e => e.DiscordId == DiscordId);
        Assert.Equal(0, entry.Races);
        Assert.Equal(1, entry.Level);
    }

    /// <summary>Der Fahrerkarten-Pfad war schon korrekt — dieser Test hält ihn beim Refactoring fest.</summary>
    [Fact]
    public async Task DriverCards_countsFinishesEnteredUnderDiscordName()
    {
        using var ctx = new SqliteTestContext();
        SeedProfileAndRaceUnderDiscordName(ctx);
        var league = EnsureLeague(ctx);
        league.Standings.Add(new DriverStanding
        {
            LeagueId = LeagueId,
            Driver = "Jilreth",
            Team = "Ferrari",
            Position = 1,
            Points = 25
        });
        ctx.Db.SaveChanges();

        var result = await CreateController(ctx).DriverCards();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<List<(League League, List<(DriverProfile Profile, DriverDetailViewModel Card)> Drivers)>>(view.Model);
        var drivers = model.SelectMany(l => l.Drivers).ToList();
        Assert.Single(drivers);
        Assert.Equal(25, drivers[0].Card.TotalPoints);
        Assert.Equal(1, drivers[0].Card.Wins);
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Erdi-ERC.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Testing";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
