using Erdi_ERC.Helpers;

namespace Erdi_ERC.Tests.Helpers;

/// <summary>
/// Streckennamen kommen als Freitext aus der Renn-CSV, teils mit Akzenten
/// („Formula 1 Gran Premio de la Ciudad de México"). Vor dem Fold fiel Mexico durch
/// alle Mapper und die Ergebnistabelle zeigte „FOR" — die ersten drei Buchstaben von
/// „Formula" — ohne Flagge.
/// </summary>
public class RaceFlagHelperTests
{
    private const string MexicoCity = "Formula 1 Gran Premio de la Ciudad de México";

    [Fact]
    public void Flagge_fuer_Mexiko_City_mit_Akzent()
    {
        Assert.Equal("mx", RaceFlagHelper.TrackFlagIso2(MexicoCity));
        Assert.Equal("MEX", RaceFlagHelper.TrackAbbr(MexicoCity));
        Assert.Equal("\U0001F1F2\U0001F1FD", RaceFlagHelper.Resolve(MexicoCity));
    }

    [Fact]
    public void Akzentfreie_Schreibweise_bleibt_gleich()
    {
        Assert.Equal("MEX", RaceFlagHelper.TrackAbbr("Autodromo Hermanos Rodriguez"));
        Assert.Equal("MEX", RaceFlagHelper.TrackAbbr("Mexiko-Stadt"));
    }

    [Fact]
    public void Unbekannte_Strecke_faellt_auf_erste_drei_Buchstaben_zurueck()
    {
        Assert.Equal("", RaceFlagHelper.TrackFlagIso2("Irgendein Kurs"));
        Assert.Equal("IRG", RaceFlagHelper.TrackAbbr("Irgendein Kurs"));
    }

    [Fact]
    public void Umlaute_bleiben_treffer()
    {
        // „Österreich" wird gefoldet zu „osterreich" — die Regel muss weiter greifen.
        Assert.Equal("AUT", RaceFlagHelper.TrackAbbr("Österreichring"));
        Assert.Equal("at", RaceFlagHelper.TrackFlagIso2("Red Bull Ring"));
    }

    [Fact]
    public void Track_Aufloesung_findet_Mexiko_mit_Akzent()
    {
        Assert.Equal("mexico", RaceFlagHelper.ResolveTrack(MexicoCity)?.Key);
    }
}
