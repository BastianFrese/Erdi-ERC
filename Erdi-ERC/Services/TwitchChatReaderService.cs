using System.Net.WebSockets;
using System.Text;
using Erdi_ERC.Helpers;
using Erdi_ERC.Options;
using Microsoft.Extensions.Options;

namespace Erdi_ERC.Services;

/// <summary>
/// Liest den öffentlichen Twitch-Chat anonym über IRC-over-WebSocket. Kein OAuth,
/// keine Chat-Antworten — der Leser reicht nur <c>!vote &lt;name&gt;</c> an den
/// Vote-Service weiter. Außerhalb von Production bleibt er aus, damit lokale
/// Läufe und Staging nicht am echten Kanal hängen.
/// </summary>
public sealed class TwitchChatReaderService : BackgroundService
{
    private static readonly Uri TwitchIrcUri = new("wss://irc-ws.chat.twitch.tv:443");
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopes;
    private readonly ApplicationOptions _app;
    private readonly DotdOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<TwitchChatReaderService> _logger;

    // Twitch erlaubt einem normalen Account ~20 Nachrichten pro 30 s. Zwei Sekunden
    // Abstand halten den Bot darunter; einzelne Bestätigungen fallen dann aus.
    private DateTime _lastReplyAt = DateTime.MinValue;

    private bool TryReserveReply()
    {
        var now = DateTime.UtcNow;
        if (now - _lastReplyAt < TimeSpan.FromSeconds(2))
        {
            return false;
        }

        _lastReplyAt = now;
        return true;
    }

    public TwitchChatReaderService(
        IServiceScopeFactory scopes,
        IOptions<ApplicationOptions> app,
        IOptions<DotdOptions> options,
        IHostEnvironment environment,
        ILogger<TwitchChatReaderService> logger)
    {
        _scopes = scopes;
        _app = app.Value;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_environment.IsProduction() && !_options.ConnectOutsideProduction)
        {
            _logger.LogInformation(
                "Twitch-Chat-Leser ist außerhalb von Production deaktiviert.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunConnectionAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex, "Twitch-Chat-Verbindung abgebrochen; neuer Versuch in 5 s.");
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

    private async Task RunConnectionAsync(CancellationToken ct)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(TwitchIrcUri, ct);

        var channel = _app.TwitchChannel.Trim().TrimStart('#');
        var canWrite = _options.CanWrite;
        var nickname = canWrite
            ? _options.BotUsername.Trim()
            : $"justinfan{Random.Shared.Next(10000, 99999)}";

        var token = _options.BotOAuthToken.Trim();
        if (token.StartsWith("oauth:", StringComparison.OrdinalIgnoreCase))
        {
            token = token[6..];
        }

        await SendAsync(socket,
            $"CAP REQ :twitch.tv/tags\r\n" +
            $"PASS {(canWrite ? $"oauth:{token}" : "SCHMOOPIIE")}\r\n" +
            $"NICK {nickname}\r\n" +
            $"JOIN #{channel}\r\n",
            ct);

        _logger.LogInformation(
            "Twitch-Chat verbunden: #{Channel} ({Mode})",
            channel,
            canWrite ? "Bot mit Schreibrecht" : "anonym, nur lesen");

        var buffer = new byte[8192];
        var pending = new StringBuilder();

        while (!ct.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            pending.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

            while (TryTakeLine(pending, out var line))
            {
                if (TwitchIrcParser.IsPing(line))
                {
                    await SendAsync(socket, "PONG :tmi.twitch.tv\r\n", ct);
                    continue;
                }

                var message = TwitchIrcParser.ParsePrivMsg(line);
                if (message is null)
                {
                    continue;
                }

                var target = TwitchIrcParser.ExtractVoteTarget(message.Text);
                if (target is null)
                {
                    continue;
                }

                using var scope = _scopes.CreateScope();
                var voteService = scope.ServiceProvider.GetRequiredService<IDotdVoteService>();
                var voterKey = string.IsNullOrWhiteSpace(message.UserId)
                    ? message.Login
                    : message.UserId;
                var matched = await voteService.RegisterVoteAsync(voterKey, target, ct);

                if (matched is not null && canWrite && TryReserveReply())
                {
                    var who = string.IsNullOrWhiteSpace(message.DisplayName)
                        ? message.Login
                        : message.DisplayName;
                    await SendAsync(socket,
                        $"PRIVMSG #{channel} :@{who} Vote für " +
                        $"{DriverNameHelper.Display(matched)} gezählt.\r\n",
                        ct);
                }
            }
        }
    }

    private static bool TryTakeLine(StringBuilder pending, out string line)
    {
        var newline = pending.ToString().IndexOf('\n');
        if (newline < 0)
        {
            line = string.Empty;
            return false;
        }

        line = pending.ToString(0, newline).TrimEnd('\r');
        pending.Remove(0, newline + 1);
        return true;
    }

    private static Task SendAsync(
        ClientWebSocket socket, string payload, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        return socket.SendAsync(
            bytes, WebSocketMessageType.Text, true, ct);
    }
}
