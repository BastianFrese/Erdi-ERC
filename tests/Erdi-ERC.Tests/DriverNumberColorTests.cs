using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Helpers;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Options;
using <OWNER_HANDLE>_ERC.Services;
using <OWNER_HANDLE>_ERC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace <OWNER_HANDLE>_ERC.Tests;

/// <summary>
/// Tests für die personalisierbare Fahrernummer-Farbe im Profil.
/// </summary>
public class DriverNumberColorTests
{
    private static DriverProfileService CreateService(AppDbContext db)
        => new(db, Microsoft.Extensions.Options.Options.Create(new DriverMatchingOptions()));

    private static DriverProfile SeedProfile(AppDbContext db, string? driverNumberColor = null, string? favoriteTeam = null)
    {
        var profile = new DriverProfile
        {
            DiscordId = "discord-1",
            DiscordName = "Racer***REMOVED***0001",
            DisplayName = "Racer",
            PreferredPlatform = "EA",
            DriverNumberColor = driverNumberColor,
            FavoriteTeam = favoriteTeam
        };
        db.DriverProfiles.Add(profile);
        db.SaveChanges();
        return profile;
    }

    [Theory]
    [InlineData("***REMOVED***00ff00")]
    [InlineData("***REMOVED***FF00AA")]
    [InlineData("***REMOVED***123abc")]
    public void ResolveDriverNumberColor_returns_explicit_color(string color)
    {
        using var ctx = new SqliteTestContext();
        var profile = SeedProfile(ctx.Db, driverNumberColor: color);

        var result = CreateService(ctx.Db).ResolveDriverNumberColor(profile);

        Assert.Equal(color, result);
    }

    [Fact]
    public void ResolveDriverNumberColor_falls_back_to_team_primary_color()
    {
        using var ctx = new SqliteTestContext();
        var profile = SeedProfile(ctx.Db, favoriteTeam: "Ferrari");

        var result = CreateService(ctx.Db).ResolveDriverNumberColor(profile);

        Assert.Equal(F1TeamsHelper.GetTeamByName("Ferrari")?.PrimaryColor ?? "***REMOVED***e10600", result);
    }

    [Fact]
    public void ResolveDriverNumberColor_falls_back_to_default_red()
    {
        using var ctx = new SqliteTestContext();
        var profile = SeedProfile(ctx.Db);

        var result = CreateService(ctx.Db).ResolveDriverNumberColor(profile);

        Assert.Equal("***REMOVED***e10600", result);
    }

    [Fact]
    public void ResolveDriverNumberColor_ignores_malformed_color_and_uses_fallback()
    {
        using var ctx = new SqliteTestContext();
        var profile = SeedProfile(ctx.Db, driverNumberColor: "not-a-color");

        var result = CreateService(ctx.Db).ResolveDriverNumberColor(profile);

        Assert.Equal("***REMOVED***e10600", result);
    }
}
