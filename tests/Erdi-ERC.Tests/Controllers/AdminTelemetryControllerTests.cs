using Erdi_ERC.Controllers;
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

namespace Erdi_ERC.Tests.Controllers;

/// <summary>
/// Controller-Integrationstests der Telemetrie-Review-Inbox: Index/Detail (Anzeige + Sender-Name),
/// Accept (Pending → finales RaceResult inkl. Standings-Rebuild, Season-Fallback, DNF→Position 0),
/// Reject (Status + Note) sowie die Sende-Key-Verwaltung (Erzeugen mit einmaliger Klartext-Anzeige, Sperren).
/// </summary>
public class AdminTelemetryControllerTests
{
    private sealed class RecordingWebhook : IWebhookAutomationService
    {
        public List<string> Events { get; } = new();
        public Task FireAsync(string eventType, Dictionary<string, string> vars)
        {
            Events.Add(eventType);
            return Task.CompletedTask;
        }
        public Task<(bool Success, string? Error)> TestRuleAsync(int ruleId, Dictionary<string, string> vars, CancellationToken ct = default)
            => Task.FromResult((true, (string?)null));
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private static AdminTelemetryController BuildController(SqliteTestContext ctx, out RecordingWebhook webhook)
    {
        var audit = new AdminAuditService(ctx.Db, new HttpContextAccessor());
        var keys = new TelemetryKeyService(ctx.Db, audit);
        var cache = new StaticDataCache(ctx.Db, new MemoryCache(new MemoryCacheOptions()));
        var stats = new StatsService(ctx.Db, Microsoft.Extensions.Options.Options.Create(new F1ScoringOptions()));
        var profiles = new DriverProfileService(ctx.Db, Microsoft.Extensions.Options.Options.Create(new DriverMatchingOptions()));
        webhook = new RecordingWebhook();
        var promotion = new PendingRacePromotionService(ctx.Db, stats, cache, profiles, audit, webhook);

        var ctrl = new AdminTelemetryController(ctx.Db, keys, promotion);
        ctrl.TempData = new TempDataDictionary(new DefaultHttpContext(), new NullTempDataProvider());
        return ctrl;
    }

    private static void AttachAdmin(Controller controller)
    {
        var admin = TestAuthHelper.CreateAdminContext("admin1", "AdminUser");
        controller.ControllerContext = new ControllerContext { HttpContext = admin };
    }

