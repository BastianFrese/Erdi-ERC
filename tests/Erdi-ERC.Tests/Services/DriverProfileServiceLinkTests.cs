using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Erdi_ERC.Options;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Erdi_ERC.Tests.Services;

/// <summary>
/// Tests fuer <see cref="DriverProfileService.GetUnlinkedDriversAsync"/> und
/// <see cref="DriverProfileService.LinkDriverAsync"/>: ein in
/// DriverStandings vorhandener Fahrer ohne Profil soll per Discord-ID
/// verknuepft werden koennen, OHNE die Standings umzubenennen.
/// </summary>
public class DriverProfileServiceLinkTests
{
    private static DriverProfileService CreateService(AppDbContext db)
        => new(db, OptionsFactory.Create(new DriverMatchingOptions
        {
            MinQueryLength = 2,
            MaxLevenshteinDistance = 2,
            MaxSuggestions = 5,
        }));

    private static League SeedLeague(AppDbContext db, string id, string name = "ERDI10")
    {
        var league = new League { Id = id, Name = name, IsArchived = false };
        db.Leagues.Add(league);
        db.SaveChanges();
        return league;
    }

    private static DriverStanding SeedStanding(AppDbContext db, string leagueId, string driver, int? number = null, string? team = null)
    {
        var standing = new DriverStanding
        {
            LeagueId = leagueId,
            Driver = driver,
            DriverNumber = number,
            // Team ist in SQLite-Tests NOT NULL; "" als neutraler Default.
            Team = team ?? string.Empty,
            Points = 0
        };
        db.DriverStandings.Add(standing);
        db.SaveChanges();
        return standing;
    }

    [Fact]
    public async Task GetUnlinkedDriversAsync_emptyDb_returnsEmpty()
    {
        using var ctx = new SqliteTestContext();
        var svc = CreateService(ctx.Db);

        var result = await svc.GetUnlinkedDriversAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUnlinkedDriversAsync_standingWithoutProfile_isReported()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx.Db, "erdi10");
        SeedStanding(ctx.Db, "erdi10", "Mick Schumacher", number: 16, team: "Mercedes");

        var svc = CreateService(ctx.Db);
        var result = await svc.GetUnlinkedDriversAsync();

