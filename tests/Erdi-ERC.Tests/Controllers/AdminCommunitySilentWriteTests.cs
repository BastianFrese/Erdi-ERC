using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Erdi_ERC.Tests.Controllers;

/// <summary>
/// Speicherpfade im Community-Admin (Stream-Termine, Giveaways, Real-Life-Events).
///
/// Zwei Fehlerklassen, die dieselben Methoden betreffen:
///
/// 1. Silent Write: der DbContext läuft global mit NoTrackingWithIdentityResolution
///    (Program.cs), <c>FindAsync</c> liefert deshalb eine detached Instanz und
///    SaveChangesAsync() schreibt beim Bearbeiten nichts. Die Tests legen die Entität
///    darum in einem FREMDEN Context an — nur so schlägt der Fehler durch (legt der Test
///    sie über den Request-Context an, liegt sie im ChangeTracker und der Bug bleibt
///    unsichtbar).
/// 2. Wanduhrzeit: Zeitpunkte aus dem Formular sind lokale Zeit, keine UTC-Werte.
///
/// Die View-Anpassungen (ToLocalTime() entfernt) sind hier nicht abgedeckt — für
/// gerendertes Razor gibt es in diesem Test-Projekt keinen Harness (siehe SmokeTests).
/// </summary>
public class AdminCommunitySilentWriteTests
{
    // Repräsentativ für einen datetime-local-Wert: lokale Wanduhrzeit, Kind=Unspecified.
    private static readonly DateTime StreamStart = new(2026, 9, 30, 20, 0, 0);

    private static int SeedStreamInForeignContext(SqliteTestContext ctx)
    {
        var seed = ctx.NewContext();
        seed.StreamSchedules.Add(new StreamSchedule
        {
            Title = "Alt",
            StartAt = StreamStart,
            DurationMinutes = 120,
        });
        seed.SaveChanges();
        return ctx.NewContext().StreamSchedules.Single().Id;
    }

    [Fact]
    public async Task SaveStreamSchedule_create_storesSubmittedWallClockUnchanged()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = AdminCommunityTestHarness.BuildController(ctx);

        var result = await ctrl.SaveStreamSchedule(
            id: null, startAt: StreamStart, durationMinutes: 90,
            title: "Spontan", url: "https://twitch.tv/erdi10");

        Assert.IsType<RedirectToActionResult>(result);

