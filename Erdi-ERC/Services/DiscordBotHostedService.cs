using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Erdi_ERC.Helpers;
using Erdi_ERC.Options;
using Microsoft.Extensions.Options;

namespace Erdi_ERC.Services;

/// <summary>
/// Hält die Gateway-Verbindung des Bots offen (damit er dauerhaft online ist) und setzt
/// dabei die Aktivität: läuft ein Stream, zeigt die Karte ihn mit klickbarem Link; sonst
/// rotieren die neuesten Twitch-Clips. Ohne erreichbare Twitch-Daten bleibt die zuletzt
/// bekannte Karte stehen — der Bot geht dadurch nicht offline.
///
/// Twitch wird über die öffentliche GraphQL-Schnittstelle desselben Web-Client-IDs
/// abgefragt, den die Twitch-Webseite benutzt. Kein App-Zugang nötig, dafür inoffiziell:
/// eine Abfrage pro Minute bleibt weit unter dem, was Twitch blockt.
/// </summary>
public sealed class DiscordBotHostedService : BackgroundService
{
    private static readonly Uri GatewayUri = new("wss://gateway.discord.gg/?v=10&encoding=json");
    private const string TwitchGqlEndpoint = "https://gql.twitch.tv/gql";
    private const string TwitchWebClientId = "kimne78kx3ncx6brgo4mv6wki5h1ko";

    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TwitchPollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RotationInterval = TimeSpan.FromSeconds(45);

    private readonly IConfiguration _configuration;
    private readonly ApplicationOptions _app;
    private readonly IHostEnvironment _environment;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DiscordBotHostedService> _logger;

    /// <summary>Letzte erfolgreiche Twitch-Abfrage. Bleibt bei Fehlern stehen.</summary>
    private TwitchSnapshot? _snapshot;
    private DateTime _nextPollAt = DateTime.MinValue;
    private DateTime _nextRotationAt = DateTime.MinValue;
    private int _rotationIndex;

    /// <summary>Zuletzt gesetzte Karte — nur Änderungen landen im Log, sonst rauscht es.</summary>
    private string? _lastPresence;

    public DiscordBotHostedService(
        IConfiguration configuration,
        IOptions<ApplicationOptions> app,
        IHostEnvironment environment,
        IHttpClientFactory httpClientFactory,
        ILogger<DiscordBotHostedService> logger)
    {
        _configuration = configuration;
        _app = app.Value;
        _environment = environment;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var token = _configuration["Discord:BotToken"];
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogInformation("Discord-Bot: kein Bot-Token konfiguriert, Präsenz bleibt aus.");
            return;
        }

        if (!_environment.IsProduction())
        {
            // Sonst setzt jeder lokale Lauf die Präsenz des echten Bots.
            _logger.LogInformation("Discord-Bot: außerhalb von Production deaktiviert.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunConnectionAsync(token, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Discord-Bot: Verbindung abgerissen, neuer Versuch in {Sekunden} s",
                    ReconnectDelay.TotalSeconds);
            }

