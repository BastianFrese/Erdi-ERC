using Erdi_ERC.Models;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Erdi_ERC.Tests.Controllers;

/// <summary>
/// Giveaway-CRUD im Admin (Community → Giveaways): Anlegen/Bearbeiten/Löschen der
/// Startseiten-Infotafel-Einträge. Sichtbarkeit ist zeitraum-basiert (StartAt/EndAt),
/// der Dienst selbst filtert in <see cref="Erdi_ERC.Services.HomeIndexDataService"/> —
/// hier nur die Persistenz + Validierung (Titel-Pflicht, EndAt &gt; StartAt, http(s)-Link).
///
/// Controller-Aufbau und Stubs kommen aus <see cref="AdminCommunityTestHarness"/>.
/// </summary>
public class AdminCommunityGiveawayTests
{
    private static DateTime FixedNow() => new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SaveGiveaway_create_persistsAllFields()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = AdminCommunityTestHarness.BuildController(ctx);
        var now = FixedNow();

        var result = await ctrl.SaveGiveaway(
            id: null,
            title: "Fanatec-Lenkrad verlosen",
            description: "Einfach im Chat teilnehmen.",
            prize: "Fanatec-Pedalset",
            startAt: now.AddDays(-1),
            endAt: now.AddDays(7),
            link: "https://discord.gg/erc");

        Assert.IsType<RedirectToActionResult>(result);

        var saved = ctx.NewContext().Giveaways.Single(g => g.Title == "Fanatec-Lenkrad verlosen");
        Assert.Equal("Fanatec-Pedalset", saved.Prize);
        Assert.Equal("Einfach im Chat teilnehmen.", saved.Description);
        Assert.Equal("https://discord.gg/erc", saved.Link);
        Assert.Equal(now.AddDays(-1), saved.StartAt);
        Assert.Equal(now.AddDays(7), saved.EndAt);
    }

    [Fact]
    public async Task SaveGiveaway_create_withoutLink_keepsLinkNull()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = AdminCommunityTestHarness.BuildController(ctx);
        var now = FixedNow();

        var result = await ctrl.SaveGiveaway(
            id: null, title: "Ohne Link", description: null, prize: null,
            startAt: now, endAt: now.AddDays(3), link: null);

        Assert.IsType<RedirectToActionResult>(result);
        var saved = ctx.NewContext().Giveaways.Single(g => g.Title == "Ohne Link");
        Assert.Null(saved.Link);
    }

    [Fact]
    public async Task SaveGiveaway_create_withoutTitle_isRejected()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = AdminCommunityTestHarness.BuildController(ctx);
        var now = FixedNow();

        var result = await ctrl.SaveGiveaway(
            id: null, title: "   ", description: null, prize: null,
            startAt: now, endAt: now.AddDays(3), link: null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Giveaways", redirect.ActionName);
        Assert.Equal("Titel ist erforderlich.", ctrl.TempData["AdminMessage"]);
        Assert.Equal(0, ctx.NewContext().Giveaways.Count());
    }

    [Fact]
    public async Task SaveGiveaway_create_endBeforeStart_isRejected()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = AdminCommunityTestHarness.BuildController(ctx);
        var now = FixedNow();

        var result = await ctrl.SaveGiveaway(
            id: null, title: "Falscher Zeitraum",
            description: null, prize: null,
            startAt: now.AddDays(5), endAt: now.AddDays(1), link: null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Giveaways", redirect.ActionName);
        Assert.Contains("Enddatum", ctrl.TempData["AdminMessage"] as string);
        Assert.Equal(0, ctx.NewContext().Giveaways.Count());
    }

    [Fact]
    public async Task SaveGiveaway_rejectsInvalidLink()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = AdminCommunityTestHarness.BuildController(ctx);
        var now = FixedNow();

        var result = await ctrl.SaveGiveaway(
            id: null, title: "Kaputter Link",
            description: null, prize: null,
            startAt: now, endAt: now.AddDays(3), link: "kein-link");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Giveaways", redirect.ActionName);
        Assert.Contains("Link", ctrl.TempData["AdminMessage"] as string);
        Assert.Equal(0, ctx.NewContext().Giveaways.Count());
    }

    [Fact]
    public async Task SaveGiveaway_edit_updatesExisting()
    {
        using var ctx = new SqliteTestContext();

        // Anlegen in einem FREMDEN Context: so liegt das Giveaway nicht im ChangeTracker
        // des Request-Contexts und der frühere Silent Write (FindAsync → detached) würde
        // auffallen. Wird direkt über ctx.Db angelegt, findet FindAsync die Entität im
        // Tracker und der Test bestünde auch mit dem Bug.
        var seed = ctx.NewContext();
        seed.Giveaways.Add(new Giveaway
        {
            Title = "Alt-Titel",
            StartAt = FixedNow(),
            EndAt = FixedNow().AddDays(3),
        });
        seed.SaveChanges();

        var ctrl = AdminCommunityTestHarness.BuildController(ctx);
        var existingId = ctx.NewContext().Giveaways.Single().Id;

        var result = await ctrl.SaveGiveaway(
            id: existingId,
            title: "Neuer Titel",
            description: "Geändert",
            prize: "Neu",
            startAt: FixedNow().AddDays(-2),
            endAt: FixedNow().AddDays(10),
            link: "https://example.com");

        Assert.IsType<RedirectToActionResult>(result);

        var edited = ctx.NewContext().Giveaways.Single(g => g.Id == existingId);
        Assert.Equal("Neuer Titel", edited.Title);
        Assert.Equal("Geändert", edited.Description);
        Assert.Equal("Neu", edited.Prize);
        Assert.Equal(FixedNow().AddDays(-2), edited.StartAt);
        Assert.Equal(FixedNow().AddDays(10), edited.EndAt);
        Assert.Equal(1, ctx.NewContext().Giveaways.Count()); // kein neuer Eintrag
    }

    [Fact]
    public async Task DeleteGiveaway_removesEntity()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Giveaways.Add(new Giveaway
        {
            Title = "Zu löschen",
            StartAt = FixedNow(),
            EndAt = FixedNow().AddDays(2),
        });
        ctx.Db.SaveChanges();
        var id = ctx.NewContext().Giveaways.Single().Id;

        var ctrl = AdminCommunityTestHarness.BuildController(ctx);
        var result = await ctrl.DeleteGiveaway(id);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Giveaways", redirect.ActionName);
        Assert.Equal("Giveaway gelöscht.", ctrl.TempData["AdminMessage"]);
        Assert.Equal(0, ctx.NewContext().Giveaways.Count());
    }
}
