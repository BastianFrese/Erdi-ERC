using Erdi_ERC.Helpers;

namespace Erdi_ERC.Tests.Helpers;

/// <summary>
/// Die gespeicherten Fahrernamen tragen das Clantag "ERC |" vorneweg. In einer
/// Tabelle, in der JEDER Eintrag so beginnt, ist das Rauschen in der wichtigsten
/// Spalte. Angezeigt wird ohne Tag — nachgeschlagen wird weiter mit dem vollen
/// Namen, deshalb ist das eine reine Anzeigefunktion.
/// </summary>
public class DriverNameHelperTests
{
    [Theory]
    [InlineData("ERC | michi582015", "michi582015")]
    [InlineData("ERC | Luke-Berger", "Luke-Berger")]
    [InlineData("ERC|KITT", "KITT")]
    [InlineData("ERC |Luke-Berger", "Luke-Berger")]
    [InlineData("erc | klein", "klein")]
    [InlineData("ERC  |  doppelt", "doppelt")]
    public void Anzeige_Entfernt_Das_Clantag(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, DriverNameHelper.Display(eingabe));
    }

    [Theory]
    [InlineData("michi582015")]
    [InlineData("Eric | Berger")]      // "Eric" ist kein Clantag
    [InlineData("Marcus ERC | X")]     // Tag steht nicht am Anfang
    [InlineData("ERCLES")]             // kein Trenner nach dem Tag
    public void Anzeige_Laesst_Andere_Namen_Unberuehrt(string eingabe)
    {
        Assert.Equal(eingabe, DriverNameHelper.Display(eingabe));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Anzeige_Vertraegt_Leere_Eingaben(string? eingabe)
    {
        Assert.Equal(string.Empty, DriverNameHelper.Display(eingabe));
    }

    [Fact]
    public void Nachschlagen_Behaelt_Den_Vollen_Namen()
    {
        // Der Anzeigename ist gekuerzt, der Schluessel fuer die Suche nicht.
        const string gespeichert = "ERC | michi582015";
        Assert.Equal("michi582015", DriverNameHelper.Display(gespeichert));
        Assert.Equal(gespeichert, gespeichert);
    }
}
