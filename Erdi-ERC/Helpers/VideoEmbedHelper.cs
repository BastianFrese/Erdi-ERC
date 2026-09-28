using Microsoft.AspNetCore.WebUtilities;

namespace Erdi_ERC.Helpers;

/// <summary>Anbieter, dessen Player sich als iframe einbetten lässt.</summary>
public enum VideoPlatform
{
    YouTube,
    Twitch
}

/// <summary>Einbettbares Video: Anbieter und fertige iframe-URL.</summary>
public readonly record struct VideoEmbed(VideoPlatform Platform, string Url);

/// <summary>
/// Wandelt eingetragene Video-Links in Player-URLs um. Einzige Quelle für diese Regel —
/// vorher lag sie doppelt in den Views (<c>Views/Community/EventDetail.cshtml</c> inline,
/// <c>Views/Community/Events.cshtml</c> als <c>ToEmbedUrl</c>) und kannte nur YouTube.
///
/// In der DB steht immer die <em>Original-URL</em>, nie die Embed-URL — sonst wäre später
/// nicht mehr erkennbar, was der Admin eingetragen hat.
///
/// Eingebettet werden nur Links bekannter Anbieter. Alles andere (Vimeo, Kanal-URLs,
/// Freitext) liefert <c>null</c>; die Views zeigen dann einen normalen Link statt eines
/// leeren Players. <see cref="DetectPlatform"/> erkennt auch die nicht einbettbaren Links
/// derselben Anbieter — das ist die Prüfung für die Admin-Eingabe.
/// </summary>
public static class VideoEmbedHelper
{
    private const string YouTubeEmbedPrefix = "https://www.youtube.com/embed/";
    private const string TwitchClipEmbedPrefix = "https://clips.twitch.tv/embed?clip=";
    private const string TwitchVideoEmbedPrefix = "https://player.twitch.tv/?video=";

    /// <summary>
    /// Der Platzhalter aus dem Playlist-Player <c>/embed/videoseries</c>. Er sieht wie eine
    /// Video-ID aus (sogar mit 11 Zeichen) und ergäbe als solche ein Embed ohne
    /// <c>list</c>-Parameter: einen Player mit „Video unavailable" statt des Link-Rückfalls.
    /// </summary>
    private const string YouTubePlaylistPlaceholder = "videoseries";

    /// <summary>YouTube-Video-IDs sind immer genau 11 Zeichen lang.</summary>
    private const int YouTubeIdLength = 11;

    /// <summary>
    /// Zweiter <c>parent</c>-Wert für die lokale Entwicklung. Twitch verlangt für jeden Host,
    /// unter dem der Player läuft, einen eigenen Parameter; ein Embed mit falschem oder
    /// fehlendem <c>parent</c> zeigt nur einen Fehlerkasten.
    /// </summary>
    private const string DevParent = "localhost";

    /// <summary>Längster zulässiger DNS-Name.</summary>
    private const int MaxHostLength = 253;

    /// <summary>Kürzeste ID, die noch als Video-/Clip-Kennung durchgeht.</summary>
    private const int MinTokenLength = 5;

    /// <summary>Obergrenze gegen Unsinn in einer Admin-Eingabe oder Community-Einsendung.</summary>
    private const int MaxTokenLength = 128;

    /// <summary>
    /// Origins, die als <c>iframe</c>-Quelle vorkommen können: die Player, die
    /// <see cref="Resolve"/> baut, plus die fest in den Views eingetragenen Embeds
    /// (Twitch-Kanal und -Chat, Discord-Widget, YouTube-nocookie).
    /// </summary>
    public static readonly string[] FrameOrigins =
    [
        "https://clips.twitch.tv",
        "https://player.twitch.tv",
        "https://www.twitch.tv",
        "https://embed.twitch.tv",
        "https://www.youtube.com",
        "https://www.youtube-nocookie.com",
        "https://discord.com"
    ];

    /// <summary>
    /// Wert der <c>frame-src</c>-Direktive der App-CSP. Steht hier und nicht in
    /// <c>Program.cs</c>, damit die Tests Helper und CSP gegeneinander prüfen können: ein
    /// Host, den <see cref="Resolve"/> erzeugt, den die CSP aber nicht erlaubt, wird vom
    /// Browser still blockiert — leerer Player, kein Fehler im App-Log. Genau das passierte
    /// bei <c>clips.twitch.tv</c>.
    /// </summary>
    public static string FrameSrcDirective() => string.Join(' ', FrameOrigins.Prepend("'self'"));

    /// <summary>
    /// Anzeigename des Anbieters für Beschriftungen und <c>title</c>-Attribute.
    /// </summary>
    public static string DisplayName(VideoPlatform platform) => platform switch
    {
        VideoPlatform.YouTube => "YouTube",
        VideoPlatform.Twitch => "Twitch",
        // Kein stiller Fallback auf „YouTube": ein später ergänzter Anbieter soll sich
        // nicht als YouTube ausgeben.
        _ => platform.ToString()
    };

