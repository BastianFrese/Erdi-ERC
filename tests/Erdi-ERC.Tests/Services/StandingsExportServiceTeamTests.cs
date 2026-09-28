using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Erdi_ERC.Options;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Erdi_ERC.Tests.Services;

/// <summary>
/// Teamwertung des Overlay-Exports (GET /api/telemetry/standings). Der Export spiegelt
/// laut eigenem Kommentar „exakt dieselbe Berechnung wie die LeagueResults-Seite" — die
/// Behandlung des „Ohne Team"-Buckets muss deshalb identisch sein:
/// keine Zeile ohne Punkte, aber auch kein stiller Punktverlust, wenn dort Punkte liegen.
/// </summary>
public class StandingsExportServiceTeamTests
{
    private static StandingsExportService BuildService(SqliteTestContext ctx) =>
        new(ctx.Db, Microsoft.Extensions.Options.Options.Create(new F1ScoringOptions()));

    /// <summary>
    /// Fahrer A fährt für „Team X" und gewinnt (25). Fahrer B hat KEIN Team und wird P17 —
    /// außerhalb der Punkteränge; die konfigurierte Map punktet bis P15, P12 brächte also
    /// noch 4 Punkte und wäre kein „0-Punkte"-Fall. Fahrer C fährt für „Team Y" und wird P18:
    /// ein echtes Team mit 0 Punkten muss in der Tabelle bleiben.
    /// </summary>
    private static async Task<SqliteTestContext> SeedAsync(params (string Driver, string Team, int Position)[] extraFinishes)
    {
        var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "L1", Name = "TestLiga", SortOrder = 1 });
        ctx.Db.DriverStandings.AddRange(
            new DriverStanding { LeagueId = "L1", Driver = "Fahrer A", Team = "Team X", Points = 25 },
            new DriverStanding { LeagueId = "L1", Driver = "Fahrer B", Team = string.Empty, Points = 0 },
            new DriverStanding { LeagueId = "L1", Driver = "Fahrer C", Team = "Team Y", Points = 0 });
        foreach (var (driver, team, position) in extraFinishes)
        {
            ctx.Db.DriverStandings.Add(new DriverStanding
            {
                LeagueId = "L1", Driver = driver, Team = team, Points = 0
            });
        }

        var race = new RaceResult
        {
            LeagueId = "L1",
            Date = new DateTime(2026, 9, 20, 20, 0, 0),
            Track = "Imola",
            Winner = "Fahrer A",
            FastestLap = "Fahrer A",
            Finishes = new List<RaceFinish>
            {
                new() { Driver = "Fahrer A", Position = 1 },
                new() { Driver = "Fahrer B", Position = 17 },
                new() { Driver = "Fahrer C", Position = 18 },
            }
        };
        foreach (var (driver, _, position) in extraFinishes)
        {
            race.Finishes.Add(new RaceFinish { Driver = driver, Position = position });
        }

        ctx.Db.RaceResults.Add(race);
        await ctx.Db.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task HoleAsync_teamlessDriverWithoutPoints_hasNoNoTeamRow()
    {
        using var ctx = await SeedAsync();

        var export = await BuildService(ctx).HoleAsync();

        var teams = export.Teams.Where(t => t.Liga == "L1").ToList();
        Assert.DoesNotContain(teams, t => t.Team == "Ohne Team");
    }

    [Fact]
    public async Task HoleAsync_realTeamWithoutPoints_staysVisible()
    {
        using var ctx = await SeedAsync();

        var export = await BuildService(ctx).HoleAsync();

        var teams = export.Teams.Where(t => t.Liga == "L1").ToList();
        var teamY = Assert.Single(teams, t => t.Team == "Team Y");
        Assert.Equal(0, teamY.Punkte);
        Assert.Equal(25, Assert.Single(teams, t => t.Team == "Team X").Punkte);
    }

    [Fact]
    public async Task HoleAsync_teamlessDriverWithPoints_keepsNoTeamRow()
    {
        // Fahrer D ohne Team wird P2 → 21 Punkte. Diese Punkte dürfen nicht verschwinden,
        // der Bucket muss also sichtbar bleiben (Signal an den Admin: Stammdaten fehlen).
        using var ctx = await SeedAsync(("Fahrer D", string.Empty, 2));

        var export = await BuildService(ctx).HoleAsync();

        var teams = export.Teams.Where(t => t.Liga == "L1").ToList();
        var noTeam = Assert.Single(teams, t => t.Team == "Ohne Team");
        Assert.Equal(21, noTeam.Punkte);
    }

    [Fact]
    public async Task HoleAsync_scoringGuestWithoutStanding_createsNoTeamRow()
    {
        // Alle Kader-Fahrer haben ein Team; nur ein Gast ohne Standings-Zeile punktet
        // (P2 = 21). Früher kam der Bucket-Name ausschließlich aus leeren Standings-Zeilen,
        // der Gast fiel also still aus der Wertung — obwohl die Gesamtwertung ihn zählt.
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "L1", Name = "TestLiga", SortOrder = 1 });
        ctx.Db.DriverStandings.Add(new DriverStanding
        {
            LeagueId = "L1", Driver = "Fahrer A", Team = "Team X", Points = 25
        });
        ctx.Db.RaceResults.Add(new RaceResult
        {
            LeagueId = "L1",
            Date = new DateTime(2026, 9, 20, 20, 0, 0),
            Track = "Imola",
            Winner = "Fahrer A",
            FastestLap = "Fahrer A",
            Finishes = new List<RaceFinish>
            {
                new() { Driver = "Fahrer A", Position = 1 },
                new() { Driver = "Gast ohne Kader", Position = 2 },
            }
        });
        await ctx.Db.SaveChangesAsync();

        var export = await BuildService(ctx).HoleAsync();

        var teams = export.Teams.Where(t => t.Liga == "L1").ToList();
        Assert.Equal(25, Assert.Single(teams, t => t.Team == "Team X").Punkte);
        Assert.Equal(21, Assert.Single(teams, t => t.Team == "Ohne Team").Punkte);
    }

    [Fact]
    public async Task HoleAsync_abortedRace_scalesTeamPointsByFactor()
    {
        // Abgebrochenes Rennen (50 %): Team X bekommt 12,5 statt 25, der „Ohne Team"-
        // Bucket 10,5 statt 21 — der Overlay-Export muss dieselbe Zahl zeigen wie die
        // Fahrertabelle, sonst widersprechen sich die Anzeigen.
        using var ctx = await SeedAsync();
        // ExecuteUpdate: der Test-Kontext hat (wie die App) NoTracking-Default.
        await ctx.Db.RaceResults.ExecuteUpdateAsync(s => s.SetProperty(r => r.PointsPercent, 50));

        var export = await BuildService(ctx).HoleAsync();

        var teams = export.Teams.Where(t => t.Liga == "L1").ToList();
        Assert.Equal(12.5m, Assert.Single(teams, t => t.Team == "Team X").Punkte);
    }
}
