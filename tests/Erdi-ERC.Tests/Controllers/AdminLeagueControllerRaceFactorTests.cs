using Erdi_ERC.Controllers;
using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Erdi_ERC.Options;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;
using OptionsFactory = Microsoft.Extensions.Options.Options;
using Harness = Erdi_ERC.Tests.Infrastructure.AdminCommunityTestHarness;

namespace Erdi_ERC.Tests.Controllers;

/// <summary>
/// Rennabbruch im Admin-Formular: Der Punkte-Faktor (100/75/50) muss über alle drei
/// Eintragungswege gespeichert, auf erlaubte Werte normalisiert und beim Löschen/Wieder­
/// herstellen (Undo) mitgeführt werden — sonst verlöre ein wiederhergestelltes Rennen
/// still seinen Abbruch-Status.
/// </summary>
public class AdminLeagueControllerRaceFactorTests
{
    private static AdminLeagueController BuildController(SqliteTestContext ctx) => BuildController(ctx.Db);

    /// <summary>Controller auf einem konkreten Context — für Flows, die in Prod zwei
    /// getrennte Requests (und damit zwei Scopes) sind, z. B. Löschen → Undo.</summary>
    private static AdminLeagueController BuildController(AppDbContext db)
    {
        var cache = new StaticDataCache(db, new MemoryCache(new MemoryCacheOptions()));
        var profileService = new DriverProfileService(db, OptionsFactory.Create(new DriverMatchingOptions()));
        var ctrl = new AdminLeagueController(
            db,
            new Harness.NoopAudit(),
            profileService,
            new Harness.NoopWebhook(),
            new Harness.NoopStats(),
            cache);
        ctrl.TempData = new TempDataDictionary(new DefaultHttpContext(), new Harness.NullTempDataProvider());
        return ctrl;
    }

