using System.Text.RegularExpressions;

namespace Erdi_ERC.Helpers;

/// <summary>
/// Liest die rohen Zeilen des Twitch-IRC-WebSockets. Bewusst nur das, was das
/// Voting braucht: normale PRIVMSG-Zeilen, PING-Keepalives und den Vote-Befehl.
/// </summary>
public static class TwitchIrcParser
{
    public sealed record ChatMessage(string Login, string DisplayName, string UserId, string Text);

    private static readonly Regex VoteCommand =
        new(@"^!(?:vote|dotd)\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Parst eine <c>PRIVMSG</c>-Zeile; null für alles andere.</summary>
    public static ChatMessage? ParsePrivMsg(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.Contains(" PRIVMSG ", StringComparison.Ordinal))
        {
            return null;
        }

        line = line.TrimEnd('\r', '\n');

        var tags = new Dictionary<string, string>(StringComparer.Ordinal);
        var rest = line;
        if (rest.StartsWith('@'))
        {
            var space = rest.IndexOf(' ');
            if (space < 0)
            {
                return null;
            }

            foreach (var pair in rest[1..space].Split(';'))
            {
                var equals = pair.IndexOf('=');
                if (equals > 0)
                {
                    tags[pair[..equals]] = pair[(equals + 1)..];
                }
            }

            rest = rest[(space + 1)..];
        }

        if (!rest.StartsWith(':'))
        {
            return null;
        }

        var firstSpace = rest.IndexOf(' ');
        if (firstSpace < 0)
        {
            return null;
        }

        var prefix = rest[1..firstSpace];
        var bang = prefix.IndexOf('!');
        var login = bang > 0 ? prefix[..bang] : prefix;

        var textStart = rest.IndexOf(" :", StringComparison.Ordinal);
        if (textStart < 0)
        {
            return null;
        }

        var text = rest[(textStart + 2)..];
        var display = tags.TryGetValue("display-name", out var displayName)
                      && !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : login;
        var userId = tags.TryGetValue("user-id", out var id) ? id : string.Empty;

        return new ChatMessage(login, display, userId, text);
    }

    /// <summary>Twitch sendet periodisch <c>PING :tmi.twitch.tv</c>; der Client muss mit PONG antworten.</summary>
    public static bool IsPing(string line)
        => line.TrimStart().StartsWith("PING ", StringComparison.Ordinal);

    /// <summary>Liefert das Ziel hinter <c>!vote</c> bzw. <c>!dotd</c>, sonst null.</summary>
    public static string? ExtractVoteTarget(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var match = VoteCommand.Match(message.Trim());
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