    /// <summary>Seedet Liga (Saison 2026), beide Stammfahrer-Standings und einen Pending-Entwurf.</summary>
    private static async Task<int> SeedPendingAsync(SqliteTestContext ctx, PendingRaceFinish[]? finishes = null)
    {
        ctx.Db.DriverProfiles.Add(new DriverProfile { DiscordId = "d1", DiscordName = "Max Mustermann", DisplayName = "Max" });
        ctx.Db.Leagues.Add(new League { Id = "pro", Name = "ProLiga", CurrentSeason = "2026" });
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "pro", Driver = "Max Mustermann" });
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "pro", Driver = "Anna Beispiel" });
        await ctx.Db.SaveChangesAsync();

        var payload = """
            { "track": "Spa", "league": "ProLiga", "fastestLap": "Anna Beispiel",
              "finishes": [ { "position": 1, "driver": "Max Mustermann" }, { "position": 2, "driver": "Anna Beispiel" } ] }
            """;
        var pending = new PendingRaceResult
        {
            SourcePayload = payload,
            PayloadHash = TelemetryKeyService.ComputeHash(payload),
            SourceTrack = "Spa",
            SourceLeague = "ProLiga",
            SenderDiscordId = "d1",
            ReceivedAt = DateTime.UtcNow,
            Status = (int)PendingRaceStatus.Pending,
        };
        pending.Finishes = (finishes ?? new[]
        {
            new PendingRaceFinish { Position = 1, Driver = "Max Mustermann", FastestLap = false },
            new PendingRaceFinish { Position = 2, Driver = "Anna Beispiel", FastestLap = true },
        }).ToList();
        ctx.Db.PendingRaceResults.Add(pending);
        await ctx.Db.SaveChangesAsync();
        return pending.Id;
    }

    private static TelemetryAcceptInput AcceptInput(int pendingId, params PendingRaceFinishInput[] finishes) => new()
    {
        PendingId = pendingId,
        LeagueId = "pro",
        Track = "Spa",
        Date = new DateTime(2026, 9, 9, 20, 15, 0),
        Season = null, // → Fallback auf Liga-Saison
        Finishes = finishes.ToList(),
    };

    private static PendingRaceFinishInput[] TwoFinishes() => new[]
    {
        new PendingRaceFinishInput { Position = 1, Driver = "Max Mustermann", FastestLap = true },
        new PendingRaceFinishInput { Position = 2, Driver = "Anna Beispiel", FastestLap = false },
    };

    // ── Index ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Index_returnsPendingItems_withSenderName()
    {
        using var ctx = new SqliteTestContext();
        await SeedPendingAsync(ctx);
        var ctrl = BuildController(ctx, out _);
        AttachAdmin(ctrl);

        var result = await ctrl.Index(status: null, page: 1);

        var view = Assert.IsType<ViewResult>(result);
        var items = Assert.IsAssignableFrom<List<TelemetryListItem>>(ctrl.ViewBag.Items);
        var item = Assert.Single(items);
        Assert.Equal("ProLiga", item.SourceLeague);
        Assert.Equal(2, item.FinishCount);
        // Sender-Name batch-aufgelöst (DisplayName bevorzugt).
        Assert.Equal("Max", Assert.IsAssignableFrom<Dictionary<string, string>>(ctrl.ViewBag.SenderMap)["d1"]);
    }

    // ── Detail ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Detail_returnsNotFound_forUnknownId()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = BuildController(ctx, out _);
        AttachAdmin(ctrl);

        Assert.IsType<NotFoundResult>(await ctrl.Detail(9999));
    }

    [Fact]
    public async Task Detail_returnsViewModel_withPendingAndLeagues()
    {
        using var ctx = new SqliteTestContext();
        var id = await SeedPendingAsync(ctx);
        var ctrl = BuildController(ctx, out _);
        AttachAdmin(ctrl);

        var result = Assert.IsType<ViewResult>(await ctrl.Detail(id));
        var vm = Assert.IsType<TelemetryDetailViewModel>(result.Model);
        Assert.Equal(id, vm.Pending.Id);
        Assert.Equal(2, vm.Pending.Finishes.Count);
        Assert.Equal("Max", vm.SenderName);
        Assert.Single(vm.Leagues); // die nicht-archivierte Liga
    }

    // ── Accept ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Accept_createsRaceResult_rebuildsStandings_marksPendingAccepted()
    {
        using var ctx = new SqliteTestContext();
        var id = await SeedPendingAsync(ctx);
        var ctrl = BuildController(ctx, out var webhook);
        AttachAdmin(ctrl);

        var result = await ctrl.Accept(AcceptInput(id, TwoFinishes()));

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminTelemetryController.Index), redirect.ActionName);
        Assert.Contains("übernommen", (string)ctrl.TempData["AdminMessage"]!);

        await using var verify = ctx.NewContext();
        var race = await verify.RaceResults.SingleAsync();
        Assert.Equal("pro", race.LeagueId);
        Assert.Equal("Spa", race.Track);
        Assert.Equal("Max Mustermann", race.Winner);
        Assert.Equal("Anna Beispiel", race.FastestLap); // aus Payload, da im Formular leer
        Assert.Equal("2026", race.Season);              // Fallback auf Liga-Saison

        // DNF-Semantik: Position 0 für DNF, sonst 1-basiert.
        var finish1 = await verify.RaceFinishes.SingleAsync(f => f.Driver == "Max Mustermann");
        Assert.Equal(1, finish1.Position);
        Assert.True(finish1.FastestLap);
        var finish2 = await verify.RaceFinishes.SingleAsync(f => f.Driver == "Anna Beispiel");
        Assert.Equal(2, finish2.Position);

        // Pending → Accepted + Verknüpfung.
        var pending = await verify.PendingRaceResults.SingleAsync();
        Assert.Equal((int)PendingRaceStatus.Accepted, pending.Status);
        Assert.Equal(race.RowId, pending.PromotedRaceResultId);
        Assert.Equal("admin1", pending.DecidedByDiscordId);

        // Standings-Rebuild: P1=25, P2=21.
        var points = await verify.DriverStandings
            .Where(s => s.LeagueId == "pro")
            .ToDictionaryAsync(s => s.Driver, s => s.Points);
        Assert.Equal(25, points["Max Mustermann"]);
        Assert.Equal(21, points["Anna Beispiel"]);

        // Audit + Webhook (post-commit).
        Assert.NotNull(await verify.AdminAuditLogs.SingleOrDefaultAsync(a => a.Action == "TelemetryResultPromoted"));
        Assert.Contains(WebhookEvents.RaceResultSaved, webhook.Events);
    }

    [Fact]
    public async Task Accept_withoutLeague_returnsErrorMessage_andCreatesNoRace()
    {
        using var ctx = new SqliteTestContext();
        var id = await SeedPendingAsync(ctx);
        var ctrl = BuildController(ctx, out _);
        AttachAdmin(ctrl);

        var input = AcceptInput(id, TwoFinishes());
        input.LeagueId = "";
        var result = await ctrl.Accept(input);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Liga und Strecke sind erforderlich.", ctrl.TempData["AdminMessage"]);
        Assert.Empty(await ctx.NewContext().RaceResults.ToListAsync());
    }

    [Fact]
    public async Task Accept_emptyDate_fallsBackToNow()
    {
        using var ctx = new SqliteTestContext();
        var id = await SeedPendingAsync(ctx);
        var ctrl = BuildController(ctx, out _);
        AttachAdmin(ctrl);

        var input = AcceptInput(id, TwoFinishes());
        input.Date = default; // leeres datetime-local-Feld
        await ctrl.Accept(input);

        var race = await ctx.NewContext().RaceResults.SingleAsync();
        Assert.InRange(race.Date, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(5));
        Assert.NotEqual(default, race.Date);
    }

    [Fact]
    public async Task Accept_dnfFinish_storesPositionZero()
    {
        using var ctx = new SqliteTestContext();
        var id = await SeedPendingAsync(ctx);
        var ctrl = BuildController(ctx, out _);
        AttachAdmin(ctrl);

        var input = AcceptInput(id,
            new PendingRaceFinishInput { Position = 1, Driver = "Max Mustermann", FastestLap = true },
            new PendingRaceFinishInput { Position = 2, Driver = "Anna Beispiel", IsDnf = true });
        await ctrl.Accept(input);

        var race = await ctx.NewContext().RaceResults.SingleAsync();
        Assert.Equal("Max Mustermann", race.Winner);
        var dnf = await ctx.NewContext().RaceFinishes.SingleAsync(f => f.Driver == "Anna Beispiel");
        Assert.Equal(0, dnf.Position);
    }

    [Fact]
    public async Task Accept_allDnf_rejectsWithMessage()
    {
        using var ctx = new SqliteTestContext();
        var id = await SeedPendingAsync(ctx);
        var ctrl = BuildController(ctx, out _);
        AttachAdmin(ctrl);

        var input = AcceptInput(id,
            new PendingRaceFinishInput { Position = 1, Driver = "Max Mustermann", IsDnf = true },
            new PendingRaceFinishInput { Position = 2, Driver = "Anna Beispiel", IsDnf = true });
        await ctrl.Accept(input);

        Assert.Contains("Mindestens ein Fahrer muss das Ziel erreicht haben", (string)ctrl.TempData["AdminMessage"]!);
        Assert.Empty(await ctx.NewContext().RaceResults.ToListAsync());
    }

    // ── Reject ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reject_setsRejected_withNoteAndDecider()
    {
        using var ctx = new SqliteTestContext();
        var id = await SeedPendingAsync(ctx);
        var ctrl = BuildController(ctx, out _);
        AttachAdmin(ctrl);

        var result = await ctrl.Reject(id, "Doppelt gesendet");

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Ergebnis-Entwurf abgelehnt.", ctrl.TempData["AdminMessage"]);

        await using var verify = ctx.NewContext();
        var pending = await verify.PendingRaceResults.SingleAsync();
        Assert.Equal((int)PendingRaceStatus.Rejected, pending.Status);
        Assert.Equal("Doppelt gesendet", pending.ReviewNote);
        Assert.Equal("admin1", pending.DecidedByDiscordId);
        Assert.NotNull(await verify.AdminAuditLogs.SingleOrDefaultAsync(a => a.Action == "TelemetryResultRejected"));
    }

    // ── Keys ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Keys_returnsView_withKeyListAndProfiles()
    {
        using var ctx = new SqliteTestContext();
        await SeedPendingAsync(ctx);
        var ctrl = BuildController(ctx, out _);
        AttachAdmin(ctrl);

        // Einen Key per Service erzeugen, damit die Liste nicht leer ist.
        var audit = new AdminAuditService(ctx.Db, new HttpContextAccessor());
        await new TelemetryKeyService(ctx.Db, audit).GenerateAsync("d1", "Sim-Rig");

        var result = Assert.IsType<ViewResult>(await ctrl.Keys());

        var keys = Assert.IsAssignableFrom<IReadOnlyList<TelemetrySenderKey>>(ctrl.ViewBag.Keys);
        var key = Assert.Single(keys);
        Assert.Equal("d1", key.DiscordId);
        Assert.Equal("Sim-Rig", key.Description);
        Assert.Null(key.RevokedAt);
        Assert.Single(Assert.IsAssignableFrom<List<DriverProfile>>(ctrl.ViewBag.Profiles));
    }

    [Fact]
    public async Task GenerateKey_storesHash_andShowsPlainKeyOnce()
    {
        using var ctx = new SqliteTestContext();
        await SeedPendingAsync(ctx);
        var ctrl = BuildController(ctx, out _);
        AttachAdmin(ctrl);

        var result = await ctrl.GenerateKey("d1", "Laptop");

        Assert.IsType<RedirectToActionResult>(result);
        var plain = (string)ctrl.TempData["TelemetryKeyPlain"]!;
        Assert.StartsWith("erct_", plain);
        Assert.Equal("d1", ctrl.TempData["TelemetryKeyOwner"]);

        var stored = await ctx.NewContext().TelemetrySenderKeys.SingleAsync();
        Assert.Equal(TelemetryKeyService.ComputeHash(plain), stored.KeyHash);
        Assert.NotEqual(plain, stored.KeyHash);
    }

    [Fact]
    public async Task GenerateKey_unknownDriver_showsError()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = BuildController(ctx, out _);
        AttachAdmin(ctrl);

        var result = await ctrl.GenerateKey("gibts-nicht", null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Fahrer-Profil nicht gefunden.", ctrl.TempData["AdminMessage"]);
        Assert.Empty(await ctx.NewContext().TelemetrySenderKeys.ToListAsync());
    }

    [Fact]
    public async Task RevokeKey_marksRevoked()
    {
        using var ctx = new SqliteTestContext();
        await SeedPendingAsync(ctx);
        var ctrl = BuildController(ctx, out _);
        AttachAdmin(ctrl);

        var audit = new AdminAuditService(ctx.Db, new HttpContextAccessor());
        var keys = new TelemetryKeyService(ctx.Db, audit);
        var generated = await keys.GenerateAsync("d1", null);

        await ctrl.RevokeKey(generated.SenderKey!.Id);

        var stored = await ctx.NewContext().TelemetrySenderKeys.SingleAsync();
        Assert.NotNull(stored.RevokedAt);
        Assert.Contains("gesperrt", (string)ctrl.TempData["AdminMessage"]!);
    }
}
