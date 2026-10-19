using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erdi_ERC.Tests.Services;

/// <summary>
/// Vertrag des Vote-Service: eine offene Wahl, eine Stimme pro Nutzer, Mehrdeutigkeit
/// wird verworfen, Neustart verliert die Wahl nicht, DB-Ausfall liefert den letzten Stand.
/// </summary>
public class DotdVoteServiceTests
{
    private static void SeedLeague(SqliteTestContext ctx, string id, params string[] drivers)
    {
        ctx.Db.Leagues.Add(new League { Id = id, Name = "Pro", CurrentSeason = "2026" });
        var position = 1;
        foreach (var driver in drivers)
        {
            ctx.Db.DriverStandings.Add(new DriverStanding
            {
                LeagueId = id,
                Driver = driver,
                Team = "ERC",
                Position = position++
            });
        }
        ctx.Db.SaveChanges();
    }

    private static (DotdVoteService Service, DotdRuntimeState State) Create(SqliteTestContext ctx)
    {
        var state = new DotdRuntimeState();
        return (new DotdVoteService(ctx.Db, state, NullLogger<DotdVoteService>.Instance), state);
    }

    [Fact]
    public async Task Open_und_gueltige_Stimme_zaehlen()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx, "pro", "ERC | Wiener", "ERC | Müller");
        var (service, _) = Create(ctx);

        Assert.True((await service.OpenAsync("pro", "admin-1")).Ok);
        Assert.True(await service.RegisterVoteAsync("tw-1", "wiener"));

        var active = await service.GetActiveAsync();
        Assert.NotNull(active);
        Assert.Equal(1, active!.TotalVotes);
        Assert.Equal("ERC | Wiener", active.Counts.Single().Driver);
    }

    [Fact]
    public async Task Umentscheiden_ersetzt_die_alte_Stimme()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx, "pro", "ERC | Wiener", "ERC | Müller");
        var (service, _) = Create(ctx);
        await service.OpenAsync("pro", null);

        await service.RegisterVoteAsync("tw-1", "wiener");
        await service.RegisterVoteAsync("tw-1", "müller");

        var active = await service.GetActiveAsync();
        Assert.Equal(1, active!.TotalVotes);
        Assert.Equal("ERC | Müller", active.Counts.Single().Driver);
    }

    [Fact]
    public async Task Mehrdeutiger_Name_zaehlt_nicht_und_wird_protokolliert()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx, "pro", "ERC | Max Wiener", "ERC | Max Berger");
        var (service, _) = Create(ctx);
        await service.OpenAsync("pro", null);

        Assert.False(await service.RegisterVoteAsync("tw-1", "max"));
        Assert.Empty(ctx.Db.DotdBallots);
        Assert.Contains(service.RecentRejections, r => r.ChatText.Contains("max"));
    }

    [Fact]
    public async Task Schliessen_setzt_Sieger_und_loescht_Ballots()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx, "pro", "ERC | Wiener", "ERC | Müller");
        var (service, _) = Create(ctx);
        await service.OpenAsync("pro", null);
        await service.RegisterVoteAsync("tw-1", "wiener");
        await service.RegisterVoteAsync("tw-2", "wiener");
        await service.RegisterVoteAsync("tw-3", "müller");

        var result = await service.CloseAsync();

        Assert.True(result.Ok);
        var snapshot = await service.GetSnapshotAsync();
        Assert.Equal("closed", snapshot.State);
        Assert.Equal("Wiener", snapshot.Winner!.Name);
        Assert.Equal(67, snapshot.Winner.Percent);
        Assert.Equal(0, ctx.Db.DotdBallots.Count());
    }

    [Fact]
    public async Task Stimme_nach_dem_Schliessen_zaehlt_nicht()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx, "pro", "ERC | Wiener");
        var (service, _) = Create(ctx);
        await service.OpenAsync("pro", null);
        await service.CloseAsync();

        Assert.False(await service.RegisterVoteAsync("tw-1", "wiener"));
        Assert.Empty(ctx.Db.DotdBallots);
    }

    [Fact]
    public async Task Nach_Neustart_laeuft_die_offene_Wahl_weiter()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx, "pro", "ERC | Wiener", "ERC | Müller");
        var (first, _) = Create(ctx);
        await first.OpenAsync("pro", null);
        await first.RegisterVoteAsync("tw-1", "wiener");

        var secondState = new DotdRuntimeState();
        var second = new DotdVoteService(
            ctx.NewContext(), secondState, NullLogger<DotdVoteService>.Instance);

        Assert.True(await second.RegisterVoteAsync("tw-2", "müller"));
        var active = await second.GetActiveAsync();
        Assert.Equal(2, active!.TotalVotes);
    }

    [Fact]
    public async Task Umbenannter_Fahrer_bleibt_ueber_den_alten_Namen_waehlbar()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx, "pro", "ERC | Wiener");
        var (service, _) = Create(ctx);
        await service.OpenAsync("pro", null);

        var standing = ctx.Db.DriverStandings.AsTracking().Single(s => s.Driver == "ERC | Wiener");
        standing.Driver = "ERC | Wiener2";
        ctx.Db.SaveChanges();

        Assert.True(await service.RegisterVoteAsync("tw-1", "wiener"));
    }

    [Fact]
    public async Task Db_Ausfall_liefert_letzten_Stand_mit_stale()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx, "pro", "ERC | Wiener");
        var (service, _) = Create(ctx);
        await service.OpenAsync("pro", null);
        await service.GetSnapshotAsync();

        ctx.Db.Dispose();

        var snapshot = await service.GetSnapshotAsync();
        Assert.True(snapshot.Stale);
        Assert.Equal("open", snapshot.State);
    }

    [Fact]
    public async Task Zweite_Wahl_wird_abgelehnt()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx, "pro", "ERC | Wiener");
        var (service, _) = Create(ctx);

        var first = await service.OpenAsync("pro", null);
        var second = await service.OpenAsync("pro", null);

        Assert.True(first.Ok);
        Assert.False(second.Ok);
        Assert.Equal("Es läuft bereits eine Wahl für Pro.", second.Error);
    }

    [Fact]
    public async Task Gleichstand_geht_alphabetisch_an_den_Anzeigenamen()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx, "pro", "ERC | Wiener", "ERC | Müller");
        var (service, _) = Create(ctx);
        await service.OpenAsync("pro", null);
        await service.RegisterVoteAsync("tw-1", "wiener");
        await service.RegisterVoteAsync("tw-2", "müller");

        await service.CloseAsync();
        var snapshot = await service.GetSnapshotAsync();

        Assert.Equal("Müller", snapshot.Winner!.Name);
        Assert.Equal(50, snapshot.Winner.Percent);
    }

    [Fact]
    public async Task Schliessen_laesst_Ballots_bei_DB_Fehler_stehen()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx, "pro", "ERC | Wiener");
        var (seedService, _) = Create(ctx);
        await seedService.OpenAsync("pro", null);
        await seedService.RegisterVoteAsync("tw-1", "wiener");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(ctx.Db.Database.GetDbConnection())
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution)
            .AddInterceptors(new FailOnSaveInterceptor())
            .Options;
        using var failingDb = new AppDbContext(options);
        var failingService = new DotdVoteService(
            failingDb, new DotdRuntimeState(), NullLogger<DotdVoteService>.Instance);

        await Assert.ThrowsAnyAsync<Exception>(() => failingService.CloseAsync());

        Assert.Equal(1, ctx.Db.DotdBallots.Count());
    }

    [Fact]
    public async Task Verwerfen_loescht_Wahl_und_Ballots()
    {
        using var ctx = new SqliteTestContext();
        SeedLeague(ctx, "pro", "ERC | Wiener");
        var (service, _) = Create(ctx);
        await service.OpenAsync("pro", null);
        await service.RegisterVoteAsync("tw-1", "wiener");

        Assert.True((await service.DiscardAsync()).Ok);
        Assert.Empty(ctx.Db.DotdVotes);
        Assert.Empty(ctx.Db.DotdBallots);
        Assert.False(await service.HasOpenVoteAsync());
    }

    /// <summary>Simuliert einen DB-Fehler genau beim Speichern der geschlossenen Wahl.</summary>
    private sealed class FailOnSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("simulierter DB-Fehler");
    }
}
