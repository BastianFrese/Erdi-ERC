using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erdi_ERC.Tests;

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

        var service = new StatsService(ctx.Db, Microsoft.Extensions.Options.Options.Create(new Erdi_ERC.Options.F1ScoringOptions()));

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

        var service = new StatsService(ctx.Db, Microsoft.Extensions.Options.Options.Create(new Erdi_ERC.Options.F1ScoringOptions()));

        // Act
        await service.RebuildLeagueStandingsAsync("l1");

        // Assert
        await using var verify = ctx.NewContext();
        var bonus = await verify.DriverStandings.SingleAsync(s => s.Driver == "Bonus");
        Assert.Equal(42, bonus.Points);
    }

    [Fact]
    public async Task RebuildLeagueStandings_keepsCrossLeagueGuestOutOfStandings_withoutAssignment()
    {
        // Arrange — "Guest" faehrt ein Rennen in Liga l1, hat dort aber keine Stammwertung
        // und keine RaceGuestAssignment. Mit dem Cross-League-Gate soll er NICHT in den
        // Liga-Standings erscheinen.
        using var ctx = new SqliteTestContext();
        await SeedLeagueAsync(ctx, "l1");
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l1", Driver = "Stamm" });
        await ctx.Db.SaveChangesAsync();
        await AddRaceAsync(ctx, "l1", ("Stamm", 2), ("Guest", 1)); // Guest P1 = 25, Stamm P2 = 21

        var service = new StatsService(ctx.Db, Microsoft.Extensions.Options.Options.Create(new Erdi_ERC.Options.F1ScoringOptions()));

        // Act
        await service.RebuildLeagueStandingsAsync("l1");

        // Assert — Gast wird NICHT als Liga-Standing angelegt. Nur "Stamm" fuehrt die Tabelle an.
        await using var verify = ctx.NewContext();
        var guest = await verify.DriverStandings.SingleOrDefaultAsync(s => s.LeagueId == "l1" && s.Driver == "Guest");
        Assert.Null(guest);

        var stamm = await verify.DriverStandings.SingleAsync(s => s.Driver == "Stamm");
        Assert.Equal(21, stamm.Points);
        Assert.Equal(1, stamm.Position); // Stamm fuehrt jetzt die Tabelle an
    }

    [Fact]
    public async Task RebuildLeagueStandings_crossLeagueGuestWithMainAssignment_doesNotCreateStanding_butMainInheritsTeam()
    {
        // Arrange — Cross-League-Guest ist einem Liga-Hauptfahrer zugeordnet.
        // Er bleibt aus der Liga-Bestenliste raus, aber RaceTeamHelper liefert das Team.
        using var ctx = new SqliteTestContext();
        await SeedLeagueAsync(ctx, "l1");
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l1", Driver = "Stamm", Team = "Ferrari" });
        await ctx.Db.SaveChangesAsync();

        var race = new RaceResult { LeagueId = "l1", Date = DateTime.UtcNow, Track = "Test" };
        ctx.Db.RaceResults.Add(race);
        await ctx.Db.SaveChangesAsync();
        ctx.Db.RaceFinishes.Add(new RaceFinish { RaceResultId = race.RowId, Driver = "Stamm", Position = 2 });
        ctx.Db.RaceFinishes.Add(new RaceFinish { RaceResultId = race.RowId, Driver = "Guest", Position = 1 });
        ctx.Db.RaceGuestAssignments.Add(new RaceGuestAssignment { RaceResultId = race.RowId, GuestDriver = "Guest", MainDriver = "Stamm" });
        await ctx.Db.SaveChangesAsync();

        var service = new StatsService(ctx.Db, Microsoft.Extensions.Options.Options.Create(new Erdi_ERC.Options.F1ScoringOptions()));

        // Act
        await service.RebuildLeagueStandingsAsync("l1");

        // Assert — kein Standing fuer Guest.
        await using var verify = ctx.NewContext();
        var guest = await verify.DriverStandings.SingleOrDefaultAsync(s => s.Driver == "Guest");
        Assert.Null(guest);
    }

    [Fact]
    public async Task RebuildLeagueStandings_stewardingPenaltyDoesNotSubtractFromDriverPoints()
    {
        // Regression (User-Report 2026-08-15): Stewarding-Penalty-Points duerfen NICHT
        // von der Gesamtpunktzahl des Fahrers abgezogen werden. Stewarding-Berichte sind
        // eigenstaendige Dokumente; das "Points"-Feld im Bericht ist informativ.
        // Korrekturen muessen explizit ueber DriverStanding.PointsAdjustment laufen.
        using var ctx = new SqliteTestContext();
        await SeedLeagueAsync(ctx, "l1");
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l1", Driver = "Alpha" });
        await ctx.Db.SaveChangesAsync();
        await AddRaceAsync(ctx, "l1", ("Alpha", 1)); // P1 = 25 Punkte

        // Mehrere Strafen verschiedener Typen — keine darf abziehen.
        ctx.Db.LeaguePenalties.Add(new LeaguePenalty
        {
            LeagueId = "l1", Driver = "Alpha",
            PenaltyType = "Punkteabzug", Points = 5,
            Date = DateTime.UtcNow, Reason = "Kollision"
        });
        ctx.Db.LeaguePenalties.Add(new LeaguePenalty
        {
            LeagueId = "l1", Driver = "Alpha",
            PenaltyType = "Punkteabzug", Points = 3,
            Date = DateTime.UtcNow, Reason = "Track-Limits"
        });
        ctx.Db.LeaguePenalties.Add(new LeaguePenalty
        {
            LeagueId = "l1", Driver = "Alpha",
            PenaltyType = "Zeitstrafe", Points = 10,
            Date = DateTime.UtcNow, Reason = "Verwarnung"
        });
        await ctx.Db.SaveChangesAsync();

        var service = new StatsService(ctx.Db, Microsoft.Extensions.Options.Options.Create(new Erdi_ERC.Options.F1ScoringOptions()));

        // Act
        await service.RebuildLeagueStandingsAsync("l1");

        // Assert — Penalty-Summe (5+3+10) bleibt unberuecksichtigt.
        await using var verify = ctx.NewContext();
        var alpha = await verify.DriverStandings.SingleAsync(s => s.Driver == "Alpha");
        Assert.Equal(25, alpha.Points); // nur Renn-Punkte, kein Penalty-Abzug
    }

    [Fact]
    public async Task RebuildLeagueStandings_equalPoints_moreBetterPositionsGetsHigherPosition()
    {
        // F1-Tiebreaker: Alice (P1+P6 = 25+12 = 37) und Bob (P2+P4 = 21+16 = 37)
        // haben dieselbe Punktzahl, aber Alice hat die bessere Position (1× P1).
        // Sie muss Position 1 bekommen, Bob Position 2.
        using var ctx = new SqliteTestContext();
        await SeedLeagueAsync(ctx, "l1");
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l1", Driver = "Alice" });
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l1", Driver = "Bob" });
        await ctx.Db.SaveChangesAsync();
        await AddRaceAsync(ctx, "l1", ("Alice", 1), ("Bob", 2));
        await AddRaceAsync(ctx, "l1", ("Alice", 6), ("Bob", 4));

        var service = new StatsService(ctx.Db, Microsoft.Extensions.Options.Options.Create(new Erdi_ERC.Options.F1ScoringOptions()));

        // Act
        await service.RebuildLeagueStandingsAsync("l1");

        // Assert — gleiche Punkte, aber Alice (mehr P1) führt.
        await using var verify = ctx.NewContext();
        var alice = await verify.DriverStandings.SingleAsync(s => s.Driver == "Alice");
        var bob = await verify.DriverStandings.SingleAsync(s => s.Driver == "Bob");
        Assert.Equal(37, alice.Points);
        Assert.Equal(37, bob.Points);
        Assert.Equal(1, alice.Position);
        Assert.Equal(2, bob.Position);
    }

    // ── Rennabbruch: Punkte-Faktor (50 % / 75 %) ────────────────────────────────

    /// <summary>Rennen mit explizitem Punkte-Faktor (Rennabbruch) anlegen.</summary>
    private static async Task AddRaceWithFactorAsync(
        SqliteTestContext ctx, string leagueId, int pointsPercent, params (string Driver, int Position)[] finishes)
    {
        var race = new RaceResult
        {
            LeagueId = leagueId,
            Date = DateTime.UtcNow,
            Track = "Testbahn",
            PointsPercent = pointsPercent
        };
        ctx.Db.RaceResults.Add(race);
        await ctx.Db.SaveChangesAsync();

        foreach (var (driver, position) in finishes)
        {
            ctx.Db.RaceFinishes.Add(new RaceFinish { RaceResultId = race.RowId, Driver = driver, Position = position });
        }
        await ctx.Db.SaveChangesAsync();
    }

    [Theory]
    [InlineData(50, 12.5)]   // 50 % aus P1 (25)
    [InlineData(75, 18.75)]  // 75 % aus P1 (25)
    [InlineData(100, 25)]    // regulär
    public async Task RebuildLeagueStandings_appliesRaceFactorToPoints(int pointsPercent, decimal expected)
    {
        // Arrange — abgebrochenes Rennen: Anteil der Grundpunkte, bewusst ohne Rundung
        // (12,5 / 18,75 wie in der echten F1).
        using var ctx = new SqliteTestContext();
        await SeedLeagueAsync(ctx, "l1");
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l1", Driver = "Alpha" });
        await ctx.Db.SaveChangesAsync();
        await AddRaceWithFactorAsync(ctx, "l1", pointsPercent, ("Alpha", 1));

        var service = new StatsService(ctx.Db, Microsoft.Extensions.Options.Options.Create(new Erdi_ERC.Options.F1ScoringOptions()));

        // Act
        await service.RebuildLeagueStandingsAsync("l1");

        // Assert
        await using var verify = ctx.NewContext();
        var alpha = await verify.DriverStandings.SingleAsync(s => s.Driver == "Alpha");
        Assert.Equal(expected, alpha.Points);
    }

    [Fact]
    public async Task RebuildLeagueStandings_mixedFactors_countsEachRaceWithItsOwnFactor()
    {
        // Arrange — einmal abgebrochen (50 % → 12,5), einmal regulär (P2 → 21).
        // Beweist, dass der Faktor pro Rennen greift und nicht saisonweit.
        using var ctx = new SqliteTestContext();
        await SeedLeagueAsync(ctx, "l1");
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l1", Driver = "Alpha", PointsAdjustment = -0.5m });
        await ctx.Db.SaveChangesAsync();
        await AddRaceWithFactorAsync(ctx, "l1", 50, ("Alpha", 1));
        await AddRaceWithFactorAsync(ctx, "l1", 100, ("Alpha", 2));

        var service = new StatsService(ctx.Db, Microsoft.Extensions.Options.Options.Create(new Erdi_ERC.Options.F1ScoringOptions()));

        // Act
        await service.RebuildLeagueStandingsAsync("l1");

        // Assert — 12,5 + 21 − 0,5 Korrektur; die Korrektur bleibt dezimal erhalten.
        await using var verify = ctx.NewContext();
        var alpha = await verify.DriverStandings.SingleAsync(s => s.Driver == "Alpha");
        Assert.Equal(33m, alpha.Points);
    }

    [Fact]
    public async Task RebuildLeagueStandings_abortedRace_passesHalvedReservePointsToMain()
    {
        // Arrange — Reserve fährt ein Rennen mit 50 %: der Anteil (12,5) muss in
        // ReservePointsForMain landen, nicht die vollen 25.
        using var ctx = new SqliteTestContext();
        await SeedLeagueAsync(ctx, "l1");
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l1", Driver = "Stamm" });
        ctx.Db.DriverStandings.Add(new DriverStanding
        {
            LeagueId = "l1", Driver = "Reserve", IsReserveDriver = true, ReserveForDriver = "Stamm"
        });
        await ctx.Db.SaveChangesAsync();
        await AddRaceWithFactorAsync(ctx, "l1", 50, ("Reserve", 1));

        var service = new StatsService(ctx.Db, Microsoft.Extensions.Options.Options.Create(new Erdi_ERC.Options.F1ScoringOptions()));

        // Act
        await service.RebuildLeagueStandingsAsync("l1");

        // Assert
        await using var verify = ctx.NewContext();
        var reserve = await verify.DriverStandings.SingleAsync(s => s.Driver == "Reserve");
        Assert.Equal(1, reserve.ReserveStarts);
        Assert.Equal(12.5m, reserve.ReservePointsForMain);
    }

    [Fact]
    public async Task RebuildLeagueStandings_dropWorst_appliesFactorBeforeDroppingWorstResult()
    {
        // Arrange — Streichresultat-Reihenfolge: der Faktor muss VOR dem Sortieren
        // greifen. Sonst würde das halbierte Rennen als "bestes" gezählt und
        // stattdessen das 21-Punkte-Rennen gestrichen.
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "l1", Name = "l1", DropWorstResults = 1 });
        await ctx.Db.SaveChangesAsync();
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l1", Driver = "Alpha" });
        await ctx.Db.SaveChangesAsync();

        await AddRaceWithFactorAsync(ctx, "l1", 100, ("Alpha", 1)); // 25
        await AddRaceWithFactorAsync(ctx, "l1", 100, ("Alpha", 2)); // 21
        await AddRaceWithFactorAsync(ctx, "l1", 50, ("Alpha", 1));  // 12,5 ← Streicher

        var service = new StatsService(ctx.Db, Microsoft.Extensions.Options.Options.Create(new Erdi_ERC.Options.F1ScoringOptions()));

        // Act
        await service.RebuildLeagueStandingsAsync("l1");

        // Assert — 25 + 21, das halbierte Rennen fällt raus.
        await using var verify = ctx.NewContext();
        var alpha = await verify.DriverStandings.SingleAsync(s => s.Driver == "Alpha");
        Assert.Equal(46m, alpha.Points);
    }
}
