using Erdi_ERC.Controllers;
using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Erdi_ERC.Tests.Controllers;

/// <summary>
/// Setup-Editor: Änderungen an einem bestehenden Setup müssen tatsächlich in der DB
/// landen ("Änderungen beim Setup Editor werden nicht gespeichert").
///
/// Ursache des Bugs war derselbe Silent-Write wie beim Stewarding: der DbContext läuft
/// global mit NoTrackingWithIdentityResolution (Program.cs), FindAsync liefert daher eine
/// detached Instanz und SaveChangesAsync() schreibt beim Update nichts.
/// </summary>
public class AdminSetupsSaveTrackSetupTests
{
    private const string PayloadJson =
        """{"version":2,"source":"f1-ingame-style-editor-v2","strategy":"medium","trackKey":"imola","lengthKey":"medium","strategyPlan":"Lap 12: Pit","categories":{"aero":{"frontWing":5}}}""";

    private static AdminSetupsController BuildController(SqliteTestContext ctx)
    {
        var httpCtx = TestAuthHelper.CreateAdminContext("admin1", "AdminUser");
        var ctrl = new AdminSetupsController(
            ctx.Db,
            new AdminAuditService(ctx.Db, new HttpContextAccessor { HttpContext = httpCtx }),
            new ConfigurationBuilder().Build());
        TestAuthHelper.AttachContext(ctrl, httpCtx);
        ctrl.TempData = new TempDataDictionary(httpCtx, new NullTempDataProvider());
        return ctrl;
    }

    /// <summary>Legt ein Setup in einem FREMDEN Context an, damit der Request-Context es nie trackt.</summary>
    private static int SeedSetupInForeignContext(SqliteTestContext ctx, string track = "Imola", string title = "Original")
    {
        var seedCtx = ctx.NewContext();
        seedCtx.TrackSetups.Add(new TrackSetup
        {
            Track = track,
            Title = title,
            SetupText = PayloadJson,
            RequiredAccessTier = 0,
            CreatedAt = new DateTime(2026, 8, 1),
            UpdatedAt = new DateTime(2026, 8, 1),
        });
        seedCtx.SaveChanges();

        return ctx.NewContext().TrackSetups.Single().Id;
    }

    [Fact]
    public async Task SaveTrackSetup_create_persistsNewSetup()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = BuildController(ctx);

        var result = await ctrl.SaveTrackSetup(
            id: null, track: "Monza", title: "Low Downforce", requiredAccessTier: 3,
            requiredRoleLabel: "T1", setupInfo: "Für lange Geraden", setupText: PayloadJson,
            strategy: "medium", gameYear: "26");

        Assert.IsType<RedirectToActionResult>(result);

