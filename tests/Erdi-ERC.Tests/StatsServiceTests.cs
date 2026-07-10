using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Services;
using <OWNER_HANDLE>_ERC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace <OWNER_HANDLE>_ERC.Tests;

/// <summary>
/// Verifiziert die abgeleitete Tabellen-Berechnung: Punkte kommen aus den Renn-Ergebnissen,
/// die manuelle <see cref="DriverStanding.PointsAdjustment"/> wird addiert und überlebt die
/// Neuberechnung, und Gastfahrer aus einer anderen Liga sammeln ihre Punkte in der
/// Gastgeber-Liga (Host-League-Semantik).
/// </summary>
public class StatsServiceTests
{
    private static async Task SeedLeagueAsync(SqliteTestContext ctx, string leagueId)
    {
        ctx.Db.Leagues.Add(new League { Id = leagueId, Name = leagueId });
        await ctx.Db.SaveChangesAsync();
    }

    private static async Task AddRaceAsync(SqliteTestContext ctx, string leagueId, params (string Driver, int Position)[] finishes)
    {
        var race = new RaceResult { LeagueId = leagueId, Date = DateTime.UtcNow, Track = "Testbahn" };
        ctx.Db.RaceResults.Add(race);
        await ctx.Db.SaveChangesAsync();

        foreach (var (driver, position) in finishes)
        {
            ctx.Db.RaceFinishes.Add(new RaceFinish { RaceResultId = race.RowId, Driver = driver, Position = position });
        }
        await ctx.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task RebuildLeagueStandings_addsPointsAdjustmentOnTopOfRacePoints()
    {
        // Arrange
        using var ctx = new SqliteTestContext();
        await SeedLeagueAsync(ctx, "l1");
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l1", Driver = "Alpha", PointsAdjustment = -5 });
        await ctx.Db.SaveChangesAsync();
        await AddRaceAsync(ctx, "l1", ("Alpha", 1)); // P1 = 25 Punkte

        var service = new StatsService(ctx.Db, Microsoft.Extensions.Options.Options.Create(new <OWNER_HANDLE>_ERC.Options.F1ScoringOptions()));

        // Act
        await service.RebuildLeagueStandingsAsync("l1");

        // Assert
        await using var verify = ctx.NewContext();
        var alpha = await verify.DriverStandings.SingleAsync(s => s.Driver == "Alpha");
        Assert.Equal(20, alpha.Points);          // 25 aus Rennen - 5 Korrektur
        Assert.Equal(1, alpha.Wins);
        Assert.Equal(-5, alpha.PointsAdjustment); // Korrektur bleibt erhalten
    }

    [Fact]
    public async Task RebuildLeagueStandings_appliesAdjustmentToDriverWithoutRaces()
    {
        // Arrange — reiner Bonus/Malus ohne Rennen (z.B. Mid-Season-Übernahme von Punkten)
        using var ctx = new SqliteTestContext();
        await SeedLeagueAsync(ctx, "l1");
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l1", Driver = "Bonus", PointsAdjustment = 42 });
        await ctx.Db.SaveChangesAsync();

        var service = new StatsService(ctx.Db, Microsoft.Extensions.Options.Options.Create(new <OWNER_HANDLE>_ERC.Options.F1ScoringOptions()));

        // Act
        await service.RebuildLeagueStandingsAsync("l1");

        // Assert
        await using var verify = ctx.NewContext();
        var bonus = await verify.DriverStandings.SingleAsync(s => s.Driver == "Bonus");
        Assert.Equal(42, bonus.Points);
    }

    [Fact]
    public async Task RebuildLeagueStandings_createsStandingForGuestDriverInHostLeague()
    {
        // Arrange — "Guest" fährt ein Rennen in Liga l1, hat dort aber keine Stammwertung.
        using var ctx = new SqliteTestContext();
        await SeedLeagueAsync(ctx, "l1");
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l1", Driver = "Stamm" });
        await ctx.Db.SaveChangesAsync();
        await AddRaceAsync(ctx, "l1", ("Stamm", 2), ("Guest", 1)); // Guest P1 = 25, Stamm P2 = 21

        var service = new StatsService(ctx.Db, Microsoft.Extensions.Options.Options.Create(new <OWNER_HANDLE>_ERC.Options.F1ScoringOptions()));

        // Act
        await service.RebuildLeagueStandingsAsync("l1");

        // Assert — Gast bekommt eine Wertung in der Gastgeber-Liga mit seinen Punkten.
        await using var verify = ctx.NewContext();
        var guest = await verify.DriverStandings.SingleOrDefaultAsync(s => s.LeagueId == "l1" && s.Driver == "Guest");
        Assert.NotNull(guest);
        Assert.Equal(25, guest!.Points);
        Assert.Equal(1, guest.Position); // führt die Tabelle an
    }
}
