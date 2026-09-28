using Erdi_ERC.Helpers;
using Xunit;

namespace Erdi_ERC.Tests.Helpers;

/// <summary>
/// Video-Links aus Events (<c>RealLifeEvent.YouTubeUrl</c>) und Highlights
/// (<c>RaceHighlightClip.Url</c>) werden öffentlich als iframe eingebettet. Die Umwandlung
/// lag vorher doppelt in den Views (<c>EventDetail.cshtml</c> inline, <c>Events.cshtml</c>
/// als <c>ToEmbedUrl</c>) und kannte nur YouTube; Razor erreichen die Tests nicht, deshalb
/// liegt die Regel jetzt hier — gleiche Begründung wie bei
/// <see cref="ConstructorTeamHelper"/>.
///
/// Zwei Zusagen, die die Tests festhalten: fremde Hosts werden nie eingebettet (der Link
/// kommt aus einer Admin-Eingabe bzw. aus einer Community-Einsendung), und Twitch-Clips
/// ohne verwertbaren Request-Host liefern kein Embed — der Player verlangt einen
/// <c>parent</c>, sonst zeigt er nur einen Fehlerkasten.
/// </summary>
public class VideoEmbedHelperTests
{
    private const string Host = "erdi-erc.de";

    // ---------- YouTube ----------

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=42")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/live/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PLabc123")]
    [InlineData("  https://www.youtube.com/watch?v=dQw4w9WgXcQ  ")]
    public void Resolve_youtubeShapes_normalizesToEmbed(string url)
    {
        AssertEmbed(url, VideoPlatform.YouTube, "https://www.youtube.com/embed/dQw4w9WgXcQ");
    }

    [Fact]
    public void Resolve_youtube_needsNoRequestHost()
    {
        // Anders als Twitch: der YouTube-Player braucht keinen parent-Parameter.
        AssertEmbed("https://youtu.be/dQw4w9WgXcQ", VideoPlatform.YouTube,
            "https://www.youtube.com/embed/dQw4w9WgXcQ", host: null);
    }

    [Theory]
    [InlineData("https://www.youtube.com/playlist?list=PLabc123")]
    [InlineData("https://www.youtube.com/watch?v=")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ\"onload=\"alert(1)")]
    [InlineData("https://www.youtube.com/embed/../../evil")]
    public void Resolve_youtubeWithoutUsableVideoId_hasNoEmbed(string url)
    {
        AssertNoEmbed(url);
    }

    [Fact]
    public void Resolve_youTubeChannelPage_isRecognizedButNotEmbeddable()
    {
        // Erkannt (der Admin darf den Link eintragen), aber nicht einbettbar: die View
        // fällt dann auf einen normalen Link zurück statt einen leeren Player zu zeigen.
        Assert.Equal(VideoPlatform.YouTube, VideoEmbedHelper.DetectPlatform("https://www.youtube.com/@erdi10"));
        AssertNoEmbed("https://www.youtube.com/@erdi10");
    }

    // ---------- Twitch ----------

    [Theory]
    [InlineData("https://clips.twitch.tv/AwkwardHelplessSalamanderSwiftRage")]
    [InlineData("https://clips.twitch.tv/AwkwardHelplessSalamanderSwiftRage?tt_content=url")]
    [InlineData("https://www.twitch.tv/erdi10/clip/AwkwardHelplessSalamanderSwiftRage")]
    [InlineData("https://twitch.tv/erdi10/clip/AwkwardHelplessSalamanderSwiftRage")]
    [InlineData("https://m.twitch.tv/erdi10/clip/AwkwardHelplessSalamanderSwiftRage")]
    [InlineData("https://player.twitch.tv/?clip=AwkwardHelplessSalamanderSwiftRage")]
    public void Resolve_twitchClipShapes_embedsWithParent(string url)
    {
        AssertEmbed(url, VideoPlatform.Twitch,
            "https://clips.twitch.tv/embed?clip=AwkwardHelplessSalamanderSwiftRage&parent=erdi-erc.de&parent=localhost");
    }

    [Theory]
    [InlineData("https://www.twitch.tv/videos/1234567890")]
    [InlineData("https://player.twitch.tv/?video=1234567890")]
    public void Resolve_twitchVodShapes_embedsWithParent(string url)
    {
        AssertEmbed(url, VideoPlatform.Twitch,
            "https://player.twitch.tv/?video=1234567890&parent=erdi-erc.de&parent=localhost");
    }

    [Theory]
    [InlineData("https://www.twitch.tv/videos/keine-zahl")]
    [InlineData("https://www.twitch.tv/erdi10/clip/")]
    [InlineData("https://www.twitch.tv/erdi10")]
    [InlineData("https://clips.twitch.tv/")]
    public void Resolve_twitchWithoutClipOrVideoId_hasNoEmbed(string url)
    {
        AssertNoEmbed(url);
    }

    [Fact]
    public void Resolve_twitchChannelPage_isRecognizedButNotEmbeddable()
    {
        Assert.Equal(VideoPlatform.Twitch, VideoEmbedHelper.DetectPlatform("https://www.twitch.tv/erdi10"));
        AssertNoEmbed("https://www.twitch.tv/erdi10");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_twitchClip_withoutRequestHost_hasNoEmbed(string? host)
    {
        // Ohne parent zeigt der Twitch-Player nur „Whoops, something went wrong" — dann ist
        // der Link die bessere Ausgabe.
        var embed = VideoEmbedHelper.Resolve("https://clips.twitch.tv/AwkwardHelplessSalamanderSwiftRage", host);
        Assert.False(embed.HasValue);
    }

    [Theory]
    [InlineData("erdi-erc.de\"onload=\"alert(1)")]
    [InlineData("erdi-erc.de/videos")]
    [InlineData("erdi-erc.de&parent=evil.example")]
    [InlineData("erdi-erc.de:5000")]
    [InlineData("-")]
    public void Resolve_twitchClip_withUnusableRequestHost_hasNoEmbed(string host)
    {
        // Der Request-Host kommt aus dem Host-Header und ist damit angreifbar; er landet roh
        // in einem src-Attribut. Alles außer einem Hostnamen wird verworfen.
        var embed = VideoEmbedHelper.Resolve("https://clips.twitch.tv/AwkwardHelplessSalamanderSwiftRage", host);
        Assert.False(embed.HasValue);
    }

    [Fact]
    public void Resolve_twitchClip_uppercaseHostAndTrailingDot_isNormalized()
    {
        AssertEmbed("https://clips.twitch.tv/AwkwardHelplessSalamanderSwiftRage", VideoPlatform.Twitch,
            "https://clips.twitch.tv/embed?clip=AwkwardHelplessSalamanderSwiftRage&parent=erdi-erc.de&parent=localhost",
            host: "ERDI-ERC.DE.");
    }

    [Fact]
    public void Resolve_twitchClip_onLocalhost_addsParentOnlyOnce()
    {
        AssertEmbed("https://clips.twitch.tv/AwkwardHelplessSalamanderSwiftRage", VideoPlatform.Twitch,
            "https://clips.twitch.tv/embed?clip=AwkwardHelplessSalamanderSwiftRage&parent=localhost",
            host: "localhost");
    }

    // ---------- Fremde Hosts und Nicht-URLs ----------

    [Theory]
    [InlineData("https://evilyoutube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com.evil.example/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://twitch.tv.evil.example/erdi10/clip/AwkwardHelplessSalamanderSwiftRage")]
    [InlineData("https://eviltwitch.tv/erdi10/clip/AwkwardHelplessSalamanderSwiftRage")]
    public void Resolve_lookalikeHosts_areNotEmbedded(string url)
    {
        // `Contains("youtube.com")` hätte hier zugeschlagen — exakter Host oder Subdomain.
        // Eine echte Subdomain (clips.twitch.tv, m.twitch.tv) bleibt dagegen erlaubt: sie
        // liegt unter der Kontrolle des Anbieters.
        AssertNoEmbed(url);
        Assert.Null(VideoEmbedHelper.DetectPlatform(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("kein link")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("//youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://vimeo.com/12345")]
    public void Resolve_notAWebVideoUrl_hasNoEmbed(string? url)
    {
        AssertNoEmbed(url);
    }

    // ---------- Grenzfälle der ID-Erkennung ----------

    [Theory]
    [InlineData("https://youtu.be/watch")]
    [InlineData("https://youtu.be/embed")]
    [InlineData("https://youtu.be/about")]
    [InlineData("https://youtu.be/dQw4w9WgXc")]
    [InlineData("https://youtu.be/dQw4w9WgXcQQ")]
    [InlineData("https://www.youtube.com/embed/videoseries?list=PLabc123")]
    [InlineData("https://www.youtube.com/embed/watch")]
    public void Resolve_youtubeWithoutRealVideoId_hasNoEmbed(string url)
    {
        // `videoseries` ist der Playlist-Platzhalter aus dem YouTube-Dialog und mit seinen
        // 11 Buchstaben genauso lang wie eine echte ID — nur die Längenprüfung würde ihn
        // durchlassen und der `list`-Parameter ginge verloren. Ergebnis wäre ein Player mit
        // „Video unavailable" statt des Link-Rückfalls.
        AssertNoEmbed(url);
    }

    [Fact]
    public void Resolve_youtubePlaylist_isRecognizedButNotEmbeddable()
    {
        // Erkannt bleiben muss sie: der Admin darf den Link eintragen, die View zeigt ihn
        // dann als Link statt als leeren Player.
        const string playlist = "https://www.youtube.com/embed/videoseries?list=PLabc123";
        Assert.Equal(VideoPlatform.YouTube, VideoEmbedHelper.DetectPlatform(playlist));
        AssertNoEmbed(playlist);
    }

    // ---------- Abgleich mit der CSP ----------

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ")]
    [InlineData("https://clips.twitch.tv/AwkwardHelplessSalamanderSwiftRage")]
    [InlineData("https://www.twitch.tv/erdi10/clip/AwkwardHelplessSalamanderSwiftRage")]
    [InlineData("https://www.twitch.tv/videos/1234567890")]
    public void Resolve_embedHostIsAllowedByTheFrameSrcCsp(string url)
    {
        // Ein Host, den Resolve baut, den die frame-src-Direktive aber nicht erlaubt, wird
        // vom Browser still blockiert: leerer Player, kein Fehler im App-Log. Genau das war
        // bei clips.twitch.tv der Fall — der Helper baute darauf, die CSP kannte den Host nicht.
        var embed = VideoEmbedHelper.Resolve(url, Host);
        Assert.True(embed.HasValue, $"Kein Embed für: {url}");

        var origin = new Uri(embed!.Value.Url).GetLeftPart(UriPartial.Authority);
        Assert.Contains($" {origin} ", $" {VideoEmbedHelper.FrameSrcDirective()} ");
    }

    [Fact]
    public void FrameSrcDirective_containsEveryDeclaredOrigin()
    {
        var directive = VideoEmbedHelper.FrameSrcDirective();

        Assert.StartsWith("'self' ", directive);
        foreach (var origin in VideoEmbedHelper.FrameOrigins)
        {
            Assert.Contains($" {origin} ", $" {directive} ");
        }
    }

    // ---------- Host aus dem Request ----------

    [Theory]
    [InlineData("ERDI-ERC.DE.", "erdi-erc.de")]
    [InlineData("localhost", "localhost")]
    [InlineData("erdi-erc.de&parent=evil.example", null)]
    [InlineData("erdi-erc.de/videos", null)]
    [InlineData("erdi-erc.de:5000", null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public void NormalizedHostOrNull_returnsHostOrNothing(string host, string? expected)
    {
        // Views/Stats/Erdi10.cshtml nutzt das für seine Twitch-parent-Parameter: der Wert
        // kommt aus dem Host-Header, der ohne Prüfung Parameter in die Player-URL hängen kann.
        Assert.Equal(expected, VideoEmbedHelper.NormalizedHostOrNull(host));
    }

    // ---------- Anzeigename ----------

    [Fact]
    public void DisplayName_namesTheProviderAndNeverLies()
    {
        Assert.Equal("YouTube", VideoEmbedHelper.DisplayName(VideoPlatform.YouTube));
        Assert.Equal("Twitch", VideoEmbedHelper.DisplayName(VideoPlatform.Twitch));
        // Ein später ergänzter Anbieter darf nicht als YouTube erscheinen.
        Assert.Equal("99", VideoEmbedHelper.DisplayName((VideoPlatform)99));
    }

    // ---------- Links, die nur verlinkt werden ----------

    [Theory]
    [InlineData("javascript:fetch('/x')")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("kein link")]
    [InlineData("youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData(null)]
    [InlineData("")]
    public void SafeLinkOrNull_nonHttpLink_isRejected(string? url)
    {
        // Diese Werte landen als `href` auf einer öffentlichen Seite ("Clip öffnen"). Ein
        // `javascript:`-Link würde dort beim Klick im Origin der Seite ausgeführt; nur
        // http(s) wird deshalb durchgelassen.
        Assert.Null(VideoEmbedHelper.SafeLinkOrNull(url));
    }

    [Theory]
    [InlineData("https://clips.twitch.tv/AwkwardHelplessSalamanderSwiftRage")]
    [InlineData("https://vimeo.com/12345")]
    [InlineData("https://www.youtube.com/@erdi10")]
    public void SafeLinkOrNull_webLink_isReturnedUnchanged(string url)
    {
        // Bewusst NICHT auf bekannte Anbieter eingeschränkt: verlinkt werden darf jeder
        // http(s)-Link, eingebettet nur ein erkannter Video-Link.
        Assert.Equal(url, VideoEmbedHelper.SafeLinkOrNull(url));
    }

    [Fact]
    public void SafeLinkOrNull_trimsSurroundingWhitespace()
    {
        Assert.Equal("https://clips.twitch.tv/abc", VideoEmbedHelper.SafeLinkOrNull("  https://clips.twitch.tv/abc  "));
    }

    // ---------- Helpers ----------

    private static VideoEmbed AssertEmbed(string? url, VideoPlatform platform, string expectedUrl, string? host = Host)
    {
        var embed = VideoEmbedHelper.Resolve(url, host);

        Assert.True(embed.HasValue, $"Kein Embed für: {url} (Host: {host})");
        Assert.Equal(platform, embed!.Value.Platform);
        Assert.Equal(expectedUrl, embed.Value.Url);
        return embed.Value;
    }

    private static void AssertNoEmbed(string? url, string? host = Host)
    {
        Assert.False(VideoEmbedHelper.Resolve(url, host).HasValue, $"Unerwartetes Embed für: {url}");
    }
}
