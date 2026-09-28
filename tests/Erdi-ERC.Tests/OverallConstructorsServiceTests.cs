using Erdi_ERC.Helpers;
using Erdi_ERC.Models;
using Erdi_ERC.Options;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace Erdi_ERC.Tests;

/// <summary>
/// Deckt die Liga-übergreifende Constructors-Meisterschaft ab: Cross-League-Aggregation
/// per <c>F1Team.CssKey</c>, Opt-in via <see cref="League.CountsTowardOverall"/>,
/// Bugfix für <see cref="RaceTeamHelper.ComputeTeamPointsForLeague"/> mit
/// konfigurierter <see cref="F1ScoringOptions.PointMap"/> und Edge-Cases
/// (leere Ligen, Cross-Team-Fahrer, Legacy-Aliase).
/// </summary>
public class OverallConstructorsServiceTests
{
    private static IOptions<F1ScoringOptions> F1Scoring() => Microsoft.Extensions.Options.Options.Create(new F1ScoringOptions());

    /// <summary>Frischer Cache pro Service-Instanz — verhindert, dass gecachte ComputeAsync-Ergebnisse zwischen Tests leaken.</summary>
    private static IMemoryCache NewCache() => new MemoryCache(new MemoryCacheOptions());

    // ---- F1TeamsHelper: kanonischer Schlüssel -------------------------------------------------

    [Fact]
    public void F1TeamsHelper_GetTeamByName_recognizesOfficialName()
    {
        var team = F1TeamsHelper.GetTeamByName("Mercedes");
        Assert.NotNull(team);
        Assert.Equal("mercedes", team!.CssKey);
    }

    [Fact]
    public void F1TeamsHelper_GetTeamByName_normalizesSauberToAudi()
    {
        var sauber = F1TeamsHelper.GetTeamByName("Sauber");
        var audi = F1TeamsHelper.GetTeamByName("Audi");
        Assert.NotNull(sauber);
        Assert.NotNull(audi);
        Assert.Equal(audi!.CssKey, sauber!.CssKey);
    }

    [Fact]
    public void F1TeamsHelper_GetTeamByName_normalizesRbToRacingBulls()
    {
        var rb = F1TeamsHelper.GetTeamByName("RB");
        var racingBulls = F1TeamsHelper.GetTeamByName("Racing Bulls");
        Assert.NotNull(rb);
        Assert.NotNull(racingBulls);
        Assert.Equal(racingBulls!.CssKey, rb!.CssKey);
    }

    // ---- RaceTeamHelper: Bugfix + Reserve-Logik ---------------------------------------------

    [Fact]
    public void RaceTeamHelper_ComputeTeamPointsForLeague_usesInjectedPointMap()
    {
        var ctx = new SqliteTestContext();
        AddLeagueWithRace(ctx, "pro", "Alpha", "Mercedes",
            ("Alpha", 1), ("Beta", 2), ("Gamma", 3));

        int[] customMap = { 1, 0, 0 };
        var league = ctx.Db.Leagues
            .Include(l => l.Races).ThenInclude(r => r.Finishes)
            .Include(l => l.Standings)
            .Single();
        var points = RaceTeamHelper.ComputeTeamPointsForLeague(league, "Mercedes", customMap);

        Assert.Equal(1, points);
    }

    [Fact]
    public void RaceTeamHelper_ComputeTeamPointsForLeague_legacyOverloadStillWorks()
    {
        var ctx = new SqliteTestContext();
        AddLeagueWithRace(ctx, "pro", "Alpha", "Mercedes", ("Alpha", 1));
        var league = ctx.Db.Leagues
            .Include(l => l.Races).ThenInclude(r => r.Finishes)
            .Include(l => l.Standings)
            .Single();

        Assert.Equal(25, RaceTeamHelper.ComputeTeamPointsForLeague(league, "Mercedes"));
    }

    [Fact]
    public void RaceTeamHelper_ComputeTeamPointsForLeague_appliesRaceFactor()
    {
        // Abgebrochenes Rennen (50 %): das Team erbt den halbierten Fahrer-Anteil.
        var ctx = new SqliteTestContext();
        AddLeagueWithRace(ctx, "pro", "Alpha", "Mercedes", ("Alpha", 1));
        SetRaceFactor(ctx, "pro", 50);

        var league = ctx.Db.Leagues
            .Include(l => l.Races).ThenInclude(r => r.Finishes)
            .Include(l => l.Standings)
            .Single();

        Assert.Equal(12.5m, RaceTeamHelper.ComputeTeamPointsForLeague(league, "Mercedes"));
    }

    // ---- OverallConstructorsService ---------------------------------------------------------

