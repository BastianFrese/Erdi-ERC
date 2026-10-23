namespace Erdi_ERC.Services;

/// <summary>Ein Stream, wie ihn die Twitch-Abfrage liefert.</summary>
public sealed record TwitchStreamInfo(string Title, string? GameName);

/// <summary>Ein Clip mit den Feldern, die auf der Discord-Karte Platz haben.</summary>
public sealed record TwitchClipInfo(string Title, string Url, DateTimeOffset CreatedAt, int ViewCount);

/// <summary>Ergebnis einer Twitch-Abfrage. <see cref="NewestClips"/> ist nach Datum absteigend.</summary>
public sealed record TwitchSnapshot(bool IsLive, TwitchStreamInfo? Stream, IReadOnlyList<TwitchClipInfo> NewestClips);

/// <summary>
/// Eine Discord-Aktivität, fertig zum Senden über das Gateway (Opcode 3).
/// <see cref="Type"/>: 1 = Streaming (einziger Typ mit klickbarem Link), 3 = Watching.
/// </summary>
public sealed record BotPresence(int Type, string Name, string? Url, string? Details, string? State)
{
    public const int Streaming = 1;
    public const int Watching = 3;
}

/// <summary>
/// Entscheidet, was auf der Bot-Karte steht. Reine Rechnung, ohne Netz und ohne Zeitbezug,
/// damit sie sich prüfen lässt: live → Streaming-Karte, sonst rotieren die neuesten Clips,
/// ohne Daten → fester Fallback. Rotiert wird über einen Index, nicht über eine Uhr.
/// </summary>
public static class BotPresencePlanner
{
    public const string Fallback = "Schaut zu: ERC Sim Racing";

    /// <summary>Discord kürzt Namen ab 128 Zeichen selbst — wir schneiden vorher sauber.</summary>
    private const int MaxNameLength = 128;

    public static BotPresence Build(string twitchChannel, TwitchSnapshot? snapshot, int rotationIndex)
    {
        var channelUrl = $"https://www.twitch.tv/{twitchChannel}";

        if (snapshot?.IsLive == true && snapshot.Stream is { } stream)
        {
            return new BotPresence(
                BotPresence.Streaming,
                Trim(string.IsNullOrWhiteSpace(stream.Title) ? "Live auf Twitch" : stream.Title),
                channelUrl,
                string.IsNullOrWhiteSpace(stream.GameName) ? "ERC Sim Racing" : Trim(stream.GameName),
                "Live auf Twitch");
        }

        if (snapshot?.NewestClips is { Count: > 0 } clips)
        {
            var index = ((rotationIndex % clips.Count) + clips.Count) % clips.Count;
            var clip = clips[index];
            return new BotPresence(
                BotPresence.Watching,
                Trim("Clip: " + clip.Title),
                null,
                Trim(clip.Title),
                $"{clip.ViewCount} Aufrufe · {clip.CreatedAt.ToLocalTime():dd.MM.}");
        }

        return new BotPresence(BotPresence.Watching, Fallback, null, null, null);
    }

    private static string Trim(string text) =>
        text.Length <= MaxNameLength ? text : text[..(MaxNameLength - 1)] + "…";
}
