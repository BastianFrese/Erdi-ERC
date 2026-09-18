using Erdi_ERC.Helpers;
using Erdi_ERC.Models;
using Xunit;

namespace Erdi_ERC.Tests.Helpers;

/// <summary>
/// Vertrag von <see cref="DriverAliasHelper"/>: Das Match-Set eines Fahrers besteht aus GamerTags,
/// DisplayName UND DiscordName. Jeder Aufrufer (Fahrerkarten, Fahrer-Level, Profil, Admin-Karten)
/// muss dieselbe Liste sehen — die frühere Duplizierung ließ den DiscordName in
/// <c>StatsController.BuildDriverLevelsAsync</c> fehlen (Prod-Befund 2026-09-17).
/// </summary>
public class DriverAliasHelperTests
{
    private static DriverProfile Profile(string discordName = "discord-max", string? displayName = "ERC | Max")
        => new()
        {
            DiscordId = "1",
            DiscordName = discordName,
            DisplayName = displayName,
            GamerTags =
            {
                new DriverGamerTag { DiscordId = "1", Platform = "EA", GamerTag = "ERC | Max" },
                new DriverGamerTag { DiscordId = "1", Platform = "Steam", GamerTag = "MaxOnSteam" }
            }
        };

    [Fact]
    public void Build_includesGamerTagsDisplayNameAndDiscordName()
    {
        var aliases = DriverAliasHelper.Build(Profile());

        Assert.Contains("ERC | Max", aliases);
        Assert.Contains("MaxOnSteam", aliases);
        Assert.Contains("discord-max", aliases);
        Assert.Equal(3, aliases.Count); // DisplayName == GamerTag → dedupliziert
    }

    [Fact]
    public void Build_isCaseInsensitive()
    {
        var aliases = DriverAliasHelper.Build(Profile());

        Assert.Contains("erc | max", aliases);
        Assert.Contains("ERC | MAX", aliases);
    }

    [Fact]
    public void Build_trimsAliases()
    {
        var profile = Profile(displayName: "  Max  ");
        profile.GamerTags.Clear();
        profile.GamerTags.Add(new DriverGamerTag { DiscordId = "1", Platform = "EA", GamerTag = "  Max  " });

        var aliases = DriverAliasHelper.Build(profile);

        Assert.Contains("Max", aliases);
        Assert.Equal(2, aliases.Count);
    }

    [Fact]
    public void Build_dropsBlankAliases()
    {
        var profile = Profile(discordName: string.Empty, displayName: null);
        profile.GamerTags.Clear();
        profile.GamerTags.Add(new DriverGamerTag { DiscordId = "1", Platform = "EA", GamerTag = "   " });

        var aliases = DriverAliasHelper.Build(profile);

        // Kein leerer Eintrag, der auf Ergebnis-Zeilen mit leerem Fahrernamen matchen würde.
        Assert.Empty(aliases);
        Assert.DoesNotContain(string.Empty, aliases);
    }
}