        Assert.Single(result);
        var u = result[0];
        Assert.Equal("Mick Schumacher", u.Name);
        Assert.Equal(16, u.DriverNumber);
        Assert.Equal("Mercedes", u.Team);
        Assert.Equal(new[] { "erdi10" }, u.LeagueIds);
    }

    [Fact]
    public async Task GetUnlinkedDriversAsync_standingWithProfile_isFilteredOut()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx.Db, "erdi10");
        SeedStanding(ctx.Db, "erdi10", "Mick Schumacher", number: 16);

        // Profil + GamerTag anlegen -> Standing gilt als verknuepft.
        var profile = new DriverProfile
        {
            DiscordId = "111",
            DiscordName = "mick-discord",
            DisplayName = "Mick Schumacher",
            GamerTags = new List<DriverGamerTag>
            {
                new() { DiscordId = "111", Platform = "EA", GamerTag = "Mick Schumacher", IsPrimary = true }
            }
        };
        ctx.Db.DriverProfiles.Add(profile);
        ctx.Db.SaveChanges();

        var svc = CreateService(ctx.Db);
        var result = await svc.GetUnlinkedDriversAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUnlinkedDriversAsync_sameNameAcrossLeagues_aggregatesLeagues()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx.Db, "erdi10");
        SeedLeague(ctx.Db, "erdi9");
        SeedStanding(ctx.Db, "erdi10", "Charles Leclerc", number: 16);
        SeedStanding(ctx.Db, "erdi9",  "Charles Leclerc", number: 16);

        var svc = CreateService(ctx.Db);
        var result = await svc.GetUnlinkedDriversAsync();

        Assert.Single(result);
        Assert.Equal(2, result[0].OccurrenceCount);
        Assert.Equal(2, result[0].LeagueIds.Count);
    }

    [Fact]
    public async Task LinkDriverAsync_createsProfileAndGamerTag_doesNotRenameStanding()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx.Db, "erdi10");
        SeedStanding(ctx.Db, "erdi10", "Mick Schumacher", number: 16, team: "Mercedes");

        var svc = CreateService(ctx.Db);
        var result = await svc.LinkDriverAsync(
            driverName:    "Mick Schumacher",
            discordId:     "111111111111111111",
            discordName:   "mick-discord",
            platform:      "EA",
            actorDiscordId: "admin-id",
            ct:            default);

        Assert.True(result.Created);
        Assert.Equal("111111111111111111", result.ExistingDiscordId);

        // Profil + Tag existieren.
        var profile = ctx.Db.DriverProfiles
            .Include(p => p.GamerTags)
            .FirstOrDefault(p => p.DiscordId == "111111111111111111");
        Assert.NotNull(profile);
        Assert.Single(profile!.GamerTags);
        Assert.Equal("Mick Schumacher", profile.GamerTags[0].GamerTag);
        Assert.Equal("EA", profile.GamerTags[0].Platform);

        // WICHTIG: Standing wurde NICHT umbenannt.
        var standing = ctx.Db.DriverStandings.First(s => s.LeagueId == "erdi10");
        Assert.Equal("Mick Schumacher", standing.Driver);
        Assert.Equal(16, standing.DriverNumber);
    }

    [Fact]
    public async Task LinkDriverAsync_againSameName_sameProfile_isIdempotent()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx.Db, "erdi10");
        SeedStanding(ctx.Db, "erdi10", "Mick Schumacher", number: 16);

        var svc = CreateService(ctx.Db);
        await svc.LinkDriverAsync("Mick Schumacher", "111", "mick-discord", "EA", "admin", default);
        var second = await svc.LinkDriverAsync("Mick Schumacher", "111", "mick-discord", "EA", "admin", default);

        Assert.False(second.Created);
        Assert.Equal("111", second.ExistingDiscordId);

        var profile = ctx.Db.DriverProfiles.Include(p => p.GamerTags).First();
        Assert.Single(profile.GamerTags); // kein doppelter Tag
    }

    [Fact]
    public async Task LinkDriverAsync_duplicateNameForOtherProfile_returnsExistingId()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx.Db, "erdi10");
        SeedStanding(ctx.Db, "erdi10", "Mick Schumacher", number: 16);

        // Erstes Profil anlegen.
        var svc = CreateService(ctx.Db);
        await svc.LinkDriverAsync("Mick Schumacher", "111", "mick-discord", "EA", "admin", default);

        // Versuch, denselben Namen einer ANDEREN Discord-ID zuzuordnen -> Schutz.
        var result = await svc.LinkDriverAsync("Mick Schumacher", "222", "anderer", "EA", "admin", default);

        Assert.False(result.Created);
        Assert.Equal("111", result.ExistingDiscordId);

        var profiles = ctx.Db.DriverProfiles.ToList();
        Assert.Single(profiles); // kein zweites Profil angelegt
    }

    [Fact]
    public async Task LinkDriverAsync_existingProfileWithSamePlatformTag_linksViaDisplayName()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx.Db, "erdi10");
        SeedStanding(ctx.Db, "erdi10", "M. Schumacher", number: 16);

        // Profil existiert bereits (z. B. aus Accept-Flow) mit EA-Tag, dessen
        // GamerTag UND DisplayName vom Standing-Namen abweichen.
        ctx.Db.DriverProfiles.Add(new DriverProfile
        {
            DiscordId = "777",
            DiscordName = "mick-discord",
            DisplayName = "Mick",
            GamerTags = new List<DriverGamerTag>
            {
                new() { DiscordId = "777", Platform = "EA", GamerTag = "Schumacher88", IsPrimary = true }
            }
        });
        ctx.Db.SaveChanges();

        var svc = CreateService(ctx.Db);
        var result = await svc.LinkDriverAsync(
            "M. Schumacher", "777", "mick-discord", "EA", "admin", default);

        Assert.False(result.Created);
        Assert.Equal("777", result.ExistingDiscordId);
        // Kein zweiter EA-Tag (Unique-Index DiscordId+Platform).
        var profile = ctx.Db.DriverProfiles.Include(p => p.GamerTags).First(p => p.DiscordId == "777");
        Assert.Single(profile.GamerTags);
    }

    [Fact]
    public async Task LinkDriverAsync_existingProfileWithSamePlatformTag_freeDisplayName_setsDisplayName()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx.Db, "erdi10");
        SeedStanding(ctx.Db, "erdi10", "M. Schumacher", number: 16);

        // Profil mit EA-Tag, aber OHNE DisplayName -> Alias kann darueber registriert werden.
        ctx.Db.DriverProfiles.Add(new DriverProfile
        {
            DiscordId = "888",
            DiscordName = "mick-discord",
            GamerTags = new List<DriverGamerTag>
            {
                new() { DiscordId = "888", Platform = "EA", GamerTag = "Schumacher88", IsPrimary = true }
            }
        });
        ctx.Db.SaveChanges();

        var svc = CreateService(ctx.Db);
        var result = await svc.LinkDriverAsync(
            "M. Schumacher", "888", "mick-discord", "EA", "admin", default);

        Assert.True(result.Created);
        Assert.Equal("888", result.ExistingDiscordId);

        var profile = ctx.Db.DriverProfiles.Include(p => p.GamerTags).First(p => p.DiscordId == "888");
        Assert.Equal("M. Schumacher", profile.DisplayName);
        Assert.Single(profile.GamerTags); // weiterhin nur der EA-Tag
        // Standing unveraendert.
        Assert.Equal("M. Schumacher", ctx.Db.DriverStandings.First().Driver);
    }

    [Fact]
    public async Task LinkDriverAsync_thenUnlink_removesProfileAndStandingStaysIntact()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx.Db, "erdi10");
        SeedStanding(ctx.Db, "erdi10", "Mick Schumacher", number: 16);

        var svc = CreateService(ctx.Db);
        await svc.LinkDriverAsync("Mick Schumacher", "111", "mick-discord", "EA", "admin", default);

        // Standing ist immer noch da und unveraendert.
        var standingBefore = ctx.Db.DriverStandings.First().Driver;
        Assert.Equal("Mick Schumacher", standingBefore);

        // Unlink.
        var removed = await svc.UnlinkDriverAsync("111");
        Assert.Equal(1, removed);

        // Standing bleibt erhalten (Backfill war non-destructive).
        var standingAfter = ctx.Db.DriverStandings.First().Driver;
        Assert.Equal("Mick Schumacher", standingAfter);

        // Profil ist weg, weil der einzige GamerTag weg ist.
        Assert.Empty(ctx.Db.DriverProfiles);
    }
}