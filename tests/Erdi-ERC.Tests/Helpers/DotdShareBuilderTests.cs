using Erdi_ERC.Helpers;
using Xunit;

namespace Erdi_ERC.Tests.Helpers;

/// <summary>
/// Das Overlay darf während der Wahl keine Stimmenzahlen sehen: nur gerundete,
/// leicht verrauschte Anteile. Nach dem Schließen sind die echten Prozente erlaubt.
/// </summary>
public class DotdShareBuilderTests
{
    [Fact]
    public void Open_bars_runden_auf_fuenf_prozent_ohne_rauschen()
    {
        var counts = new Dictionary<string, int> { ["A"] = 10, ["B"] = 5, ["C"] = 5 };

        var bars = DotdShareBuilder.BuildOpenBars(counts, () => 0);

        Assert.Equal(new[] { ("A", 50), ("B", 25), ("C", 25) },
            bars.Select(b => (b.Name, b.Share)));
    }

    [Fact]
    public void Open_bars_zeigen_hoechstens_acht_fahrer()
    {
        var counts = Enumerable.Range(0, 12).ToDictionary(i => $"F{i:00}", i => 12 - i);

        var bars = DotdShareBuilder.BuildOpenBars(counts, () => 0);

        Assert.Equal(8, bars.Count);
    }

    [Fact]
    public void Final_bars_zeigen_exakte_prozente()
    {
        var counts = new Dictionary<string, int> { ["A"] = 2, ["B"] = 1 };

        var bars = DotdShareBuilder.BuildFinalBars(counts);

        Assert.Equal(new[] { ("A", 67), ("B", 33) },
            bars.Select(b => (b.Name, b.Share)));
    }

    [Fact]
    public void Bars_stehen_alphabetisch_und_bleiben_im_bereich()
    {
        var counts = new Dictionary<string, int> { ["Zulu"] = 1, ["Alpha"] = 1 };

        var bars = DotdShareBuilder.BuildOpenBars(counts);

        Assert.Equal(new[] { "Alpha", "Zulu" }, bars.Select(b => b.Name));
        Assert.All(bars, b => Assert.InRange(b.Share, 0, 100));
    }

    [Fact]
    public void Open_bars_enthalten_keine_Stimmenzahl()
    {
        var counts = new Dictionary<string, int> { ["A"] = 10, ["B"] = 5 };

        var json = System.Text.Json.JsonSerializer.Serialize(
            DotdShareBuilder.BuildOpenBars(counts, () => 0));

        Assert.DoesNotContain("votes", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("count", json, StringComparison.OrdinalIgnoreCase);
    }
}
