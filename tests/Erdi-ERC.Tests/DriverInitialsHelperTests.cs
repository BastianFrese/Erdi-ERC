using Erdi_ERC.Helpers;

namespace Erdi_ERC.Tests;

/// <summary>
/// Sichert <see cref="DriverInitialsHelper.FromGamertag"/> ab. Kernanforderung:
/// Da Fahrer Gamertags eintragen, dürfen Unterstriche &amp; Co. NICHT ins
/// Initialen-Monogramm der Fahrer-Karte fließen, sondern wirken als Wortgrenzen.
/// </summary>
public class DriverInitialsHelperTests
{
    [Theory]
    // Klassische Namen — verhaltensgleich zur alten Space-Split-Logik.
    [InlineData("John Doe", "JD")]
    [InlineData("Jane", "J")]
    [InlineData("max verstappen", "MV")]
    // Unterstriche wirken als Wortgrenze, nicht als Zeichen (der eigentliche Bug-Fix).
    [InlineData("lt_wiener", "LW")]
    [InlineData("John_Doe", "JD")]
    [InlineData("xX_Noscope_Xx", "XN")]
    // Führende/umschließende Sonderzeichen verschwinden komplett.
    [InlineData("_wiener", "W")]
    [InlineData("__John__Doe__", "JD")]
    [InlineData("[ERC]Max", "EM")]
    [InlineData(".hidden", "H")]
    // Weitere Trennzeichen ("und ähnliches").
    [InlineData("Max.Verstappen", "MV")]
    [InlineData("speed-demon", "SD")]
    [InlineData("O'Brien", "OB")]
    // Nur das erste Zeichen je Token, maximal zwei insgesamt.
    [InlineData("Alpha Bravo Charlie", "AB")]
    // Unicode-Buchstaben (Umlaute) bleiben gültige Initialen.
    [InlineData("Müller_Schumacher", "MS")]
    // Ziffern sind gültige Gamertag-Zeichen, keine Trenner.
    [InlineData("123_racer", "1R")]
    public void FromGamertag_ProducesCleanInitials(string input, string expected)
    {
        Assert.Equal(expected, DriverInitialsHelper.FromGamertag(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("___")]      // nur Trennzeichen → kein verwertbares Zeichen
    [InlineData("-.-")]
    [InlineData("[]()")]
    public void FromGamertag_NoUsableCharacters_ReturnsFallback(string? input)
    {
        Assert.Equal("?", DriverInitialsHelper.FromGamertag(input));
    }
}