        var created = ctx.NewContext().TrackSetups.Single();
        Assert.Equal("Monza", created.Track);
        Assert.Equal("Low Downforce", created.Title);
        Assert.Equal(3, created.RequiredAccessTier);
        Assert.Equal("26", created.GameYear);
        // Payload (inkl. F1-Strategie) wird unverändert gespeichert — der Editor schreibt
        // trackKey/lengthKey/strategyPlan selbst ins JSON.
        Assert.Contains("\"strategyPlan\":\"Lap 12: Pit\"", created.SetupText);
        Assert.Contains("\"trackKey\":\"imola\"", created.SetupText);
    }

    [Fact]
    public async Task SaveTrackSetup_edit_persistsChangesWhenEntityWasNeverTrackedByRequestContext()
    {
        using var ctx = new SqliteTestContext();
        var setupId = SeedSetupInForeignContext(ctx);

        var ctrl = BuildController(ctx);
        var result = await ctrl.SaveTrackSetup(
            id: setupId, track: "Imola", title: "Bearbeitet", requiredAccessTier: 1,
            requiredRoleLabel: null, setupInfo: "neue Info", setupText: PayloadJson,
            strategy: "short", gameYear: "25");

        Assert.IsType<RedirectToActionResult>(result);

        var edited = ctx.NewContext().TrackSetups.Single(s => s.Id == setupId);
        Assert.Equal("Bearbeitet", edited.Title);
        Assert.Equal(1, edited.RequiredAccessTier);
        Assert.Equal("neue Info", edited.SetupInfo);
        Assert.Equal("25", edited.GameYear);
        Assert.NotEqual(new DateTime(2026, 8, 1), edited.UpdatedAt);
    }

    [Fact]
    public async Task SaveTrackSetup_edit_doesNotCreateSecondRow()
    {
        using var ctx = new SqliteTestContext();
        var setupId = SeedSetupInForeignContext(ctx);

        var ctrl = BuildController(ctx);
        await ctrl.SaveTrackSetup(
            id: setupId, track: "Imola", title: "Bearbeitet", requiredAccessTier: 0,
            requiredRoleLabel: null, setupInfo: null, setupText: PayloadJson,
            strategy: null, gameYear: null);

        var fresh = ctx.NewContext();
        Assert.Single(fresh.TrackSetups);
        Assert.Equal(setupId, fresh.TrackSetups.Single().Id);
    }

    [Fact]
    public async Task SaveTrackSetup_writesAuditLogEntryAfterCommit()
    {
        using var ctx = new SqliteTestContext();

        var ctrl = BuildController(ctx);
        await ctrl.SaveTrackSetup(
            id: null, track: "Spa", title: "Regen", requiredAccessTier: 5,
            requiredRoleLabel: null, setupInfo: null, setupText: PayloadJson,
            strategy: "wet", gameYear: "26");

        // LogAsync() allein fügt nur zum Context hinzu — ohne LogAndSaveAsync() ginge der
        // Audit-Eintrag beim Request-Ende verloren.
        var log = ctx.NewContext().AdminAuditLogs.SingleOrDefault(l => l.Action == "SaveTrackSetup");
        Assert.NotNull(log);
        Assert.Equal("TrackSetup", log!.EntityType);
        Assert.Contains("Spa", log.Details);
    }

    [Fact]
    public async Task SaveTrackSetup_rejectsMissingRequiredFields()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = BuildController(ctx);

        var result = await ctrl.SaveTrackSetup(
            id: null, track: "  ", title: "Titel", requiredAccessTier: 0,
            requiredRoleLabel: null, setupInfo: null, setupText: PayloadJson,
            strategy: null, gameYear: null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Empty(ctx.NewContext().TrackSetups);
    }

    // ── Role-Mapping: gleicher Silent-Write im selben Admin-Bereich ──────────────

    [Fact]
    public async Task SaveSetupAccessRoleMapping_edit_persistsChanges()
    {
        using var ctx = new SqliteTestContext();

        var seedCtx = ctx.NewContext();
        seedCtx.SetupAccessRoleMappings.Add(new SetupAccessRoleMapping
        {
            Tier = 3,
            RoleId = "111",
            Label = "Alt",
            CreatedAt = new DateTime(2026, 8, 1),
        });
        seedCtx.SaveChanges();
        var mappingId = ctx.NewContext().SetupAccessRoleMappings.Single().Id;

        var ctrl = BuildController(ctx);
        var result = await ctrl.SaveSetupAccessRoleMapping(id: mappingId, tier: 4, roleId: "222", label: "Neu");

        Assert.IsType<RedirectToActionResult>(result);

        var edited = ctx.NewContext().SetupAccessRoleMappings.Single(m => m.Id == mappingId);
        Assert.Equal(4, edited.Tier);
        Assert.Equal("222", edited.RoleId);
        Assert.Equal("Neu", edited.Label);
    }

    [Fact]
    public async Task SaveSetupAccessRoleMapping_rejectsTierOutsideSubscriptionRange()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = BuildController(ctx);

        var result = await ctrl.SaveSetupAccessRoleMapping(id: null, tier: 2, roleId: "111", label: null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Empty(ctx.NewContext().SetupAccessRoleMappings);
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }
}
