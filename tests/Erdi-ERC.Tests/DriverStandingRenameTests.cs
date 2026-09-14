using Erdi_ERC.Models;
using Erdi_ERC.Options;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Erdi_ERC.Tests;

/// <summary>
/// Tests für <see cref="DriverProfileService.RenameStandingDriverAsync"/> — die
/// Umbenennung eines Fahrers über die Ligaverwaltung (SaveAllStandings). Nur wenn
/// der alte Name zu einem DriverProfile aufgelöst werden kann, wird systemweit
/// propagiert (Profil/Fahrerkarte, andere Ligen, Renn-Ergebnisse). Ohne Profil-Match
/// passiert nichts — kein Risiko, einen gleichnamigen Fremdfahrer umzubenennen.
/// </summary>
public class DriverStandingRenameTests
{
    private static DriverProfileService CreateService(Erdi_ERC.Data.AppDbContext db)
        => new(db, OptionsFactory.Create(new DriverMatchingOptions()));

    private static DriverProfile SeedProfile(
        Erdi_ERC.Data.AppDbContext db,
        string discordId,
        string displayName,
        string discordName,
        string preferredPlatform,
        params (string Platform, string Tag, bool IsPrimary)[] tags)
    {
        var profile = new DriverProfile
        {
            DiscordId = discordId,
            DiscordName = discordName,
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
    public async Task RenameStandingDriver_propagatesToOtherLeagueFinishAndProfile()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "l1", Name = "League One" });
        ctx.Db.Leagues.Add(new League { Id = "l2", Name = "League Two" });
        ctx.Db.SaveChanges();
        SeedProfile(ctx.Db, "discord-1", displayName: "OldName", discordName: "OldName#0001", preferredPlatform: "EA",
            ("EA", "OldName", true));

        // Die Liga-Zeile, die der Controller bereits umbenannt hat (l1) — bleibt unangetastet.
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l1", Driver = "NewName", Team = "", Position = 1, Points = 50 });
        // Andere Liga + Renn-Ergebnis mit altem Namen → müssen propagiert werden.
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l2", Driver = "OldName", Team = "", Position = 1, Points = 30 });
        var race = new RaceResult { LeagueId = "l2", Date = DateTime.UtcNow, Track = "Spa", Winner = "OldName", FastestLap = "" };
        race.Finishes.Add(new RaceFinish { Driver = "OldName", Position = 1 });
        ctx.Db.RaceResults.Add(race);
        ctx.Db.SaveChanges();

        var changed = await CreateService(ctx.Db).RenameStandingDriverAsync("OldName", "NewName", "admin-1");

        Assert.True(changed >= 3);
        using var verify = ctx.NewContext();
        Assert.Equal("NewName", (await verify.DriverStandings.SingleAsync(s => s.LeagueId == "l2")).Driver);
        var reloaded = await verify.RaceResults.Include(r => r.Finishes).SingleAsync();
        Assert.Equal("NewName", reloaded.Winner);
        Assert.Equal("NewName", reloaded.Finishes.Single().Driver);
        var profile = await verify.DriverProfiles.Include(p => p.GamerTags).SingleAsync();
        Assert.Equal("NewName", profile.DisplayName);
    }

    [Fact]
    public async Task RenameStandingDriver_noProfileMatch_returnsZeroAndChangesNothing()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "l2", Name = "League Two" });
        ctx.Db.SaveChanges();
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l2", Driver = "Unknown", Team = "", Position = 1, Points = 10 });
        ctx.Db.SaveChanges();

        var changed = await CreateService(ctx.Db).RenameStandingDriverAsync("Unknown", "NewName", "admin-1");

        Assert.Equal(0, changed);
        using var verify = ctx.NewContext();
        Assert.Equal("Unknown", (await verify.DriverStandings.SingleAsync()).Driver);
    }

    [Fact]
    public async Task RenameStandingDriver_sameNameCaseInsensitive_returnsZero()
    {
        using var ctx = new SqliteTestContext();
        SeedProfile(ctx.Db, "discord-1", displayName: "OldName", discordName: "OldName#0001", preferredPlatform: "EA",
            ("EA", "OldName", true));

        var changed = await CreateService(ctx.Db).RenameStandingDriverAsync("OldName", "oldname", "admin-1");

        Assert.Equal(0, changed);
    }

    [Fact]
    public async Task RenameStandingDriver_resolvesByDiscordName()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "l2", Name = "League Two" });
        ctx.Db.SaveChanges();
        // Profil, dessen GamerTag/DisplayName NICHT zum Liga-Namen passt — nur der
        // DiscordName matcht. FindByDriverNameAsync muss auch DiscordName auflösen.
        SeedProfile(ctx.Db, "discord-1", displayName: "EaTag", discordName: "Basti#0001", preferredPlatform: "EA",
            ("EA", "EaTag", true));
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l2", Driver = "Basti#0001", Team = "", Position = 1, Points = 20 });
        ctx.Db.SaveChanges();

        var changed = await CreateService(ctx.Db).RenameStandingDriverAsync("Basti#0001", "NewName", "admin-1");

        Assert.True(changed > 0);
        using var verify = ctx.NewContext();
        Assert.Equal("NewName", (await verify.DriverStandings.SingleAsync()).Driver);
    }
}
