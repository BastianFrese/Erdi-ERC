using Erdi_ERC.Models;
using Erdi_ERC.Options;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Erdi_ERC.Tests.Services;

/// <summary>
/// Tests für <see cref="SetupAccessService"/>: ResolveUserAsync (Discord /users/@me für den
/// SetupBlockedUsers-Check der Setups-API) und ResolveSetupAccessAsync (Guild-/Rollen-Auflösung
/// gegen eine gestubbte Discord-API).
/// </summary>
public class SetupAccessServiceTests
{
    private const string GuildId = "guild1";

    private static SetupAccessService Build(
        SqliteTestContext ctx,
        FakeHttpClientFactory http,
        int minTenureDays = 0)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new DiscordSetupAccessOptions
        {
            GuildId = GuildId,
            MinGuildTenureDays = minTenureDays,
        });
        return new SetupAccessService(
            options, ctx.Db, NullLogger<SetupAccessService>.Instance, http);
    }

    // ── ResolveUserAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveUserAsync_nullToken_returnsNull()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx, new FakeHttpClientFactory(_ => FakeHttpClientFactory.Json("{}")));

        Assert.Null(await svc.ResolveUserAsync(null));
        Assert.Null(await svc.ResolveUserAsync("   "));
    }

    [Fact]
    public async Task ResolveUserAsync_discordError_returnsNull()
    {
        using var ctx = new SqliteTestContext();
        var http = new FakeHttpClientFactory(_ => FakeHttpClientFactory.Error(System.Net.HttpStatusCode.Unauthorized));
        var svc = Build(ctx, http);

        Assert.Null(await svc.ResolveUserAsync("token"));
    }

    [Fact]
    public async Task ResolveUserAsync_success_parsesUser()
    {
        using var ctx = new SqliteTestContext();
        var http = new FakeHttpClientFactory(_ => FakeHttpClientFactory.Json(
            """{"id":"123","username":"playerone","global_name":"Player One"}"""));
        var svc = Build(ctx, http);

        var user = await svc.ResolveUserAsync("token");

        Assert.NotNull(user);
        Assert.Equal("123", user!.Id);
        Assert.Equal("playerone", user.Username);
        Assert.Equal("Player One", user.GlobalName);
        Assert.Equal("https://discord.com/api/v10/users/@me", http.LastRequest?.RequestUri?.ToString());
        Assert.Equal("Bearer token", http.LastRequest?.Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task ResolveUserAsync_missingId_returnsNull()
    {
        using var ctx = new SqliteTestContext();
        var http = new FakeHttpClientFactory(_ => FakeHttpClientFactory.Json(
            """{"username":"playerone"}"""));
        var svc = Build(ctx, http);

        Assert.Null(await svc.ResolveUserAsync("token"));
    }

    // ── ResolveSetupAccessAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task ResolveSetupAccessAsync_noMappings_returnsTier0()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx, new FakeHttpClientFactory(_ => FakeHttpClientFactory.Json("[]")));

        var result = await svc.ResolveSetupAccessAsync("token");

        Assert.True(result.Success);
        Assert.Equal(0, result.Tier);
        Assert.False(result.IsOnCommunityGuild);
    }

    [Fact]
    public async Task ResolveSetupAccessAsync_notInGuild_returnsTier0()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.SetupAccessRoleMappings.Add(new SetupAccessRoleMapping { Tier = 3, RoleId = "role3", Label = "T1" });
        await ctx.Db.SaveChangesAsync();
        var http = new FakeHttpClientFactory(_ => FakeHttpClientFactory.Json(
            """[{"id":"other-guild","name":"Andere"}]"""));
        var svc = Build(ctx, http);

        var result = await svc.ResolveSetupAccessAsync("token");

        Assert.True(result.Success);
        Assert.Equal(0, result.Tier);
        Assert.False(result.IsOnCommunityGuild);
    }

    [Fact]
    public async Task ResolveSetupAccessAsync_withMatchingRole_returnsTier()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.SetupAccessRoleMappings.Add(new SetupAccessRoleMapping { Tier = 3, RoleId = "role3", Label = "T1" });
        ctx.Db.SetupAccessRoleMappings.Add(new SetupAccessRoleMapping { Tier = 5, RoleId = "role5", Label = "T3" });
        await ctx.Db.SaveChangesAsync();
        var http = new FakeHttpClientFactory(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/guilds"))
            {
                return FakeHttpClientFactory.Json($$"""[{"id":"{{GuildId}}","name":"Community"}]""");
            }
            return FakeHttpClientFactory.Json(
                """{"roles":["role5","role3"],"joined_at":"2024-01-01T00:00:00Z"}""");
        });
        var svc = Build(ctx, http);

        var result = await svc.ResolveSetupAccessAsync("token");

        Assert.True(result.Success);
        Assert.Equal(5, result.Tier); // höchste passende Rolle gewinnt
        Assert.Equal("T3", result.RoleLabel);
        Assert.True(result.IsOnCommunityGuild);
    }

    [Fact]
    public async Task ResolveSetupAccessAsync_noMatchingRole_returnsTier1()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.SetupAccessRoleMappings.Add(new SetupAccessRoleMapping { Tier = 3, RoleId = "role3", Label = "T1" });
        await ctx.Db.SaveChangesAsync();
        var http = new FakeHttpClientFactory(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/guilds"))
            {
                return FakeHttpClientFactory.Json($$"""[{"id":"{{GuildId}}","name":"Community"}]""");
            }
            return FakeHttpClientFactory.Json(
                """{"roles":["unrelated-role"],"joined_at":"2024-01-01T00:00:00Z"}""");
        });
        var svc = Build(ctx, http);

        var result = await svc.ResolveSetupAccessAsync("token");

        Assert.True(result.Success);
        Assert.Equal(1, result.Tier); // auf der Guild, aber keine passende Rolle
        Assert.True(result.IsOnCommunityGuild);
    }

    [Fact]
    public async Task ResolveSetupAccessAsync_tenureNotMet_reducesTierTo0()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.SetupAccessRoleMappings.Add(new SetupAccessRoleMapping { Tier = 3, RoleId = "role3", Label = "T1" });
        await ctx.Db.SaveChangesAsync();
        var http = new FakeHttpClientFactory(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/guilds"))
            {
                return FakeHttpClientFactory.Json($$"""[{"id":"{{GuildId}}","name":"Community"}]""");
            }
            // Gerade eben beigetreten → Tenure (7 Tage) nicht erfüllt.
            return FakeHttpClientFactory.Json(
                """{"roles":["role3"],"joined_at":"2026-09-10T00:00:00Z"}""");
        });
        var svc = Build(ctx, http, minTenureDays: 7);

        var result = await svc.ResolveSetupAccessAsync("token");

        Assert.True(result.Success);
        Assert.Equal(0, result.Tier);
        Assert.True(result.IsPendingTenure);
        Assert.Null(result.RoleLabel);
    }
}