    /// <summary>
    /// Der eingetragene Link, wenn er ein vollständiger http(s)-Link ist — sonst <c>null</c>.
    /// Für Stellen, die einen Link nur <em>verlinken</em> statt ihn einzubetten: ein
    /// <c>javascript:</c>- oder <c>data:</c>-Link ist kein Video, würde als <c>href</c> aber
    /// im Origin der Seite ausgeführt. Razor-Encoding hilft dagegen nicht — solche URLs
    /// enthalten keine Sonderzeichen, die entschärft werden könnten.
    /// </summary>
    public static string? SafeLinkOrNull(string? url) =>
        TryParseWebUrl(url, out _) ? url!.Trim() : null;

    /// <summary>
    /// Anbieter des Links oder <c>null</c>, wenn es kein Link eines bekannten Anbieters ist.
    /// Erkennt bewusst auch Kanal-, Playlist- und Profil-URLs, die sich nicht einbetten
    /// lassen: der Link darf eingetragen werden, die Anzeige fällt auf einen Link zurück.
    /// </summary>
    public static VideoPlatform? DetectPlatform(string? url) =>
        TryParseWebUrl(url, out var uri) ? Classify(uri) : null;

    /// <summary>
    /// Anbieter eines bereits geparsten Links — gemeinsame Quelle für
    /// <see cref="DetectPlatform"/> und <see cref="Resolve"/>, damit die Host-Erkennung nicht
    /// an zwei Stellen gepflegt werden muss (und beide gleich antworten).
    /// </summary>
    private static VideoPlatform? Classify(Uri uri) =>
        IsYouTubeHost(uri.Host) ? VideoPlatform.YouTube
        : IsHostOrSubdomain(uri.Host, "twitch.tv") ? VideoPlatform.Twitch
        : null;

    /// <summary>
    /// Fertige iframe-URL für <paramref name="url"/> oder <c>null</c>, wenn der Link nicht
    /// einbettbar ist. <paramref name="requestHost"/> ist der Host der laufenden Anfrage
    /// (<c>Context.Request.Host.Host</c>) — Twitch braucht ihn als <c>parent</c>.
    /// </summary>
    public static VideoEmbed? Resolve(string? url, string? requestHost)
    {
        if (!TryParseWebUrl(url, out var uri))
        {
            return null;
        }

        return Classify(uri) switch
        {
            VideoPlatform.YouTube => ExtractYouTubeId(uri) is { } id
                ? new VideoEmbed(VideoPlatform.YouTube, YouTubeEmbedPrefix + id)
                : null,
            VideoPlatform.Twitch => ResolveTwitch(uri, NormalizedHostOrNull(requestHost)),
            _ => null
        };
    }

    private static bool IsYouTubeHost(string host) =>
        IsHostOrSubdomain(host, "youtube.com") || IsHostOrSubdomain(host, "youtu.be");

    private static VideoEmbed? ResolveTwitch(Uri uri, string? host)
    {
        if (host is null)
        {
            return null;
        }

        var (clipSlug, videoId) = ExtractTwitchId(uri);

        if (clipSlug is not null)
        {
            return new VideoEmbed(VideoPlatform.Twitch, TwitchClipEmbedPrefix + clipSlug + ParentParams(host));
        }

        if (videoId is not null)
        {
            return new VideoEmbed(VideoPlatform.Twitch, TwitchVideoEmbedPrefix + videoId + ParentParams(host));
        }

        return null;
    }

    /// <summary>Video-ID einer YouTube-URL: <c>youtu.be/&lt;id&gt;</c>, <c>/embed|shorts|live/&lt;id&gt;</c>, <c>?v=&lt;id&gt;</c>.</summary>
    private static string? ExtractYouTubeId(Uri uri)
    {
        var segments = PathSegments(uri);

        if (IsHostOrSubdomain(uri.Host, "youtu.be"))
        {
            // Nur die ID-Form: `youtu.be/about` oder `youtu.be/watch` wären sonst „Videos"
            // und die View zeigte statt eines Links einen Player mit „Video unavailable".
            return YouTubeIdOrNull(segments.Length > 0 ? segments[0] : null);
        }

        if (segments.Length >= 2
            && (segments[0].Equals("embed", StringComparison.OrdinalIgnoreCase)
                || segments[0].Equals("shorts", StringComparison.OrdinalIgnoreCase)
                || segments[0].Equals("live", StringComparison.OrdinalIgnoreCase)))
        {
            return segments[1].Equals(YouTubePlaylistPlaceholder, StringComparison.OrdinalIgnoreCase)
                ? null
                : YouTubeIdOrNull(segments[1]);
        }

        var query = QueryHelpers.ParseQuery(uri.Query);
        return query.TryGetValue("v", out var v) ? SafeTokenOrNull(v.ToString()) : null;
    }