            try
            {
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunConnectionAsync(string token, CancellationToken ct)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(GatewayUri, ct);

        var sendLock = new SemaphoreSlim(1, 1);
        var sequence = 0;
        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var hello = await ReceiveJsonAsync(socket, ct)
            ?? throw new InvalidOperationException("Gateway schickte kein HELLO.");
        if (hello["op"]?.GetValue<int>() != 10)
            throw new InvalidOperationException($"Erwartet HELLO (10), bekommen {hello["op"]}.");

        var intervalMs = hello["d"]?["heartbeat_interval"]?.GetValue<int>() ?? 41250;
        var heartbeatTask = RunHeartbeatAsync(socket, sendLock, intervalMs, () => sequence, heartbeatCts.Token);

        await SendAsync(socket, sendLock, new JsonObject
        {
            ["op"] = 2,
            ["d"] = new JsonObject
            {
                ["token"] = token,
                ["intents"] = 0,
                ["properties"] = new JsonObject
                {
                    ["os"] = "linux",
                    ["browser"] = "erdi-erc",
                    ["device"] = "erdi-erc",
                },
            },
        }, ct);

        _logger.LogInformation("Discord-Bot: Verbindung steht, warte auf READY.");

        try
        {
            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var frame = await ReceiveJsonAsync(socket, ct);
                if (frame is null) break;

                if (frame["s"] is JsonNode seqNode && seqNode.GetValueKind() == JsonValueKind.Number)
                    sequence = seqNode.GetValue<int>();

                switch (frame["op"]?.GetValue<int>())
                {
                    case 7: // Reconnect
                        _logger.LogInformation("Discord-Bot: Gateway verlangt Neuverbindung.");
                        return;
                    case 9: // Invalid Session
                        _logger.LogWarning("Discord-Bot: Sitzung ungültig, neuer Versuch.");
                        await Task.Delay(TimeSpan.FromSeconds(3), ct);
                        return;
                    case 0:
                        var eventName = frame["t"]?.GetValue<string>();
                        if (eventName == "READY")
                        {
                            _logger.LogInformation("Discord-Bot: online als {User}.",
                                frame["d"]?["user"]?["username"]?.GetValue<string>());
                            await RefreshPresenceAsync(socket, sendLock, force: true, ct);
                        }
                        break;
                    case 11: // Heartbeat bestätigt
                        break;
                    case 1: // Server fordert Heartbeat
                        await SendHeartbeatAsync(socket, sendLock, sequence, ct);
                        break;
                }

                // Empfangsgetrieben: Discord schickt spätestens alle ~41 s einen Heartbeat-Ack,
                // also kommt die Rotation damit von selbst im 45-Sekunden-Takt voran.
                await RefreshPresenceAsync(socket, sendLock, force: false, ct);
            }
        }
        finally
        {
            await heartbeatCts.CancelAsync();
            try { await heartbeatTask; } catch (OperationCanceledException) { /* Abbau */ }
        }
    }

    private async Task RefreshPresenceAsync(ClientWebSocket socket, SemaphoreSlim sendLock, bool force, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (now >= _nextPollAt)
        {
            _nextPollAt = now.Add(TwitchPollInterval);
            var fresh = await PollTwitchAsync(ct);
            if (fresh is not null) _snapshot = fresh;
        }

        if (!force && now < _nextRotationAt) return;
        _nextRotationAt = now.Add(RotationInterval);

        var presence = BotPresencePlanner.Build(_app.TwitchChannel, _snapshot, _rotationIndex);
        if (presence.Type != BotPresence.Streaming) _rotationIndex++;

        var label = $"{presence.Type}|{presence.Name}";
        if (label != _lastPresence)
        {
            _lastPresence = label;
            _logger.LogInformation("Discord-Bot: Karte = {Name} ({Typ})", presence.Name,
                presence.Type == BotPresence.Streaming ? "Streaming" : "Watching");
        }

        var activity = new JsonObject
        {
            ["name"] = presence.Name,
            ["type"] = presence.Type,
        };
        if (presence.Url is not null) activity["url"] = presence.Url;
        if (presence.Details is not null) activity["details"] = presence.Details;
        if (presence.State is not null) activity["state"] = presence.State;

        await SendAsync(socket, sendLock, new JsonObject
        {
            ["op"] = 3,
            ["d"] = new JsonObject
            {
                ["since"] = 0,
                ["status"] = "online",
                ["afk"] = false,
                ["activities"] = new JsonArray(activity),
            },
        }, ct);
    }

    private async Task<TwitchSnapshot?> PollTwitchAsync(CancellationToken ct)
    {
        var login = _app.TwitchChannel;
        var query =
            "{ user(login:" + JsonSerializer.Serialize(login) + "){ displayName stream{ title game{ name } } " +
            "clips(first:5, criteria:{period:LAST_WEEK}){ edges{ node{ title url createdAt viewCount } } } } }";

        try
        {
            var client = _httpClientFactory.CreateClient("TwitchApi");
            using var request = new HttpRequestMessage(HttpMethod.Post, TwitchGqlEndpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { query }), Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("Client-ID", TwitchWebClientId);

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Twitch-Abfrage fehlgeschlagen: HTTP {Status}", (int)response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var user = doc.RootElement.GetProperty("data").GetProperty("user");

            var streamNode = user.GetProperty("stream");
            var isLive = streamNode.ValueKind == JsonValueKind.Object;
            TwitchStreamInfo? streamInfo = null;
            if (isLive)
            {
                streamInfo = new TwitchStreamInfo(
                    streamNode.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                    streamNode.TryGetProperty("game", out var g) && g.ValueKind == JsonValueKind.Object
                        ? g.GetProperty("name").GetString()
                        : null);
            }

            var clips = new List<TwitchClipInfo>();
            foreach (var edge in user.GetProperty("clips").GetProperty("edges").EnumerateArray())
            {
                var node = edge.GetProperty("node");
                clips.Add(new TwitchClipInfo(
                    node.GetProperty("title").GetString() ?? "",
                    node.GetProperty("url").GetString() ?? "",
                    node.GetProperty("createdAt").GetDateTimeOffset(),
                    node.GetProperty("viewCount").GetInt32()));
            }

            return new TwitchSnapshot(isLive, streamInfo, clips);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Twitch-Abfrage fehlgeschlagen; letzte Karte bleibt stehen.");
            return null;
        }
    }

    private async Task RunHeartbeatAsync(ClientWebSocket socket, SemaphoreSlim sendLock,
        int intervalMs, Func<int> sequence, CancellationToken ct)
    {
        // Discord verlangt den ersten Heartbeat nach einer zufälligen Teilmenge des Intervalls.
        var firstDelay = Random.Shared.Next(0, intervalMs);
        await Task.Delay(firstDelay, ct);
        while (!ct.IsCancellationRequested)
        {
            await SendHeartbeatAsync(socket, sendLock, sequence(), ct);
            await Task.Delay(intervalMs, ct);
        }
    }

    private static Task SendHeartbeatAsync(ClientWebSocket socket, SemaphoreSlim sendLock, int sequence, CancellationToken ct) =>
        SendAsync(socket, sendLock, new JsonObject { ["op"] = 1, ["d"] = sequence }, ct);

    private static async Task SendAsync(ClientWebSocket socket, SemaphoreSlim sendLock, JsonNode payload, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(payload.ToJsonString());
        await sendLock.WaitAsync(ct);
        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
        }
        finally
        {
            sendLock.Release();
        }
    }

    private static async Task<JsonNode?> ReceiveJsonAsync(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[8 * 1024];
        using var message = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) break;
            if (message.Length > 1024 * 1024) throw new InvalidOperationException("Gateway-Frame zu groß.");
        }

        return JsonNode.Parse(message.ToArray());
    }
}
