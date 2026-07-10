using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Options;
using <OWNER_HANDLE>_ERC.Services;
using <OWNER_HANDLE>_ERC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace <OWNER_HANDLE>_ERC.Tests;

/// <summary>
/// Regression tests for <see cref="DriverProfileService.RenameIngameNameAsync"/>: a profile
/// name change must propagate to the public results/standings, no matter which alias a row
/// was originally entered under and regardless of casing/whitespace differences.
/// </summary>
public class DriverProfileRenameTests
{
    private static DriverProfileService CreateService(<OWNER_HANDLE>_ERC.Data.AppDbContext db)
        => new(db, Microsoft.Extensions.Options.Options.Create(new DriverMatchingOptions()));

    private static DriverProfile SeedProfile(
        <OWNER_HANDLE>_ERC.Data.AppDbContext db,
        string discordId,
        string displayName,
        string preferredPlatform,
        params (string Platform, string Tag, bool IsPrimary)[] tags)
    {
        var profile = new DriverProfile
        {
            DiscordId = discordId,
            DiscordName = $"{displayName}***REMOVED***0001",
            DisplayName = displayName,
            PreferredPlatform = preferredPlatform,
            GamerTags = tags.Select(t => new DriverGamerTag
            {
                DiscordId = discordId,
                Platform = t.Platform,
                GamerTag = t.Tag,
                IsPrimary = t.IsPrimary
            }).ToList()
        };
        db.DriverProfiles.Add(profile);
        db.SaveChanges();
        return profile;
    }

    [Fact]
    public async Task Rename_updates_standing_entered_under_non_preferred_platform_tag()
    {
        using var ctx = new SqliteTestContext();
        ApplicationServiceTestHarness.SeedLeague(ctx.Db, "l1", "League One");
        // Preferred platform is EA, but the standing was entered under the Steam tag.
        SeedProfile(ctx.Db, "discord-1", displayName: "EaName", preferredPlatform: "EA",
            ("EA", "EaName", true), ("Steam", "SteamName", false));
        ctx.Db.DriverStandings.Add(new DriverStanding
        {
            LeagueId = "l1", Driver = "SteamName", Team = "", Position = 1, Points = 50
        });
        ctx.Db.SaveChanges();

        var changed = await CreateService(ctx.Db).RenameIngameNameAsync("discord-1", "NewName", "admin-1");

        Assert.True(changed > 0);
        using var verify = ctx.NewContext();
        Assert.Equal("NewName", (await verify.DriverStandings.SingleAsync()).Driver);
    }

    [Fact]
    public async Task Rename_updates_references_despite_casing_and_whitespace()
    {
        using var ctx = new SqliteTestContext();
        ApplicationServiceTestHarness.SeedLeague(ctx.Db, "l1", "League One");
        SeedProfile(ctx.Db, "discord-2", displayName: "EaName", preferredPlatform: "EA",
            ("EA", "EaName", true));
        // Hand-entered rows with different casing / stray whitespace.
        ctx.Db.DriverStandings.Add(new DriverStanding
        {
            LeagueId = "l1", Driver = "  eaname ", Team = "", Position = 1, Points = 30
        });
        var race = new RaceResult { LeagueId = "l1", Date = DateTime.UtcNow, Track = "Spa", Winner = "EANAME", FastestLap = "" };
        race.Finishes.Add(new RaceFinish { Driver = "eaName", Position = 1 });
        ctx.Db.RaceResults.Add(race);
        ctx.Db.SaveChanges();

        var changed = await CreateService(ctx.Db).RenameIngameNameAsync("discord-2", "NewName", "admin-1");

        Assert.True(changed >= 3);
        using var verify = ctx.NewContext();
        Assert.Equal("NewName", (await verify.DriverStandings.SingleAsync()).Driver);
        var reloaded = await verify.RaceResults.Include(r => r.Finishes).SingleAsync();
        Assert.Equal("NewName", reloaded.Winner);
        Assert.Equal("NewName", reloaded.Finishes.Single().Driver);
    }

    [Fact]
    public async Task Rename_sets_display_name_and_primary_tag_to_new_name()
    {
        using var ctx = new SqliteTestContext();
        SeedProfile(ctx.Db, "discord-3", displayName: "OldName", preferredPlatform: "EA",
            ("EA", "OldName", true));

        await CreateService(ctx.Db).RenameIngameNameAsync("discord-3", "BrandNew", "admin-1");

        using var verify = ctx.NewContext();
        var profile = await verify.DriverProfiles.Include(p => p.GamerTags).SingleAsync();
        Assert.Equal("BrandNew", profile.DisplayName);
        Assert.Equal("BrandNew", profile.GamerTags.Single(t => t.Platform == "EA").GamerTag);
    }
}
