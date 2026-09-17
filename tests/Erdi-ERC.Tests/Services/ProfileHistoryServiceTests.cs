using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Erdi_ERC.Tests.Services;

/// <summary>
/// Regression für leere Fahrer-Profile (Prod-Befund 2026-09-17): <see cref="ProfileHistoryService"/>
/// baute sein Match-Set aus dem mit '|' zusammengejointen Cache-Key zurück (<c>Split('|')</c>).
/// Die Fahrernamen der Liga enthalten aber selbst ein '|' ("ERC | Max") — der Alias zerfiel
/// dadurch in die Fragmente "erc " und " max" und matchte keine einzige Standings-/Finish-Zeile.
/// Ergebnis: 0 Rennen / 0 Siege / 0 Punkte auf /Profile/{discordId}, obwohl /fahrerkarten und
/// die Ewige Liste Werte zeigten. Betraf praktisch das komplette Fahrerfeld.
/// </summary>
public class ProfileHistoryServiceTests
{
    private static ProfileHistoryService BuildService(SqliteTestContext ctx)
        => new(ctx.Db, new MemoryCache(new MemoryCacheOptions()));

    private static void SeedLeague(
        SqliteTestContext ctx,
        string leagueId,
        string driver,
        int points,
        int position,
        bool fastestLap = false)
    {
        var league = new League { Id = leagueId, Name = leagueId };
        league.Standings.Add(new DriverStanding
        {
            LeagueId = leagueId,
            Driver = driver,
            Team = "Ferrari",
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
        race.Finishes.Add(new RaceFinish { Driver = driver, Position = position, FastestLap = fastestLap });
        league.Races.Add(race);

        ctx.Db.Leagues.Add(league);
        ctx.Db.SaveChanges();
    }

    [Fact]
    public async Task GetHistoryAsync_matchesDriverNameContainingPipeDelimiter()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx, "div-main", "ERC | Max", points: 75, position: 1, fastestLap: true);

        var history = await BuildService(ctx).GetHistoryAsync(new[] { "ERC | Max" });

        Assert.NotNull(history);
        Assert.Equal(75, history!.TotalPoints);
        Assert.Equal(1, history.Wins);
        Assert.Equal(1, history.Podiums);
        Assert.Equal(1, history.FastestLaps);
        Assert.Equal(1, history.BestFinish);
        var race = Assert.Single(history.Races);
        Assert.Equal(1, race.Position);
        Assert.Equal("Ferrari", race.Team);
        Assert.False(race.WasReserve);
    }

    [Fact]
    public async Task GetHistoryAsync_pipeAliasAndSplitAliases_doNotShareCacheEntry()
    {
        using var ctx = new SqliteTestContext();
        // "ERC|Max" (ein Alias) und "ERC" + "Max" (zwei Aliase) ergaben vor dem Fix denselben
        // Cache-Key "erc|max" — der zweite Aufruf bekam das falsche Ergebnis aus dem Cache.
        SeedLeague(ctx, "div-main", "ERC|Max", points: 75, position: 1);
        SeedLeague(ctx, "div-b", "ERC", points: 10, position: 2);
        SeedLeague(ctx, "div-c", "Max", points: 20, position: 3);

        var service = BuildService(ctx);

        var piped = await service.GetHistoryAsync(new[] { "ERC|Max" });
        var split = await service.GetHistoryAsync(new[] { "ERC", "Max" });

        Assert.NotNull(piped);
        Assert.Equal(75, piped!.TotalPoints);
        Assert.Equal(1, piped.Wins);
        Assert.Single(piped.Races);

        Assert.NotNull(split);
        Assert.Equal(30, split!.TotalPoints);
        Assert.Equal(0, split.Wins);
        Assert.Equal(2, split.Podiums);
        Assert.Equal(2, split.Races.Count);

        // Zweiter Aufruf mit dem Pipe-Alias: darf nicht vom Split-Aufruf überschrieben sein.
        var pipedAgain = await service.GetHistoryAsync(new[] { "ERC|Max" });
        Assert.Equal(75, pipedAgain!.TotalPoints);
        Assert.Single(pipedAgain.Races);
    }

    [Fact]
    public async Task GetHistoryAsync_isCaseInsensitiveAndTrimsAliases()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx, "div-main", "ERC | Max", points: 75, position: 1);

        var history = await BuildService(ctx).GetHistoryAsync(new[] { "  erc | mAx  " });

        Assert.NotNull(history);
        Assert.Equal(75, history!.TotalPoints);
    }

    [Fact]
    public async Task GetHistoryAsync_matchesPlainAliasWithoutDelimiter()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx, "div-main", "Jilreth", points: 10, position: 2);

        var history = await BuildService(ctx).GetHistoryAsync(new[] { "Jilreth" });

        Assert.NotNull(history);
        Assert.Equal(10, history!.TotalPoints);
        Assert.Single(history.Races);
    }

    [Fact]
    public async Task GetHistoryAsync_returnsNullWhenAliasesAreBlank()
    {
        using var ctx = new SqliteTestContext();

        var history = await BuildService(ctx).GetHistoryAsync(new[] { "   ", string.Empty });

        Assert.Null(history);
    }
}
