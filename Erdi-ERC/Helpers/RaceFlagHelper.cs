using System.Collections.Generic;
using Erdi_ERC.Models;

namespace Erdi_ERC.Helpers;

/// <summary>
/// Mappt den Freitext-Trackname (z. B. "Spielberg", "Monza", "Las Vegas Strip Circuit")
/// auf eine Landesflagge (Emoji) — auf Basis des <see cref="F1RaceCatalog"/> plus Aliase
/// für gängige Strecken- und Stadtnamen.
/// </summary>
public static class RaceFlagHelper
{
    private const string Unknown = "\U0001F310"; // 🌐
    private const string Empty   = "\U0001F3F3️"; // 🏳️

    private static readonly IReadOnlyDictionary<string, string> CountryFlags =
        new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["Bahrain"]         = "\U0001F1E7\U0001F1ED",
            ["Saudi-Arabien"]   = "\U0001F1F8\U0001F1E6",
            ["Australien"]      = "\U0001F1E6\U0001F1FA",
            ["Japan"]           = "\U0001F1EF\U0001F1F5",
            ["China"]           = "\U0001F1E8\U0001F1F3",
            ["USA"]             = "\U0001F1FA\U0001F1F8",
            ["Italien"]         = "\U0001F1EE\U0001F1F9",
            ["Monaco"]          = "\U0001F1F2\U0001F1E8",
            ["Kanada"]          = "\U0001F1E8\U0001F1E6",
            ["Spanien"]         = "\U0001F1EA\U0001F1F8",
            ["Österreich"]      = "\U0001F1E6\U0001F1F9",
            ["Großbritannien"]  = "\U0001F1EC\U0001F1E7",
            ["Ungarn"]          = "\U0001F1ED\U0001F1FA",
            ["Belgien"]         = "\U0001F1E7\U0001F1EA",
            ["Niederlande"]     = "\U0001F1F3\U0001F1F1",
            ["Aserbaidschan"]   = "\U0001F1E6\U0001F1FF",
            ["Singapur"]        = "\U0001F1F8\U0001F1EC",
            ["Mexiko"]          = "\U0001F1F2\U0001F1FD",
            ["Brasilien"]       = "\U0001F1E7\U0001F1F7",
            ["Katar"]           = "\U0001F1F6\U0001F1E6",
            ["UAE"]              = "\U0001F1E6\U0001F1EA",
            // Reserve für klassische / zukünftige Strecken
            ["Deutschland"]     = "\U0001F1E9\U0001F1EA",
            ["Frankreich"]      = "\U0001F1EB\U0001F1F7",
            ["Portugal"]        = "\U0001F1F5\U0001F1F9",
            ["Türkei"]          = "\U0001F1F9\U0001F1F7",
            ["Russland"]        = "\U0001F1F7\U0001F1FA",
            ["Korea"]           = "\U0001F1F0\U0001F1F7",
            ["Malaysia"]        = "\U0001F1F2\U0001F1FE",
            ["Indien"]          = "\U0001F1EE\U0001F1F3",
            ["Argentinien"]     = "\U0001F1E6\U0001F1F7",
            ["Südafrika"]       = "\U0001F1FF\U0001F1E6",
            // Zusätzliche Nationalitäten für Fahrer-Profile (Community ist DACH-lastig + EU).
            ["Schweiz"]         = "\U0001F1E8\U0001F1ED",
            ["Polen"]           = "\U0001F1F5\U0001F1F1",
            ["Schweden"]        = "\U0001F1F8\U0001F1EA",
            ["Norwegen"]        = "\U0001F1F3\U0001F1F4",
            ["Dänemark"]        = "\U0001F1E9\U0001F1F0",
            ["Finnland"]        = "\U0001F1EB\U0001F1EE",
            ["Tschechien"]      = "\U0001F1E8\U0001F1FF",
            ["Irland"]          = "\U0001F1EE\U0001F1EA",
            ["Kroatien"]        = "\U0001F1ED\U0001F1F7",
            ["Slowenien"]       = "\U0001F1F8\U0001F1EE",
            ["Slowakei"]        = "\U0001F1F8\U0001F1F0",
            ["Rumänien"]        = "\U0001F1F7\U0001F1F4",
            ["Griechenland"]    = "\U0001F1EC\U0001F1F7",
            ["Luxemburg"]       = "\U0001F1F1\U0001F1FA",
        };

    // Reihenfolge fürs Nationalitäts-Dropdown: DACH zuerst, dann der Rest alphabetisch.
    private static readonly string[] PinnedCountries = { "Deutschland", "Österreich", "Schweiz" };

    // Substring-Aliase auf Land. Werden geprüft, BEVOR auf Country/Key aus F1RaceCatalog gematcht wird.
    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            // Australien
            ["melbourne"]       = "Australien",
            ["albert park"]     = "Australien",
            // Österreich
            ["spielberg"]       = "Österreich",
            ["red bull ring"]   = "Österreich",
            // Belgien
            ["spa"]             = "Belgien",
            ["francorchamps"]   = "Belgien",
            // Japan
            ["suzuka"]          = "Japan",
            // China
            ["shanghai"]        = "China",
            // Brasilien
            ["interlagos"]      = "Brasilien",
            ["sao paulo"]       = "Brasilien",
            ["são paulo"]       = "Brasilien",
            // USA
            ["vegas"]           = "USA",
            ["cota"]            = "USA",
            ["austin"]          = "USA",
            ["miami"]           = "USA",
            // UAE / Abu Dhabi
            ["yas marina"]      = "UAE",
            ["abu dhabi"]       = "UAE",
            // Singapur
            ["marina bay"]      = "Singapur",
            // Ungarn
            ["hungaroring"]     = "Ungarn",
            ["budapest"]        = "Ungarn",
            // Niederlande
            ["zandvoort"]       = "Niederlande",
            // Aserbaidschan
            ["baku"]            = "Aserbaidschan",
            // Spanien
            ["barcelona"]       = "Spanien",
            ["catalunya"]       = "Spanien",
            ["madrid"]          = "Spanien",
            // GB
            ["silverstone"]     = "Großbritannien",
            // Italien
            ["monza"]           = "Italien",
            ["imola"]           = "Italien",
            // Monaco
            ["monte carlo"]     = "Monaco",
            // Katar
            ["losail"]          = "Katar",
            ["lusail"]          = "Katar",
            // Mexiko
            ["hermanos"]        = "Mexiko",
            ["rodriguez"]       = "Mexiko",
            ["rodríguez"]       = "Mexiko",
            // Saudi-Arabien
            ["jeddah"]          = "Saudi-Arabien",
            ["corniche"]        = "Saudi-Arabien",
            // Kanada
            ["villeneuve"]      = "Kanada",
            ["montreal"]        = "Kanada",
            // Deutschland (Reserve)
            ["hockenheim"]      = "Deutschland",
            ["nürburgring"]     = "Deutschland",
            ["nurburgring"]     = "Deutschland",
        };

    /// <summary>
    /// Liefert das Flaggen-Emoji für einen Track-Freitext.
    /// Versucht zuerst Substring-Aliase, dann Country/Key/Name aus <see cref="F1RaceCatalog"/>.
    /// </summary>
    public static string Resolve(string? trackText)
    {
        if (string.IsNullOrWhiteSpace(trackText)) return Empty;
        var text = Fold(trackText);

        foreach (var alias in Aliases)
        {
            if (text.Contains(Fold(alias.Key), System.StringComparison.OrdinalIgnoreCase)
                && CountryFlags.TryGetValue(alias.Value, out var flag))
            {
                return flag;
            }
        }

        foreach (var track in F1RaceCatalog.Tracks)
        {
            if (text.Contains(Fold(track.Country), System.StringComparison.OrdinalIgnoreCase)
                || text.Contains(Fold(track.Key), System.StringComparison.OrdinalIgnoreCase))
            {
                if (CountryFlags.TryGetValue(track.Country, out var flag)) return flag;
            }
        }

        return Unknown;
    }

    /// <summary>
    /// Normalisiert Freitext für Vergleiche: Diakritika raus (é→e, ü→u, ó→o), klein.
    /// Ohne das findet „Formula 1 Gran Premio de la Ciudad de México" weder „mexico"
    /// noch „Mexiko" — der Streckenname kommt so aus der Renn-CSV.
    /// </summary>
    public static string Fold(string text)
    {
        var decomposed = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().ToLowerInvariant();
    }

    /// <summary>ISO-2-Code der Strecken-Nation fürs Header-Flaggenbild, z. B. „mx". Leer, wenn unbekannt.</summary>
    public static string TrackFlagIso2(string? track)
    {
        if (string.IsNullOrWhiteSpace(track)) return "";
        var iso2 = Fold(track) switch
        {
            var t when t.Contains("australi") => "au",
            var t when t.Contains("bahrain") || t.Contains("bhr") => "bh",
            var t when t.Contains("saudi") || t.Contains("jeddah") || t.Contains("ksa") => "sa",
            var t when t.Contains("japan") || t.Contains("suzuka") || t.Contains("jpn") => "jp",
            var t when t.Contains("china") || t.Contains("shanghai") => "cn",
            var t when t.Contains("miami") => "us",
            var t when t.Contains("imola") || t.Contains("emilia") => "it",
            var t when t.Contains("monaco") => "mc",
            var t when t.Contains("kanada") || t.Contains("canada") || t.Contains("can") => "ca",
            var t when t.Contains("spanien") || t.Contains("spain") || t.Contains("barcelona") => "es",
            // Kein nacktes "aut": das trifft jedes „Autódromo" (Mexiko, Imola, Interlagos).
            var t when t.Contains("osterreich") || t.Contains("austria") || t.Contains("red bull ring") => "at",
            var t when t.Contains("silverstone") || t.Contains("grossbritannien") || t.Contains("ukm") || t.Contains("britain") => "gb",
            var t when t.Contains("ungarn") || t.Contains("hungary") || t.Contains("budapest") => "hu",
            var t when t.Contains("belgien") || t.Contains("belgium") || t.Contains("spa") || t.Contains("bel") => "be",
            var t when t.Contains("niederland") || t.Contains("netherlands") || t.Contains("zandvoort") || t.Contains("ned") => "nl",
            var t when t.Contains("italien") || t.Contains("italy") || t.Contains("monza") || t.Contains("ita") => "it",
            var t when t.Contains("aserbaidschan") || t.Contains("azerbaijan") || t.Contains("baku") => "az",
            var t when t.Contains("singapur") || t.Contains("singapore") => "sg",
            var t when t.Contains("austin") || t.Contains("cota") || t.Contains("usa") => "us",
            var t when t.Contains("mexiko") || t.Contains("mexico") || t.Contains("mex") => "mx",
            var t when t.Contains("brasili") || t.Contains("brazil") || t.Contains("sao paulo") || t.Contains("interlagos") => "br",
            var t when t.Contains("las vegas") => "us",
            var t when t.Contains("katar") || t.Contains("qatar") || t.Contains("losail") => "qa",
            var t when t.Contains("abu dhabi") || t.Contains("yas marina") => "ae",
            _ => ""
        };

        // Kein Treffer im Raster: über die Alias-/Katalog-Auflösung nachsehen.
        // „Autódromo Hermanos Rodríguez" trägt kein „mex" im Text, ist aber Mexiko.
        if (iso2.Length > 0) return iso2;
        return ResolveCountryIso2(ResolveTrack(track)?.Country) ?? "";
    }

    /// <summary>Kürzel der Strecke für die Ergebnistabelle, z. B. „MEX".</summary>
    public static string TrackAbbr(string? track)
    {
        if (string.IsNullOrWhiteSpace(track)) return "";
        var abbr = Fold(track) switch
        {
            var t when t.Contains("australi") => "AUS",
            var t when t.Contains("bahrain") || t.Contains("bhr") => "BHR",
            var t when t.Contains("saudi") || t.Contains("jeddah") || t.Contains("ksa") => "KSA",
            var t when t.Contains("japan") || t.Contains("suzuka") || t.Contains("jpn") => "JPN",
            var t when t.Contains("china") || t.Contains("shanghai") => "CHN",
            var t when t.Contains("miami") => "MIA",
            var t when t.Contains("imola") || t.Contains("emilia") => "Imola",
            var t when t.Contains("monaco") => "MON",
            var t when t.Contains("kanada") || t.Contains("canada") || t.Contains("can") => "CAN",
            var t when t.Contains("spanien") || t.Contains("spain") || t.Contains("barcelona") => "ESP",
            var t when t.Contains("osterreich") || t.Contains("austria") || t.Contains("red bull ring") => "AUT",
            var t when t.Contains("silverstone") || t.Contains("grossbritannien") || t.Contains("ukm") || t.Contains("britain") => "GBR",
            var t when t.Contains("ungarn") || t.Contains("hungary") || t.Contains("budapest") => "HUN",
            var t when t.Contains("belgien") || t.Contains("belgium") || t.Contains("spa") || t.Contains("bel") => "BEL",
            var t when t.Contains("niederland") || t.Contains("netherlands") || t.Contains("zandvoort") || t.Contains("ned") => "NED",
            var t when t.Contains("italien") || t.Contains("italy") || t.Contains("monza") || t.Contains("ita") => "Monza",
            var t when t.Contains("aserbaidschan") || t.Contains("azerbaijan") || t.Contains("baku") => "AZE",
            var t when t.Contains("singapur") || t.Contains("singapore") => "SGP",
            var t when t.Contains("austin") || t.Contains("cota") || t.Contains("usa") => "USA",
            var t when t.Contains("mexiko") || t.Contains("mexico") || t.Contains("mex") => "MEX",
            var t when t.Contains("brasili") || t.Contains("brazil") || t.Contains("sao paulo") || t.Contains("interlagos") => "BRA",
            var t when t.Contains("las vegas") => "LVG",
            var t when t.Contains("katar") || t.Contains("qatar") || t.Contains("losail") => "QAT",
            var t when t.Contains("abu dhabi") || t.Contains("yas marina") => "ABU",
            _ => ""
        };

        if (abbr.Length > 0) return abbr;
        var code = ResolveCountryCode(ResolveTrack(track)?.Country);
        if (code is not null) return code;
        return track.Length >= 3 ? track[..3].ToUpperInvariant() : track.ToUpperInvariant();
    }

    /// <summary>
    /// Liefert das Flaggen-Emoji für einen exakten Ländernamen (wie im Profil-Dropdown gespeichert),
    /// oder <c>null</c>, wenn kein passendes Land hinterlegt ist (z. B. Freitext / „Andere").
    /// </summary>
    public static string? ResolveCountry(string? country)
    {
        if (string.IsNullOrWhiteSpace(country)) return null;
        return CountryFlags.TryGetValue(country.Trim(), out var flag) ? flag : null;
    }

    // Land → 3-Buchstaben-Sportcode (IOC/F1-Style). Bulletproof auf jeder Plattform — löst das
    // Emoji-Problem (Windows/Chrome rendert Flaggen-Emojis als Buchstaben) auf der Fahrer-Karte.
    private static readonly IReadOnlyDictionary<string, string> CountryCodes =
        new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["Bahrain"]="BHR", ["Saudi-Arabien"]="SAU", ["Australien"]="AUS", ["Japan"]="JPN",
            ["China"]="CHN", ["USA"]="USA", ["Italien"]="ITA", ["Monaco"]="MON", ["Kanada"]="CAN",
            ["Spanien"]="ESP", ["Österreich"]="AUT", ["Großbritannien"]="GBR", ["Ungarn"]="HUN",
            ["Belgien"]="BEL", ["Niederlande"]="NED", ["Aserbaidschan"]="AZE", ["Singapur"]="SGP",
            ["Mexiko"]="MEX", ["Brasilien"]="BRA", ["Katar"]="QAT", ["UAE"]="UAE",
            ["Deutschland"]="GER", ["Frankreich"]="FRA", ["Portugal"]="POR", ["Türkei"]="TUR",
            ["Russland"]="RUS", ["Korea"]="KOR", ["Malaysia"]="MAS", ["Indien"]="IND",
            ["Argentinien"]="ARG", ["Südafrika"]="RSA", ["Schweiz"]="SUI", ["Polen"]="POL",
            ["Schweden"]="SWE", ["Norwegen"]="NOR", ["Dänemark"]="DEN", ["Finnland"]="FIN",
            ["Tschechien"]="CZE", ["Irland"]="IRL", ["Kroatien"]="CRO", ["Slowenien"]="SLO",
            ["Slowakei"]="SVK", ["Rumänien"]="ROU", ["Griechenland"]="GRE", ["Luxemburg"]="LUX",
        };

    // Land → ISO-3166 alpha-2 (Dateiname für /images/flags/{code}.svg). Flagge ist optional:
    // fehlt das SVG, fällt die Karte sauber auf den 3-Buchstaben-Code zurück.
    private static readonly IReadOnlyDictionary<string, string> CountryIso2 =
        new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["Bahrain"]="bh", ["Saudi-Arabien"]="sa", ["Australien"]="au", ["Japan"]="jp",
            ["China"]="cn", ["USA"]="us", ["Italien"]="it", ["Monaco"]="mc", ["Kanada"]="ca",
            ["Spanien"]="es", ["Österreich"]="at", ["Großbritannien"]="gb", ["Ungarn"]="hu",
            ["Belgien"]="be", ["Niederlande"]="nl", ["Aserbaidschan"]="az", ["Singapur"]="sg",
            ["Mexiko"]="mx", ["Brasilien"]="br", ["Katar"]="qa", ["UAE"]="ae",
            ["Deutschland"]="de", ["Frankreich"]="fr", ["Portugal"]="pt", ["Türkei"]="tr",
            ["Russland"]="ru", ["Korea"]="kr", ["Malaysia"]="my", ["Indien"]="in",
            ["Argentinien"]="ar", ["Südafrika"]="za", ["Schweiz"]="ch", ["Polen"]="pl",
            ["Schweden"]="se", ["Norwegen"]="no", ["Dänemark"]="dk", ["Finnland"]="fi",
            ["Tschechien"]="cz", ["Irland"]="ie", ["Kroatien"]="hr", ["Slowenien"]="si",
            ["Slowakei"]="sk", ["Rumänien"]="ro", ["Griechenland"]="gr", ["Luxemburg"]="lu",
        };

    /// <summary>3-Buchstaben-Nationscode (z. B. „GER") für die Fahrer-Karte, oder null bei Freitext/„Andere".</summary>
    public static string? ResolveCountryCode(string? country)
    {
        if (string.IsNullOrWhiteSpace(country)) return null;
        return CountryCodes.TryGetValue(country.Trim(), out var code) ? code : null;
    }

    /// <summary>ISO-2-Ländercode (z. B. „de") für das Flaggen-SVG <c>/images/flags/{code}.svg</c>, oder null.</summary>
    public static string? ResolveCountryIso2(string? country)
    {
        if (string.IsNullOrWhiteSpace(country)) return null;
        return CountryIso2.TryGetValue(country.Trim(), out var code) ? code : null;
    }

    /// <summary>
    /// Kuratierte, sortierte Länderliste fürs Nationalitäts-Dropdown (DACH zuerst, dann alphabetisch).
    /// Jeder Eintrag hat garantiert eine Flagge in <see cref="CountryFlags"/>.
    /// </summary>
    public static System.Collections.Generic.IReadOnlyList<string> Countries { get; } =
        System.Linq.Enumerable.ToList(
            System.Linq.Enumerable.Concat(
                PinnedCountries,
                System.Linq.Enumerable.OrderBy(
                    System.Linq.Enumerable.Where(CountryFlags.Keys, k => !System.Array.Exists(PinnedCountries, p => p == k)),
                    k => k, System.StringComparer.OrdinalIgnoreCase)));

    /// <summary>
    /// Versucht, einen Freitext-Streckennamen (z. B. „Spa", „Monza", „Red Bull Ring") auf einen
    /// Katalog-Track abzubilden – für Outline-Asset (<c>track.Key</c>) und Anzeige auf der Fahrer-Karte.
    /// </summary>
    public static F1Track? ResolveTrack(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = Fold(text);
        var oic = System.StringComparison.OrdinalIgnoreCase;

        var direct = System.Linq.Enumerable.FirstOrDefault(F1RaceCatalog.Tracks,
            x => t.Contains(Fold(x.Key), oic) || Fold(x.Name).Contains(t, oic) || t.Contains(Fold(x.Name), oic));
        if (direct != null) return direct;

        foreach (var alias in Aliases)
        {
            if (t.Contains(Fold(alias.Key), oic))
            {
                var byAlias = System.Linq.Enumerable.FirstOrDefault(F1RaceCatalog.Tracks,
                    x => x.Country.Equals(alias.Value, oic));
                if (byAlias != null) return byAlias;
            }
        }

        return System.Linq.Enumerable.FirstOrDefault(F1RaceCatalog.Tracks, x => t.Contains(Fold(x.Country), oic));
    }
}
