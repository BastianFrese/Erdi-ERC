using Erdi_ERC.Data;
using Erdi_ERC.Helpers;
using Erdi_ERC.Models;
using Erdi_ERC.Options;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Erdi_ERC.Tests;

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
            DiscordName = "Racer#0001",
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
    [InlineData("#00ff00")]
    [InlineData("#FF00AA")]
    [InlineData("#123abc")]
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

        Assert.Equal(F1TeamsHelper.GetTeamByName("Ferrari")?.PrimaryColor ?? "#e10600", result);
    }

    [Fact]
    public void ResolveDriverNumberColor_falls_back_to_default_red()
    {
        using var ctx = new SqliteTestContext();
        var profile = SeedProfile(ctx.Db);

        var result = CreateService(ctx.Db).ResolveDriverNumberColor(profile);

        Assert.Equal("#e10600", result);
    }

    [Fact]
    public void ResolveDriverNumberColor_ignores_malformed_color_and_uses_fallback()
    {
        using var ctx = new SqliteTestContext();
        var profile = SeedProfile(ctx.Db, driverNumberColor: "not-a-color");

        var result = CreateService(ctx.Db).ResolveDriverNumberColor(profile);

        Assert.Equal("#e10600", result);
    }
}