    /// <summary>Clip-Kennung bzw. VOD-Nummer einer Twitch-URL (genau eine der beiden ist gesetzt).</summary>
    private static (string? ClipSlug, string? VideoId) ExtractTwitchId(Uri uri)
    {
        var segments = PathSegments(uri);
        var query = QueryHelpers.ParseQuery(uri.Query);

        // clips.twitch.tv/<slug>
        if (IsHostOrSubdomain(uri.Host, "clips.twitch.tv"))
        {
            return (SafeTokenOrNull(segments.Length > 0 ? segments[0] : null), null);
        }

        // player.twitch.tv/?clip=<slug> bzw. ?video=<id> — der Admin darf eine fertige Embed-URL eintragen.
        if (IsHostOrSubdomain(uri.Host, "player.twitch.tv"))
        {
            if (query.TryGetValue("clip", out var clip))
            {
                return (SafeTokenOrNull(clip.ToString()), null);
            }

            return (null, query.TryGetValue("video", out var video) ? DigitsOrNull(video.ToString()) : null);
        }

        // twitch.tv/videos/<nummer>
        if (segments.Length >= 2 && segments[0].Equals("videos", StringComparison.OrdinalIgnoreCase))
        {
            return (null, DigitsOrNull(segments[1]));
        }

        // twitch.tv/<kanal>/clip/<slug>
        var clipIndex = Array.FindIndex(segments, s => s.Equals("clip", StringComparison.OrdinalIgnoreCase));
        return clipIndex >= 0 && clipIndex + 1 < segments.Length
            ? (SafeTokenOrNull(segments[clipIndex + 1]), null)
            : (null, null);
    }

    /// <summary><c>&amp;parent=…</c> für den Anfrage-Host und — außer auf localhost — für die lokale Entwicklung.</summary>
    private static string ParentParams(string host) =>
        string.Concat((host.Equals(DevParent, StringComparison.OrdinalIgnoreCase)
                ? new[] { host }
                : new[] { host, DevParent })
            .Select(parent => "&parent=" + parent));

    /// <summary>
    /// Prüft und normalisiert den Host aus dem Request. Er stammt aus dem Host-Header, ist
    /// damit angreifbar und landet roh in einem <c>src</c>- bzw. <c>href</c>-Attribut — alles
    /// außer einem Hostnamen wird deshalb verworfen. Twitch vergleicht <c>parent</c> gegen den
    /// Hostnamen, Schreibweise und ein abschließender Punkt dürfen also keine Rolle spielen.
    ///
    /// Public, weil <c>Views/Stats/Erdi10.cshtml</c> denselben Wert für seine Twitch-iframes
    /// braucht und ihn nicht selbst zusammensetzen soll.
    /// </summary>
    public static string? NormalizedHostOrNull(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var normalized = host.Trim().Trim('.').ToLowerInvariant();
        if (normalized.Length is 0 or > MaxHostLength)
        {
            return null;
        }

        foreach (var c in normalized)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '.' && c != '-')
            {
                return null;
            }
        }

        return normalized.StartsWith('-') || normalized.EndsWith('-') ? null : normalized;
    }

    /// <summary>Exakter Host oder Subdomain — <c>evilyoutube.com</c> ist kein YouTube-Host.</summary>
    private static bool IsHostOrSubdomain(string host, string domain) =>
        host.Equals(domain, StringComparison.OrdinalIgnoreCase)
        || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);

    private static bool TryParseWebUrl(string? url, out Uri uri)
    {
        uri = null!;

        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        // Auch `javascript:` und `data:` bestehen TryCreate — nur http(s) ist ein Video-Link.
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    private static string[] PathSegments(Uri uri) =>
        uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Buchstaben, Ziffern, <c>-</c> und <c>_</c> in plausibler Länge — sonst <c>null</c>.</summary>
    private static string? SafeTokenOrNull(string? value)
    {
        if (value is null || value.Length is < MinTokenLength or > MaxTokenLength)
        {
            return null;
        }

        foreach (var c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')
            {
                return null;
            }
        }

        return value;
    }

    /// <summary>YouTube-Video-ID: genau 11 Zeichen aus <c>[A-Za-z0-9_-]</c> — sonst <c>null</c>.</summary>
    private static string? YouTubeIdOrNull(string? value) =>
        value is not null && value.Length == YouTubeIdLength ? SafeTokenOrNull(value) : null;

    private static string? DigitsOrNull(string? value)
    {
        if (value is null || value.Length is 0 or > 20)
        {
            return null;
        }

        foreach (var c in value)
        {
            if (!char.IsAsciiDigit(c))
            {
                return null;
            }
        }

        return value;
    }
}
