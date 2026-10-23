using Erdi_ERC.Services;
using Xunit;

namespace Erdi_ERC.Tests.Services;

/// <summary>
/// Was auf der Bot-Karte steht: Live-Stream vor Clips, Clips rotieren, ohne Daten der
/// feste Fallback. Der Streaming-Typ ist der einzige mit klickbarem Link — er darf nur
/// bei echtem Live-Status gesetzt werden.
/// </summary>
public class BotPresencePlannerTests
{
    private const string Channel = "erdi10";

    private static TwitchSnapshot Live(params TwitchClipInfo[] clips) =>
        new(true, new TwitchStreamInfo("ERC Division ROOKIES - 50% Mexiko", "EA SPORTS F1 25"), clips);

    private static TwitchClipInfo Clip(string title, int views = 10, int day = 19) =>
        new(title, "https://www.twitch.tv/erdi10/clip/x", new DateTimeOffset(2026, 10, day, 18, 0, 0, TimeSpan.Zero), views);

    [Fact]
    public void Live_Stream_hat_Vorrang_und_ist_klickbar()
    {
        var presence = BotPresencePlanner.Build(Channel, Live(Clip("egal")), rotationIndex: 0);

        Assert.Equal(BotPresence.Streaming, presence.Type);
        Assert.Equal("https://www.twitch.tv/erdi10", presence.Url);
        Assert.Equal("ERC Division ROOKIES - 50% Mexiko", presence.Name);
    }

    [Fact]
    public void Clip_bekommt_den_Watching_Typ_ohne_Link()
    {
        var snapshot = new TwitchSnapshot(false, null, new[] { Clip("Schumi vs. Dome Kollision", 54) });

        var presence = BotPresencePlanner.Build(Channel, snapshot, rotationIndex: 0);

        Assert.Equal(BotPresence.Watching, presence.Type);
        Assert.Null(presence.Url);
        Assert.Equal("Clip: Schumi vs. Dome Kollision", presence.Name);
        Assert.Contains("54 Aufrufe", presence.State);
    }

    [Fact]
    public void Rotation_laeuft_ueber_die_Clip_Liste_und_bleibt_im_Bereich()
    {
        var snapshot = new TwitchSnapshot(false, null, new[] { Clip("eins"), Clip("zwei"), Clip("drei") });

        Assert.Equal("Clip: eins", BotPresencePlanner.Build(Channel, snapshot, 0).Name);
        Assert.Equal("Clip: zwei", BotPresencePlanner.Build(Channel, snapshot, 1).Name);
        Assert.Equal("Clip: drei", BotPresencePlanner.Build(Channel, snapshot, 2).Name);
        Assert.Equal("Clip: eins", BotPresencePlanner.Build(Channel, snapshot, 3).Name);
        Assert.Equal("Clip: drei", BotPresencePlanner.Build(Channel, snapshot, -1).Name);
    }

    [Fact]
    public void Ohne_Daten_steht_die_Fallback_Karte()
    {
        Assert.Equal(BotPresencePlanner.Fallback,
            BotPresencePlanner.Build(Channel, null, 7).Name);
        Assert.Equal(BotPresencePlanner.Fallback,
            BotPresencePlanner.Build(Channel, new TwitchSnapshot(false, null, Array.Empty<TwitchClipInfo>()), 0).Name);
    }

    [Fact]
    public void Lange_Titel_werden_vor_der_Discord_Grenze_gekuerzt()
    {
        var langerTitel = new string('x', 300);
        var presence = BotPresencePlanner.Build(Channel, Live(), 0);
        Assert.True(presence.Name.Length <= 128);

        var stream = new TwitchSnapshot(true, new TwitchStreamInfo(langerTitel, null), Array.Empty<TwitchClipInfo>());
        var gekuerzt = BotPresencePlanner.Build(Channel, stream, 0);
        Assert.Equal(128, gekuerzt.Name.Length);
        Assert.EndsWith("…", gekuerzt.Name);
    }
}
