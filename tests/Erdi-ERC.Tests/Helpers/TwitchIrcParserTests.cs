using Erdi_ERC.Helpers;
using Xunit;

namespace Erdi_ERC.Tests.Helpers;

/// <summary>
/// Der Chat-Leser bekommt rohe IRC-Zeilen. Der Parser muss Tags, Login und Text
/// trennen und darf normale Chat-Zeilen nicht als Vote missverstehen.
/// </summary>
public class TwitchIrcParserTests
{
    private const string Line =
        "@badge-info=;badges=;color=#1E90FF;display-name=WienerFan;emotes=;id=abc;mod=0;" +
        "room-id=1;subscriber=0;tmi-sent-ts=1;turbot=0;user-id=12345;user-type= " +
        ":wienerfan!wienerfan@wienerfan.tmi.twitch.tv PRIVMSG #erdi10 :!vote wiener";

    [Fact]
    public void ParsePrivMsg_liest_tags_login_und_text()
    {
        var m = TwitchIrcParser.ParsePrivMsg(Line);

        Assert.NotNull(m);
        Assert.Equal("wienerfan", m!.Login);
        Assert.Equal("WienerFan", m.DisplayName);
        Assert.Equal("12345", m.UserId);
        Assert.Equal("!vote wiener", m.Text);
    }

    [Theory]
    [InlineData("!vote wiener", "wiener")]
    [InlineData("!VOTE  Wiener", "Wiener")]
    [InlineData("!dotd max berger", "max berger")]
    [InlineData("!vote", null)]
    [InlineData("hallo wiener", null)]
    public void ExtractVoteTarget_erkennt_nur_befehle(string text, string? expected)
        => Assert.Equal(expected, TwitchIrcParser.ExtractVoteTarget(text));

    [Fact]
    public void ParsePrivMsg_faellt_ohne_Tags_auf_den_Login_zurueck()
    {
        const string line =
            ":wienerfan!wienerfan@wienerfan.tmi.twitch.tv PRIVMSG #erdi10 :!vote wiener";

        var m = TwitchIrcParser.ParsePrivMsg(line);

        Assert.NotNull(m);
        Assert.Equal("wienerfan", m!.Login);
        Assert.Equal("wienerfan", m.DisplayName);
        Assert.Equal(string.Empty, m.UserId);
        Assert.Equal("!vote wiener", m.Text);
    }

    [Fact]
    public void IsPing_erkennt_keepalive()
        => Assert.True(TwitchIrcParser.IsPing("PING :tmi.twitch.tv"));
}
