using Erdi_ERC.Controllers;
using Erdi_ERC.Models;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Xunit;

namespace Erdi_ERC.Tests.Controllers;

/// <summary>
/// Sammel-Abgabe auf /awards: die Seite sendet drei gleich lange Listen
/// (awardId, leagueId, driver). Die Regeln, die hier festgeschrieben sind:
/// ein leeres Select löscht keine bestehende Stimme, ungültige Zeilen fallen
/// einzeln raus (die gültigen werden trotzdem gespeichert), und Umentscheiden
/// überschreibt statt zu duplizieren.
/// </summary>
public class AwardsControllerBatchVoteTests
{
    private const string LeagueId = "f1";

    private static (AwardsController Ctrl, SqliteTestContext Ctx) Build()
    {
        var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League { Id = LeagueId, Name = "F1" });
        ctx.Db.DriverStandings.AddRange(
            new DriverStanding { LeagueId = LeagueId, Driver = "Max", Team = "Alpha" },
            new DriverStanding { LeagueId = LeagueId, Driver = "Lena", Team = "Beta" });
        ctx.Db.SeasonAwards.AddRange(
            new SeasonAward { Name = "Fahrer des Jahres" },
            new SeasonAward { Name = "Überraschung der Saison" });
        ctx.Db.SaveChanges();

        var http = TestAuthHelper.CreateAuthenticatedContext("u1", "User Eins");
        var ctrl = new AwardsController(ctx.Db);
        TestAuthHelper.AttachContext(ctrl, http);
        ctrl.TempData = new TempDataDictionary(http, new AdminCommunityTestHarness.NullTempDataProvider());
        return (ctrl, ctx);
    }

    private static List<int> Ids(params int[] ids) => ids.ToList();
    private static List<string> Leagues(int count) => Enumerable.Repeat(LeagueId, count).ToList();

    [Fact]
    public async Task Batch_savesEverySelectedAwardInOnePost()
    {
        var (ctrl, ctx) = Build();

        var result = await ctrl.Nominate(Ids(1, 2), Leagues(2), new List<string> { "Max", "Lena" });

        Assert.IsType<RedirectToActionResult>(result);
        var rows = ctx.NewContext().SeasonAwardNominations.OrderBy(n => n.AwardId).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal("Max", rows[0].Driver);
        Assert.Equal("Lena", rows[1].Driver);
        Assert.Equal("2 Stimmen gespeichert.", ctrl.TempData["AwardMessage"]);
    }

    [Fact]
    public async Task EmptySelect_leavesExistingVoteUntouched()
    {
        var (ctrl, ctx) = Build();
        await ctrl.Nominate(Ids(1, 2), Leagues(2), new List<string> { "Max", "Max" });

        // Nur Award 2 wird geändert, Award 1 bleibt leer im Select.
        await ctrl.Nominate(Ids(1, 2), Leagues(2), new List<string> { "", "Lena" });

        var rows = ctx.NewContext().SeasonAwardNominations.OrderBy(n => n.AwardId).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal("Max", rows[0].Driver);
        Assert.Equal("Lena", rows[1].Driver);
    }

    [Fact]
    public async Task EmptySelectsOnly_savesNothing()
    {
        var (ctrl, ctx) = Build();

        var result = await ctrl.Nominate(Ids(1, 2), Leagues(2), new List<string> { "", "" });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Empty(ctx.NewContext().SeasonAwardNominations);
        Assert.Contains("nichts gespeichert", ctrl.TempData["AwardMessage"] as string);
    }

    [Fact]
    public async Task DriverOutsideRoster_isSkipped_validRowStillSaved()
    {
        var (ctrl, ctx) = Build();

        await ctrl.Nominate(Ids(1, 2), Leagues(2), new List<string> { "Geist", "Lena" });

        var rows = ctx.NewContext().SeasonAwardNominations.ToList();
        var row = Assert.Single(rows);
        Assert.Equal(2, row.AwardId);
        Assert.Equal("Lena", row.Driver);
        var message = ctrl.TempData["AwardMessage"] as string;
        Assert.Contains("1 Stimme gespeichert.", message);
        Assert.Contains("nicht im Kader", message);
    }

    [Fact]
    public async Task ChangingVote_overwritesInsteadOfDuplicating()
    {
        var (ctrl, ctx) = Build();
        await ctrl.Nominate(Ids(1), Leagues(1), new List<string> { "Max" });

        await ctrl.Nominate(Ids(1), Leagues(1), new List<string> { "Lena" });

        var row = Assert.Single(ctx.NewContext().SeasonAwardNominations);
        Assert.Equal("Lena", row.Driver);
    }

    [Fact]
    public async Task SingleFieldPair_fromOldForm_stillBinds()
    {
        // Vor dem Umbau hatte jede Karte ein eigenes Formular — ein einzelnes Tripel
        // muss weiterhin durchgehen (alte Links, einzelne Clients).
        var (ctrl, ctx) = Build();

        await ctrl.Nominate(Ids(1), Leagues(1), new List<string> { "Max" });

        Assert.Equal("Max", Assert.Single(ctx.NewContext().SeasonAwardNominations).Driver);
    }
}