    private static async Task<SqliteTestContext> SeedLeagueAsync()
    {
        var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = "l1", Name = "L1" });
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l1", Driver = "Alpha" });
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l1", Driver = "Bravo" });
        await ctx.Db.SaveChangesAsync();
        return ctx;
    }

    [Theory]
    [InlineData(50, 50)]
    [InlineData(75, 75)]
    [InlineData(100, 100)]
    public async Task SaveEnteredRace_storesChosenPointsFactor(int input, int expected)
    {
        using var ctx = await SeedLeagueAsync();
        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var result = await ctrl.SaveEnteredRace(
            leagueId: "l1",
            date: new DateTime(2026, 9, 20),
            track: "Monza",
            fastestLapDriver: "Alpha",
            positions: new[] { "Alpha", "Bravo" },
            raceTimes: new[] { "1:30.000", "1:31.000" },
            penaltySeconds: null,
            dnfDrivers: null,
            reserveDrivers: null,
            reserveMainDrivers: null,
            pointsPercent: input);

        Assert.IsType<RedirectToActionResult>(result);

        await using var verify = ctx.NewContext();
        var race = await verify.RaceResults.SingleAsync();
        Assert.Equal(expected, race.PointsPercent);
    }

    [Fact]
    public async Task SaveEnteredRace_manipulatedPointsFactor_fallsBackToFull()
    {
        // Der Parameter kommt aus dem Formular — ein erfundener Wert (z. B. 1 %) darf
        // nicht in die Tabelle gelangen.
        using var ctx = await SeedLeagueAsync();
        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        await ctrl.SaveEnteredRace(
            leagueId: "l1",
            date: new DateTime(2026, 9, 20),
            track: "Monza",
            fastestLapDriver: null,
            positions: new[] { "Alpha", "Bravo" },
            raceTimes: new[] { "1:30.000", "1:31.000" },
            penaltySeconds: null,
            dnfDrivers: null,
            reserveDrivers: null,
            reserveMainDrivers: null,
            pointsPercent: 1);

        await using var verify = ctx.NewContext();
        var race = await verify.RaceResults.SingleAsync();
        Assert.Equal(100, race.PointsPercent);
    }

    [Fact]
    public async Task SaveEnteredRace_withoutPointsFactor_defaultsToFull()
    {
        // Bestehende Aufrufer (und Altdaten) setzen das Feld nicht → voll gewertet.
        using var ctx = await SeedLeagueAsync();
        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        await ctrl.SaveEnteredRace(
            leagueId: "l1",
            date: new DateTime(2026, 9, 20),
            track: "Monza",
            fastestLapDriver: null,
            positions: new[] { "Alpha" },
            raceTimes: new[] { "1:30.000" },
            penaltySeconds: null,
            dnfDrivers: null,
            reserveDrivers: null,
            reserveMainDrivers: null);

        await using var verify = ctx.NewContext();
        Assert.Equal(100, (await verify.RaceResults.SingleAsync()).PointsPercent);
    }

    [Fact]
    public async Task UpdateEnteredRace_changesPointsFactor()
    {
        using var ctx = await SeedLeagueAsync();
        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        await ctrl.SaveEnteredRace(
            leagueId: "l1",
            date: new DateTime(2026, 9, 20),
            track: "Monza",
            fastestLapDriver: null,
            positions: new[] { "Alpha", "Bravo" },
            raceTimes: new[] { "1:30.000", "1:31.000" },
            penaltySeconds: null,
            dnfDrivers: null,
            reserveDrivers: null,
            reserveMainDrivers: null,
            pointsPercent: 50);

        var rowId = (await ctx.NewContext().RaceResults.AsNoTracking().SingleAsync()).RowId;

        await ctrl.UpdateEnteredRace(
            rowId: rowId,
            leagueId: "l1",
            date: new DateTime(2026, 9, 20),
            track: "Monza",
            fastestLapDriver: null,
            positions: new[] { "Alpha", "Bravo" },
            raceTimes: new[] { "1:30.000", "1:31.000" },
            penaltySeconds: null,
            dnfDrivers: null,
            reserveDrivers: null,
            reserveMainDrivers: null,
            pointsPercent: 75);

        await using var verify = ctx.NewContext();
        Assert.Equal(75, (await verify.RaceResults.SingleAsync()).PointsPercent);
    }

    [Fact]
    public async Task SaveRace_quickSave_storesPointsFactor()
    {
        using var ctx = await SeedLeagueAsync();
        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        await ctrl.SaveEnteredRace(
            leagueId: "l1",
            date: new DateTime(2026, 9, 20),
            track: "Monza",
            fastestLapDriver: null,
            positions: new[] { "Alpha" },
            raceTimes: new[] { "1:30.000" },
            penaltySeconds: null,
            dnfDrivers: null,
            reserveDrivers: null,
            reserveMainDrivers: null);

        var rowId = (await ctx.NewContext().RaceResults.AsNoTracking().SingleAsync()).RowId;

        await ctrl.SaveRace(rowId, "l1", new DateTime(2026, 9, 21), "Monza", "Alpha", null, 50);

        await using var verify = ctx.NewContext();
        var race = await verify.RaceResults.SingleAsync();
        Assert.Equal(50, race.PointsPercent);
        Assert.Equal(new DateTime(2026, 9, 21), race.Date);
    }

    [Fact]
    public async Task DeleteAndUndo_roundTrip_keepsPointsFactor()
    {
        // Der Snapshot muss den Faktor mitführen — ein Rennen, das mit 50 % gelöscht und
        // wiederhergestellt wird, muss auch danach 50 % vergeben.
        using var ctx = await SeedLeagueAsync();
        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        await ctrl.SaveEnteredRace(
            leagueId: "l1",
            date: new DateTime(2026, 9, 20),
            track: "Monza",
            fastestLapDriver: "Alpha",
            positions: new[] { "Alpha", "Bravo" },
            raceTimes: new[] { "1:30.000", "1:31.000" },
            penaltySeconds: null,
            dnfDrivers: null,
            reserveDrivers: null,
            reserveMainDrivers: null,
            pointsPercent: 50);

        var rowId = (await ctx.NewContext().RaceResults.AsNoTracking().SingleAsync()).RowId;

        // Löschen und Undo laufen in Prod als je eigener Request mit eigenem Scope.
        // Hier genauso — sonst kollidieren die im Schreib-Context noch getrackten
        // Finish-Zeilen mit den Entitäten desselben Primärschlüssels aus dem Import.
        using var deleteDb = ctx.NewContext();
        var deleteCtrl = BuildController(deleteDb);
        TestAuthHelper.AttachContext(deleteCtrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));
        await deleteCtrl.DeleteRace(rowId, "l1");

        await using (var afterDelete = ctx.NewContext())
        {
            Assert.Empty(afterDelete.RaceResults);
            Assert.Equal(1, await afterDelete.RaceUndoEntries.CountAsync());
        }

        var undoId = (await ctx.NewContext().RaceUndoEntries.AsNoTracking().SingleAsync()).Id;

        // Der Undo läuft in Prod als eigener Request mit eigenem Scope — hier genauso,
        // sonst kollidieren die noch getrackten, gelöschten Finish-Zeilen mit den
        // wiederhergestellten.
        using var undoDb = ctx.NewContext();
        var undoCtrl = BuildController(undoDb);
        TestAuthHelper.AttachContext(undoCtrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));
        await undoCtrl.UndoDeleteRace(undoId);

        await using var verify = ctx.NewContext();
        var restored = await verify.RaceResults.SingleAsync();
        Assert.Equal(50, restored.PointsPercent);
        Assert.Equal("Monza", restored.Track);
        Assert.Equal(2, await verify.RaceFinishes.CountAsync());
    }

    [Fact]
    public async Task UndoDeleteRace_legacySnapshotWithoutFactor_restoresAsFull()
    {
        // Snapshots von vor diesem Feature kennen das Feld nicht → Default 100,
        // niemals 0 (was alle Punkte des Rennens gelöscht hätte).
        using var ctx = await SeedLeagueAsync();
        var ctrl = BuildController(ctx);
        TestAuthHelper.AttachContext(ctrl, TestAuthHelper.CreateAdminContext("admin1", "AdminUser"));

        var legacyJson = """
            {
              "Race": { "RowId": 7, "LeagueId": "l1", "Date": "2026-09-20T00:00:00",
                        "Track": "Monza", "Winner": "Alpha", "FastestLap": "Alpha", "Season": "2026" },
              "Finishes": [ { "Driver": "Alpha", "Position": 1 } ]
            }
            """;
        ctx.Db.RaceUndoEntries.Add(new RaceUndoEntry
        {
            LeagueId = "l1",
            OriginalRaceId = 7,
            Actor = "admin1",
            PayloadJson = legacyJson,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.Db.SaveChangesAsync();
        var undoId = (await ctx.NewContext().RaceUndoEntries.AsNoTracking().SingleAsync()).Id;

        await ctrl.UndoDeleteRace(undoId);

        await using var verify = ctx.NewContext();
        Assert.Equal(100, (await verify.RaceResults.SingleAsync()).PointsPercent);
    }
}
