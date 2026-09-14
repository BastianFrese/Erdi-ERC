using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erdi_ERC.Tests.Services;

/// <summary>
/// Tests für den persönlichen Sende-Key (TelemetrySenderKey): Erzeugung hinterlässt NUR den
/// SHA-256-Hash in der DB, der Klartext wird einmalig zurückgegeben; Validierung akzeptiert
/// nur aktive Keys; Sperren setzt RevokedAt. Echter AdminAuditService (nicht Noop), damit
/// die Audit-Persistenz-Konvention mitverifiziert wird.
/// </summary>
public class TelemetryKeyServiceTests
{
    private static TelemetryKeyService Build(SqliteTestContext ctx)
    {
        var audit = new AdminAuditService(ctx.Db, new HttpContextAccessor());
        return new TelemetryKeyService(ctx.Db, audit);
    }

    [Fact]
    public async Task GenerateAsync_existingProfile_persistsHashOnly_andReturnsPlainKey()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.DriverProfiles.Add(new DriverProfile { DiscordId = "d1", DiscordName = "Fahrer 1" });
        await ctx.Db.SaveChangesAsync();

        var svc = Build(ctx);
        var result = await svc.GenerateAsync("d1", "Sim-Rig");

        Assert.True(result.Ok);
        Assert.Null(result.Error);
        Assert.NotNull(result.PlainKey);
        Assert.StartsWith("erct_", result.PlainKey);
        Assert.Equal(45, result.PlainKey!.Length); // "erct_" + 40 hex

        await using var verify = ctx.NewContext();
        var stored = await verify.TelemetrySenderKeys.SingleAsync();
        Assert.Equal("d1", stored.DiscordId);
        Assert.Equal("Sim-Rig", stored.Description);
        Assert.Equal(TelemetryKeyService.ComputeHash(result.PlainKey), stored.KeyHash);
        Assert.NotEqual(result.PlainKey, stored.KeyHash);              // Klartext nie gespeichert
        Assert.DoesNotContain("erct_", stored.KeyHash);
        Assert.Null(stored.RevokedAt);

        // Audit-Persistenz: LogAsync + separater Save durch den Service → Eintrag lesbar.
        var audit = await verify.AdminAuditLogs.SingleOrDefaultAsync(a => a.Action == "TelemetrySenderKeyGenerated");
        Assert.NotNull(audit);
        Assert.Contains("d1", audit!.Details);
    }

    [Fact]
    public async Task GenerateAsync_blankDescription_storesNull()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.DriverProfiles.Add(new DriverProfile { DiscordId = "d1", DiscordName = "Fahrer 1" });
        await ctx.Db.SaveChangesAsync();

        var result = await Build(ctx).GenerateAsync("d1", "   ");
        Assert.True(result.Ok);
        var stored = await ctx.NewContext().TelemetrySenderKeys.SingleAsync();
        Assert.Null(stored.Description);
    }

    [Fact]
    public async Task GenerateAsync_unknownDiscordId_returnsError()
    {
        using var ctx = new SqliteTestContext();
        var result = await Build(ctx).GenerateAsync("gibts-nicht", null);
        Assert.False(result.Ok);
        Assert.Equal("Fahrer-Profil nicht gefunden.", result.Error);
        Assert.Empty(await ctx.NewContext().TelemetrySenderKeys.ToListAsync());
    }

    [Fact]
    public async Task ValidateAsync_knownActiveKey_returnsSenderKey()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.DriverProfiles.Add(new DriverProfile { DiscordId = "d1", DiscordName = "Fahrer 1" });
        await ctx.Db.SaveChangesAsync();
        var generated = await Build(ctx).GenerateAsync("d1", null);

        var resolved = await Build(ctx).ValidateAsync(generated.PlainKey);
        Assert.NotNull(resolved);
        Assert.Equal("d1", resolved!.DiscordId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("falscher-key")]
    public async Task ValidateAsync_absentOrWrongKey_returnsNull(string? apiKey)
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.DriverProfiles.Add(new DriverProfile { DiscordId = "d1", DiscordName = "Fahrer 1" });
        await ctx.Db.SaveChangesAsync();
        await Build(ctx).GenerateAsync("d1", null);

        Assert.Null(await Build(ctx).ValidateAsync(apiKey));
    }

    [Fact]
    public async Task ValidateAsync_revokedKey_returnsNull()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.DriverProfiles.Add(new DriverProfile { DiscordId = "d1", DiscordName = "Fahrer 1" });
        await ctx.Db.SaveChangesAsync();
        var generated = await Build(ctx).GenerateAsync("d1", null);

        var svc = Build(ctx);
        Assert.True(await svc.RevokeAsync(generated.SenderKey!.Id, "admin1"));
        Assert.Null(await Build(ctx).ValidateAsync(generated.PlainKey));
    }

    [Fact]
    public async Task RevokeAsync_setsRevokedAt_once()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.DriverProfiles.Add(new DriverProfile { DiscordId = "d1", DiscordName = "Fahrer 1" });
        await ctx.Db.SaveChangesAsync();
        var generated = await Build(ctx).GenerateAsync("d1", null);

        var svc = Build(ctx);
        Assert.True(await svc.RevokeAsync(generated.SenderKey!.Id, "admin1"));
        // Idempotent: zweites Sperren ist für den Aufrufer weiterhin Erfolg.
        Assert.True(await svc.RevokeAsync(generated.SenderKey.Id, "admin1"));

        var stored = await ctx.NewContext().TelemetrySenderKeys.SingleAsync();
        Assert.NotNull(stored.RevokedAt);
        var audits = await ctx.NewContext().AdminAuditLogs
            .Where(a => a.Action == "TelemetrySenderKeyRevoked").ToListAsync();
        Assert.Single(audits); // kein Doppel-Audit
    }

    [Fact]
    public async Task ListAsync_returnsActiveAndRevoked_orderedByCreatedAtDesc()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.DriverProfiles.Add(new DriverProfile { DiscordId = "d1", DiscordName = "Fahrer 1" });
        ctx.Db.DriverProfiles.Add(new DriverProfile { DiscordId = "d2", DiscordName = "Fahrer 2" });
        await ctx.Db.SaveChangesAsync();

        var svc = Build(ctx);
        var first = await svc.GenerateAsync("d1", null);
        var second = await svc.GenerateAsync("d2", null);
        await svc.RevokeAsync(first.SenderKey!.Id, "admin1");

        var list = await Build(ctx).ListAsync();
        Assert.Equal(2, list.Count);
        Assert.Equal(second.SenderKey!.Id, list[0].Id); // neueste zuerst
        Assert.NotNull(list.Single(k => k.Id == first.SenderKey.Id).RevokedAt);
    }
}