    [Fact]
    public async Task ComputeAsync_aggregatesAcrossLeagues_usingCssKey()
    {
        var ctx = new SqliteTestContext();
        AddLeagueWithRace(ctx, "pro", "Alpha", "Mercedes", ("Alpha", 1));
        AddLeagueWithRace(ctx, "am",  "Gamma", "mercedes", ("Gamma", 2));

        var service = new OverallConstructorsService(ctx.Db, NewCache(), F1Scoring());
        var rows = await service.ComputeAsync();

        var mercedes = Assert.Single(rows);
        Assert.Equal("Mercedes", mercedes.DisplayName);
        Assert.Equal("mercedes", mercedes.CssKey);
        Assert.Equal(46, mercedes.Points);     // 25 (P1 pro) + 21 (P2 am)
        Assert.Equal(1, mercedes.Wins);
        Assert.Equal(2, mercedes.LeaguesRaced);
        Assert.Equal(2, mercedes.PerLeague.Count);
    }

    [Fact]
    public async Task ComputeAsync_normalizesLegacyAlias()
    {
        var ctx = new SqliteTestContext();
        AddLeagueWithRace(ctx, "pro", "Alpha", "Sauber", ("Alpha", 1));
        AddLeagueWithRace(ctx, "am",  "Beta", "Audi",  ("Beta", 1));

        var service = new OverallConstructorsService(ctx.Db, NewCache(), F1Scoring());
        var rows = await service.ComputeAsync();

        var row = Assert.Single(rows);
        Assert.Equal("audi", row.CssKey);
        Assert.Equal(50, row.Points);
        Assert.Equal(2, row.Wins);
    }

