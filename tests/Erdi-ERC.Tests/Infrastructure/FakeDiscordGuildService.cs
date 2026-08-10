using <OWNER_HANDLE>_ERC.Services;

namespace <OWNER_HANDLE>_ERC.Tests.Infrastructure;

/// <summary>
/// No-op-Ersatz für den Discord-Guild-Check in Controller-Tests. Liefert ein
/// konfigurierbares Ergebnis, damit der Apply-Flow ohne echte Discord-API
/// durchläuft.
/// </summary>
public class FakeDiscordGuildService : IDiscordGuildService
{
    private readonly DiscordGuildCheckResult _result;

    public FakeDiscordGuildService(bool isOk = true, bool joinedCommunity = true, bool joinedLeague = true)
    {
        _result = new DiscordGuildCheckResult(
            isOk ? DiscordGuildCheckStatus.Ok : DiscordGuildCheckStatus.Unavailable,
            joinedCommunity,
            joinedLeague,
            isOk ? null : "Discord check failed");
    }

    public static FakeDiscordGuildService FullyJoined() => new(true, true, true);
    public static FakeDiscordGuildService MissingLeague() => new(true, true, false);
    public static FakeDiscordGuildService Failure() => new(false, false, false);

    public Task<DiscordGuildCheckResult> CheckMembershipAsync(string? accessToken, CancellationToken ct = default)
        => Task.FromResult(_result);
}
