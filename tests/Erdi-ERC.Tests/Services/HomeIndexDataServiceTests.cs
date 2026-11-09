using Erdi_ERC.Models;
using Erdi_ERC.Options;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Erdi_ERC.Tests.Services;

/// <summary>
/// Startseiten-Infotafel: <see cref="HomeIndexDataService"/> liefert nur Giveaways,
/// deren Zeitraum "jetzt" enthält (StartAt ≤ jetzt ≤ EndAt). Vergangene und noch
/// nicht gestartete Giveaways dürfen nicht auftauchen — die Sichtbarkeit ist
/// rein zeitraum-basiert (User-Anforderung 2026-09-14).
/// </summary>
public class HomeIndexDataServiceTests
{
    private static IMemoryCache NewCache() => new MemoryCache(new MemoryCacheOptions());
    private static IOptions<F1ScoringOptions> F1Scoring() => Microsoft.Extensions.Options.Options.Create(new F1ScoringOptions());

    private static HomeIndexDataService BuildService(SqliteTestContext ctx)
    {
        var cache = NewCache();
        return new HomeIndexDataService(
            ctx.Db,
            cache,
            new OverallConstructorsService(ctx.Db, cache, F1Scoring()),
            new StreamScheduleQueryService(ctx.Db, cache),
            F1Scoring(),
            NullLogger<HomeIndexDataService>.Instance);
    }

    [Fact]
    public async Task GetAsync_returnsOnlyCurrentlyActiveGiveaways()
    {
        using var ctx = new SqliteTestContext();
        var now = DateTime.UtcNow;

        ctx.Db.Giveaways.AddRange(
            // vergangen — EndAt liegt in der Vergangenheit
            new Giveaway { Title = "Vergangen",  StartAt = now.AddDays(-10), EndAt = now.AddDays(-5) },
            // läuft gerade
            new Giveaway { Title = "Läuft",     StartAt = now.AddDays(-1),  EndAt = now.AddDays(+5) },
            // noch nicht gestartet — StartAt liegt in der Zukunft
            new Giveaway { Title = "Zukunft",   StartAt = now.AddDays(+2),  EndAt = now.AddDays(+9) });
        ctx.Db.SaveChanges();

        var data = await BuildService(ctx).GetAsync();

        var active = Assert.Single(data.ActiveGiveaways);
        Assert.Equal("Läuft", active.Title);
    }

    [Fact]
    public async Task GetAsync_empty_table_returnsEmptyList()
    {
        using var ctx = new SqliteTestContext();

        var data = await BuildService(ctx).GetAsync();

        Assert.NotNull(data.ActiveGiveaways);
        Assert.Empty(data.ActiveGiveaways);
    }

    [Fact]
    public async Task GetAsync_ordersByEndAtSoonestFirst()
    {
        using var ctx = new SqliteTestContext();
        var now = DateTime.UtcNow;

        ctx.Db.Giveaways.AddRange(
            new Giveaway { Title = "Spät",  StartAt = now.AddDays(-1), EndAt = now.AddDays(+9) },
            new Giveaway { Title = "Früh",  StartAt = now.AddDays(-3), EndAt = now.AddDays(+2) });
        ctx.Db.SaveChanges();

        var data = await BuildService(ctx).GetAsync();

        Assert.Collection(data.ActiveGiveaways,
            g => Assert.Equal("Früh", g.Title),
            g => Assert.Equal("Spät", g.Title));
    }

    /// <summary>Die Startseiten-Einblendung zeigt die letzte abgeschlossene DOTD-Wahl
    /// samt Stimmenanteil und der Wahl-Id als Merker; ohne Wahl bleibt sie ganz weg.</summary>
    [Fact]
    public async Task GetAsync_liefertLetztenFahrerDesTages()
    {
        using var ctx = new SqliteTestContext();
        var vote = new DotdVote
        {
            LeagueId = "pro",
            LeagueName = "Pro Liga",
            StartedAt = DateTime.UtcNow.AddHours(-2),
            ClosedAt = DateTime.UtcNow.AddHours(-1),
            WinnerDriver = "ERC | michi582015",
            WinnerVotes = 3,
            TotalVotes = 4
        };
        ctx.Db.DotdVotes.Add(vote);
        ctx.Db.SaveChanges();

        var data = await BuildService(ctx).GetAsync();

        var dotd = Assert.IsType<HomeDotd>(data.LastDotd);
        Assert.Equal(vote.Id, dotd.Id);
        Assert.Equal("Pro Liga", dotd.League);
        // Der volle Name bleibt stehen — er ist der Schlüssel für den Profil-Lookup
        // der Fahrerkarte; gekürzt wird erst in der View (DriverNameHelper.Display).
        Assert.Equal("ERC | michi582015", dotd.Winner);
        Assert.Equal(75, dotd.Percent);
    }

    [Fact]
    public async Task GetAsync_offeneWahl_zeigtDenLetztenSiegerTrotzdem()
    {
        using var ctx = new SqliteTestContext();
        var geschlossen = new DotdVote
        {
            LeagueId = "pro",
            LeagueName = "Pro Liga",
            StartedAt = DateTime.UtcNow.AddHours(-3),
            ClosedAt = DateTime.UtcNow.AddHours(-2),
            WinnerDriver = "Luke-Berger",
            WinnerVotes = 1,
            TotalVotes = 2
        };
        ctx.Db.DotdVotes.AddRange(geschlossen, new DotdVote
        {
            LeagueId = "pro",
            LeagueName = "Pro Liga",
            StartedAt = DateTime.UtcNow.AddMinutes(-10)
        });
        ctx.Db.SaveChanges();

        var data = await BuildService(ctx).GetAsync();

        var dotd = Assert.IsType<HomeDotd>(data.LastDotd);
        Assert.Equal(geschlossen.Id, dotd.Id);
        Assert.Equal("Luke-Berger", dotd.Winner);
        Assert.Equal(50, dotd.Percent);
    }

    [Fact]
    public async Task GetAsync_ohneWahl_keineDotdTafel()
    {
        using var ctx = new SqliteTestContext();

        var data = await BuildService(ctx).GetAsync();

        Assert.Null(data.LastDotd);
    }
}