    [Fact]
    public async Task ComputeAsync_excludesLeaguesWithoutOptIn()
    {
        var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "pro", CountsTowardOverall = true });
        ctx.Db.Leagues.Add(new League { Id = "fun", CountsTowardOverall = false });
        ctx.Db.SaveChanges();

        AddLeagueWithRace(ctx, "pro", "Alpha", "Mercedes", ("Alpha", 1));
        AddLeagueWithRace(ctx, "fun", "Bob",   "Ferrari",  ("Bob",  1));

        var service = new OverallConstructorsService(ctx.Db, NewCache(), F1Scoring());
        var rows = await service.ComputeAsync();

        Assert.Single(rows);
        Assert.Equal("Mercedes", rows[0].DisplayName);
    }

    [Fact]
    public async Task ComputeAsync_handlesEmptyLeagueList()
    {
        var ctx = new SqliteTestContext();
        var service = new OverallConstructorsService(ctx.Db, NewCache(), F1Scoring());

        Assert.Empty(await service.ComputeAsync());
    }

    [Fact]
    public async Task ComputeAsync_handlesLeaguesWithoutRaces()
    {
        var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "pro" });
        ctx.Db.SaveChanges();

        var service = new OverallConstructorsService(ctx.Db, NewCache(), F1Scoring());
        Assert.Empty(await service.ComputeAsync());
    }

    [Fact]
    public async Task ComputeAsync_ranksByPointsThenDisplayName()
    {
        var ctx = new SqliteTestContext();
        AddLeagueWithRace(ctx, "pro", "Alpha", "Mercedes", ("Alpha", 1));
        AddLeagueWithRace(ctx, "am",  "Gamma", "Ferrari",  ("Gamma", 1));

        var service = new OverallConstructorsService(ctx.Db, NewCache(), F1Scoring());
        var rows = await service.ComputeAsync();

        Assert.Equal(2, rows.Count);
        Assert.Equal(1, rows[0].Position);
        Assert.Equal(2, rows[1].Position);
        Assert.Equal("Ferrari", rows[0].DisplayName);
        Assert.Equal("Mercedes", rows[1].DisplayName);
    }

    [Fact]
    public async Task ComputeAsync_attributesPointsToTeamRacedFor()
    {
        // Fahrer "Bob" fährt in Liga "am" als Stammfahrer für Mercedes (P1). Die
        // Punkte sollen für Mercedes gezählt werden, nicht für irgendein Heimteam
        // aus einer anderen Liga — das ist die "Punkte dem Team des Rennens"-Regel.
        var ctx = new SqliteTestContext();
        AddLeagueWithRace(ctx, "am", "Bob", "Mercedes", ("Bob", 1));

        var service = new OverallConstructorsService(ctx.Db, NewCache(), F1Scoring());
        var rows = await service.ComputeAsync();

        var mercedes = rows.Single();
        Assert.Equal("Mercedes", mercedes.DisplayName);
        Assert.Equal(25, mercedes.Points);
        Assert.Equal(1, mercedes.Wins);
    }

    [Fact]
    public async Task ComputeAsync_honorsConfiguredF1PointMap()
    {
        // Wenn der Betreiber in F1ScoringOptions eine kleinere Skala (z.B. Top-3)
        // konfiguriert, muss die Aggregation entsprechend niedrigere Punkte liefern.
        var ctx = new SqliteTestContext();
        AddLeagueWithRace(ctx, "pro", "Alpha", "Mercedes", ("Alpha", 1));
        // Zweiter punktender Fahrer mit eigenem Team in derselben Liga: er darf Mercedes
        // nichts geben und prüft zugleich die untere Map-Stufe (P2 → 6).
        AddLeagueWithRace(ctx, "pro", "Beta", "Ferrari", ("Beta", 2));

        var customScoring = Microsoft.Extensions.Options.Options.Create(new F1ScoringOptions
        {
            PointMap = new[] { 10, 6, 4, 2 } // nur Top-4 kassiert
        });
        var service = new OverallConstructorsService(ctx.Db, NewCache(), customScoring);
        var rows = await service.ComputeAsync();

        var mercedes = rows.Single(r => r.CssKey == "mercedes");
        Assert.Equal(10, mercedes.Points); // nur P1 (10) — Beta P2 (6) gehört zu Ferrari
        Assert.Equal(1, mercedes.Wins);
        Assert.Equal(1, mercedes.Events);
        Assert.Equal(6, rows.Single(r => r.CssKey == "ferrari").Points);
    }

    [Fact]
    public async Task ComputeAsync_handlesDnfAndDnsFinishes()
    {
        // Gemischtes Rennen: ein gültiger P1, ein DNF (Position=0), ein DNS (Position=-1).
        // DNF und DNS dürfen weder Punkte noch Events noch Wins berühren.
        var ctx = new SqliteTestContext();
        AddLeagueWithRace(ctx, "pro", "Alpha", "Mercedes",
            ("Alpha", 1),
            ("Beta", 0),   // DNF
            ("Gamma", -1)  // DNS
        );

        var service = new OverallConstructorsService(ctx.Db, NewCache(), F1Scoring());
        var rows = await service.ComputeAsync();

        var mercedes = rows.Single();
        Assert.Equal(25, mercedes.Points); // nur P1 zählt
        Assert.Equal(1, mercedes.Wins);
        Assert.Equal(1, mercedes.Events);   // DNF/DNS werden nicht als Event gezählt
        Assert.Equal(1, mercedes.BestPosition);
    }

    [Fact]
    public async Task ComputeAsync_attributesPointsToGuestTeam_AcrossLeagues()
    {
        // Bob ist in Liga "am" für Mercedes gemeldet, in Liga "pro" aber als Gaststarter
        // für Ferrari angetreten (per RaceReserveAssignment oder Standings-Override). Die
        // Punkte aus "pro" müssen für Ferrari gezählt werden, nicht für Mercedes — die
        // "Punkte dem Team des Rennens"-Regel über alle Ligen hinweg.
        var ctx = new SqliteTestContext();
        AddLeagueWithRace(ctx, "am", "Bob", "Mercedes", ("Bob", 1));          // 25 Pkt. für Mercedes
        AddLeagueWithRace(ctx, "pro", "Bob", "Ferrari", ("Bob", 1));         // 25 Pkt. für Ferrari

        var service = new OverallConstructorsService(ctx.Db, NewCache(), F1Scoring());
        var rows = await service.ComputeAsync();

        Assert.Equal(2, rows.Count);
        var mercedes = rows.Single(r => r.CssKey == "mercedes");
        var ferrari  = rows.Single(r => r.CssKey == "ferrari");
        Assert.Equal(25, mercedes.Points);
        Assert.Equal(25, ferrari.Points);
        Assert.Equal(1, mercedes.LeaguesRaced);
        Assert.Equal(1, ferrari.LeaguesRaced);
    }

    // ---- „Ohne Team"-Bucket: gleiche Regel wie Ligaseite und Overlay-Export ----------------

    [Fact]
    public async Task ComputeAsync_teamlessDriverWithPoints_countsTowardsNoTeamRow()
    {
        // Fahrer ohne Team im Kader wird P2 → 21 Punkte. Diese Punkte wurden hier früher
        // verworfen, während die Ligaseite sie im „Ohne Team"-Bucket mitzählte — dieselben
        // Rennen, zwei Summen. Jetzt trägt der Bucket sie hier genauso.
        var ctx = new SqliteTestContext();
        AddLeagueWithRace(ctx, "pro", "Teamloser", string.Empty, ("Teamloser", 2));

        var service = new OverallConstructorsService(ctx.Db, NewCache(), F1Scoring());
        var rows = await service.ComputeAsync();

        var noTeam = Assert.Single(rows);
        Assert.Equal("Ohne Team", noTeam.DisplayName);
        Assert.Equal("no-team", noTeam.CssKey); // eigener Schlüssel, landet in HTML-Ids der Tabelle
        Assert.Equal(21, noTeam.Points);
    }

    [Fact]
    public async Task ComputeAsync_unknownTeamName_doesNotMergeIntoNoTeamBucket()
    {
        // „Phantom Racing" steht nicht in F1TeamsHelper und landet deshalb auf dem
        // Fallback-Schlüssel "unknown". Der „Ohne Team"-Bucket darf nicht dort hineinlaufen,
        // sonst würden die Punkte zweier verschiedener Konstrukteure addiert.
        var ctx = new SqliteTestContext();
        AddLeagueWithRace(ctx, "pro", "Phantomfahrer", "Phantom Racing",
            ("Phantomfahrer", 1), ("Teamloser", 2));

        var service = new OverallConstructorsService(ctx.Db, NewCache(), F1Scoring());
        var rows = await service.ComputeAsync();

        Assert.Equal(2, rows.Count);
        Assert.Equal("Phantom Racing", rows.Single(r => r.CssKey == "unknown").DisplayName);
        Assert.Equal(25, rows.Single(r => r.CssKey == "unknown").Points);
        Assert.Equal(21, rows.Single(r => r.CssKey == "no-team").Points);
    }

    [Fact]
    public async Task ComputeAsync_scorelessFinishesWithoutTeam_createNoRow()
    {
        // P17 liegt außerhalb der Punkteränge (die Map punktet bis P15) — ein Bucket ohne
        // Punkte darf die Tabelle nicht füllen (ConstructorTeamHelper.IsVisibleConstructor).
        var ctx = new SqliteTestContext();
        AddLeagueWithRace(ctx, "pro", "Alpha", "Mercedes", ("Alpha", 1), ("Gast", 17));

        var service = new OverallConstructorsService(ctx.Db, NewCache(), F1Scoring());
        var rows = await service.ComputeAsync();

        Assert.Equal("Mercedes", Assert.Single(rows).DisplayName);
    }

    [Fact]
    public async Task ComputeAsync_appliesRaceFactorPerLeague()
    {
        // Liga "pro" wurde abgebrochen (50 % → 12,5), Liga "am" lief regulär (P2 → 21).
        // Die Konstrukteurswertung muss mit der Fahrertabelle übereinstimmen.
        var ctx = new SqliteTestContext();
        AddLeagueWithRace(ctx, "pro", "Alpha", "Mercedes", ("Alpha", 1));
        AddLeagueWithRace(ctx, "am", "Gamma", "mercedes", ("Gamma", 2));
        SetRaceFactor(ctx, "pro", 50);

        var service = new OverallConstructorsService(ctx.Db, NewCache(), F1Scoring());
        var rows = await service.ComputeAsync();

        var mercedes = Assert.Single(rows);
        Assert.Equal(33.5m, mercedes.Points); // 12,5 + 21
        Assert.Equal("mercedes", mercedes.CssKey);
    }

    // ---- Helpers ----------------------------------------------------------------------

    private static void AddLeagueWithRace(
        SqliteTestContext ctx, string leagueId, string? driver, string? team,
        params (string Driver, int Position)[] finishes)
    {
        if (!ctx.Db.Leagues.Any(l => l.Id == leagueId))
        {
            ctx.Db.Leagues.Add(new League { Id = leagueId });
        }
        if (!string.IsNullOrWhiteSpace(driver))
        {
            ctx.Db.DriverStandings.Add(new DriverStanding
            {
                LeagueId = leagueId,
                Driver = driver,
                Team = team ?? string.Empty
            });
        }
        ctx.Db.SaveChanges();

        var race = new RaceResult { LeagueId = leagueId, Date = DateTime.UtcNow, Track = "Test" };
        ctx.Db.RaceResults.Add(race);
        ctx.Db.SaveChanges();
        foreach (var (d, p) in finishes)
        {
            ctx.Db.RaceFinishes.Add(new RaceFinish { RaceResultId = race.RowId, Driver = d, Position = p });
        }
        ctx.Db.SaveChanges();
    }

    /// <summary>Setzt den Punkte-Faktor (Rennabbruch) auf alle Rennen einer Liga.
    /// <c>ExecuteUpdate</c>, weil der Test-Kontext (wie die App) NoTracking-Default hat —
    /// eine Mutation an einer gelesenen Entität würde sonst still verpuffen.</summary>
    private static void SetRaceFactor(SqliteTestContext ctx, string leagueId, int pointsPercent)
    {
        ctx.Db.RaceResults
            .Where(r => r.LeagueId == leagueId)
            .ExecuteUpdate(s => s.SetProperty(r => r.PointsPercent, pointsPercent));
    }
}