        var saved = ctx.NewContext().StreamSchedules.Single();
        Assert.Equal(StreamStart, saved.StartAt);
        Assert.Equal(90, saved.DurationMinutes);
        Assert.Equal("Spontan", saved.Title);
        Assert.False(saved.IsRecurring);
    }

    [Fact]
    public async Task SaveStreamSchedule_editOfUntrackedEntity_persists()
    {
        using var ctx = new SqliteTestContext();
        var streamId = SeedStreamInForeignContext(ctx);
        var ctrl = AdminCommunityTestHarness.BuildController(ctx);

        var result = await ctrl.SaveStreamSchedule(
            id: streamId, startAt: new DateTime(2026, 10, 1, 19, 30, 0), durationMinutes: 45,
            title: "Bearbeitet", url: null);

        Assert.IsType<RedirectToActionResult>(result);

        var fresh = ctx.NewContext();
        Assert.Single(fresh.StreamSchedules);
        var edited = fresh.StreamSchedules.Single(s => s.Id == streamId);
        Assert.Equal("Bearbeitet", edited.Title);
        Assert.Equal(45, edited.DurationMinutes);
        Assert.Equal(new DateTime(2026, 10, 1, 19, 30, 0), edited.StartAt);
    }

    [Fact]
    public async Task SaveStreamSchedule_editKeepsTimeWhenFormRoundTripsRawValue()
    {
        // Die Edit-Form rendert StartAt jetzt roh in datetime-local (vorher .ToLocalTime(),
        // was den Wert bei jedem Speichern um +2 h verschob). Hier wird genau dieser
        // Roundtrip nachgestellt: anzeigen → unverändert absenden → Wert bleibt gleich.
        using var ctx = new SqliteTestContext();
        var streamId = SeedStreamInForeignContext(ctx);
        var ctrl = AdminCommunityTestHarness.BuildController(ctx);

        var rendered = ctx.NewContext().StreamSchedules.Single().StartAt;
        Assert.Equal("2026-09-30T20:00", rendered.ToString("yyyy-MM-ddTHH:mm"));

        await ctrl.SaveStreamSchedule(id: streamId, startAt: rendered, durationMinutes: 120, title: "Alt", url: null);

        var edited = ctx.NewContext().StreamSchedules.Single(s => s.Id == streamId);
        Assert.Equal(StreamStart, edited.StartAt);
    }

    [Fact]
    public async Task SaveStreamSchedule_recurring_storesNextOccurrenceInLocalTime()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = AdminCommunityTestHarness.BuildController(ctx);

        var now = DateTime.Now;
        var dayOfWeek = (int)now.DayOfWeek;
        var result = await ctrl.SaveStreamSchedule(
            id: null, startAt: null, durationMinutes: 180, title: "Wöchentlich", url: null,
            isRecurring: true, dayOfWeek: dayOfWeek, timeOfDay: new TimeSpan(21, 0, 0));

        Assert.IsType<RedirectToActionResult>(result);

        // Erwartung unabhängig von der Implementierung formuliert: heute 21:00 lokal,
        // und falls das schon vorbei ist, eine Woche später.
        var expected = now.Date.AddHours(21);
        if (expected < now) expected = expected.AddDays(7);

        var saved = ctx.NewContext().StreamSchedules.Single();
        Assert.Equal(expected, saved.StartAt);
        Assert.Equal(DateTimeKind.Unspecified, saved.StartAt.Kind);
        Assert.True(saved.IsRecurring);
        Assert.Equal(new TimeSpan(21, 0, 0), saved.TimeOfDay);
    }

    [Fact]
    public async Task SaveStreamSchedule_writesAuditEntryAfterCommit()
    {
        using var ctx = new SqliteTestContext();
        var httpCtx = TestAuthHelper.CreateAdminContext("admin1", "AdminUser");
        // Echter Audit-Service: mit NoopAudit wäre nicht prüfbar, ob der Eintrag
        // tatsächlich gespeichert wird (LogAsync fügt nur zum Context hinzu).
        var audit = new AdminAuditService(ctx.Db, new HttpContextAccessor { HttpContext = httpCtx });
        var ctrl = AdminCommunityTestHarness.BuildController(ctx, httpCtx, audit);

        await ctrl.SaveStreamSchedule(
            id: null, startAt: StreamStart, durationMinutes: 90, title: "Mit Audit", url: null);

        var log = ctx.NewContext().AdminAuditLogs.SingleOrDefault(l => l.Action == "SaveStreamSchedule");
        Assert.NotNull(log);
        Assert.Equal("StreamSchedule", log!.EntityType);
        Assert.Contains("2026-09-30 20:00", log.Details);
    }

    [Fact]
    public async Task SaveStreamSchedule_recurringWithoutTimeOfDay_isRejected()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = AdminCommunityTestHarness.BuildController(ctx);

        var result = await ctrl.SaveStreamSchedule(
            id: null, startAt: null, durationMinutes: 180, title: "Ohne Uhrzeit", url: null,
            isRecurring: true, dayOfWeek: 3, timeOfDay: null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Empty(ctx.NewContext().StreamSchedules);
    }

    // ── Dieselbe Silent-Write-Klasse in den Nachbarmethoden ─────────────────────

    [Fact]
    public async Task SaveGiveaway_editOfUntrackedEntity_persists()
    {
        using var ctx = new SqliteTestContext();
        var seed = ctx.NewContext();
        seed.Giveaways.Add(new Giveaway
        {
            Title = "Alt",
            StartAt = new DateTime(2026, 9, 20, 12, 0, 0),
            EndAt = new DateTime(2026, 9, 27, 12, 0, 0),
        });
        seed.SaveChanges();
        var giveawayId = ctx.NewContext().Giveaways.Single().Id;

        var ctrl = AdminCommunityTestHarness.BuildController(ctx);
        var result = await ctrl.SaveGiveaway(
            id: giveawayId, title: "Neu", description: "Beschreibung", prize: "Preis",
            startAt: new DateTime(2026, 9, 21, 12, 0, 0), endAt: new DateTime(2026, 9, 28, 12, 0, 0),
            link: "https://example.com");

        Assert.IsType<RedirectToActionResult>(result);

        var fresh = ctx.NewContext();
        Assert.Single(fresh.Giveaways);
        var edited = fresh.Giveaways.Single(g => g.Id == giveawayId);
        Assert.Equal("Neu", edited.Title);
        Assert.Equal(new DateTime(2026, 9, 21, 12, 0, 0), edited.StartAt);
        Assert.Equal(new DateTime(2026, 9, 28, 12, 0, 0), edited.EndAt);
    }

    [Fact]
    public async Task SaveRealLifeEvent_editOfUntrackedEntity_persists()
    {
        using var ctx = new SqliteTestContext();
        var seed = ctx.NewContext();
        seed.RealLifeEvents.Add(new RealLifeEvent
        {
            Title = "Alt",
            Date = new DateTime(2026, 9, 20, 18, 0, 0),
            IsUpcoming = false,
        });
        seed.SaveChanges();
        var eventId = ctx.NewContext().RealLifeEvents.Single().Id;

        var ctrl = AdminCommunityTestHarness.BuildController(ctx);
        var result = await ctrl.SaveRealLifeEvent(
            id: eventId, title: "Kart-Event", date: new DateTime(2026, 10, 4, 14, 0, 0),
            location: "Kartbahn", description: "Beschreibung", videoUrl: null,
            isUpcoming: true, image: null);

        Assert.IsType<RedirectToActionResult>(result);

        var fresh = ctx.NewContext();
        Assert.Single(fresh.RealLifeEvents);
        var edited = fresh.RealLifeEvents.Single(e => e.Id == eventId);
        Assert.Equal("Kart-Event", edited.Title);
        Assert.Equal(new DateTime(2026, 10, 4, 14, 0, 0), edited.Date);
        Assert.True(edited.IsUpcoming);
    }
}
