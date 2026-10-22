using Erdi_ERC.Options;
using Erdi_ERC.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using Xunit;

namespace Erdi_ERC.Tests.Services;

/// <summary>
/// Auto-Beitritt nach dem Bewerben: ohne Bot-Token darf gar kein Request rausgehen
/// (die Bewerbung läuft weiter), mit Token muss ein 201 als „Joined" gemeldet werden.
/// </summary>
public class DiscordJoinServiceTests
{
    private const string Community = "111";
    private const string League = "222";

    private sealed class StubHandler : HttpMessageHandler
    {
        public List<string> Calls { get; } = new();
        private readonly HttpStatusCode _status;

        public StubHandler(HttpStatusCode status) => _status = status;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls.Add($"{request.Method} {request.RequestUri}");
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent("{}")
            });
        }
    }

    private sealed class Factory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public Factory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private static DiscordJoinService Build(HttpMessageHandler handler, string? botToken)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Discord:BotToken"] = botToken,
            })
            .Build();

        return new DiscordJoinService(
            new Factory(handler),
            Microsoft.Extensions.Options.Options.Create(new ApplicationOptions()),
            Microsoft.Extensions.Options.Options.Create(new DiscordGuildOptions { CommunityGuildId = Community, LeagueGuildId = League }),
            config,
            NullLogger<DiscordJoinService>.Instance);
    }

    [Fact]
    public async Task Ohne_Bot_Token_wird_kein_Request_gesendet()
    {
        var handler = new StubHandler(HttpStatusCode.Created);

        var result = await Build(handler, botToken: null).EnsureJoinedAsync("42", "token");

        Assert.Equal(DiscordJoinOutcome.Skipped, result.Outcome);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task Ohne_Access_Token_wird_kein_Request_gesendet()
    {
        var handler = new StubHandler(HttpStatusCode.Created);

        var result = await Build(handler, "bot").EnsureJoinedAsync("42", accessToken: null);

        Assert.Equal(DiscordJoinOutcome.Skipped, result.Outcome);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task Beitritt_in_beide_Server_mit_Bot_Token()
    {
        var handler = new StubHandler(HttpStatusCode.Created);

        var result = await Build(handler, "bot").EnsureJoinedAsync("42", "user-token");

        Assert.Equal(DiscordJoinOutcome.Joined, result.Outcome);
        Assert.Equal(2, handler.Calls.Count);
        Assert.Contains($"https://discord.com/api/v10/guilds/{Community}/members/42", handler.Calls[0]);
        Assert.Contains($"https://discord.com/api/v10/guilds/{League}/members/42", handler.Calls[1]);
    }

    [Fact]
    public async Task Bereits_Mitglied_ist_kein_Fehler()
    {
        var handler = new StubHandler(HttpStatusCode.NoContent);

        var result = await Build(handler, "bot").EnsureJoinedAsync("42", "user-token");

        Assert.Equal(DiscordJoinOutcome.AlreadyMember, result.Outcome);
    }

    [Fact]
    public async Task Fehlende_Berechtigung_wird_gemeldet_aber_nicht_geworfen()
    {
        var handler = new StubHandler(HttpStatusCode.Forbidden);

        var result = await Build(handler, "bot").EnsureJoinedAsync("42", "user-token");

        Assert.Equal(DiscordJoinOutcome.Failed, result.Outcome);
        Assert.Contains("403", result.Detail);
    }
}
