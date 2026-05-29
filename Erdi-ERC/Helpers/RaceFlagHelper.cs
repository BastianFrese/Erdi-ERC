using System.Collections.Generic;
using <OWNER_HANDLE>_ERC.Models;

namespace <OWNER_HANDLE>_ERC.Helpers;

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
        };

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
        var text = trackText.Trim();

        foreach (var alias in Aliases)
        {
            if (text.Contains(alias.Key, System.StringComparison.OrdinalIgnoreCase)
                && CountryFlags.TryGetValue(alias.Value, out var flag))
            {
                return flag;
            }
        }

        foreach (var track in F1RaceCatalog.Tracks)
        {
            if (text.Contains(track.Country, System.StringComparison.OrdinalIgnoreCase)
                || text.Contains(track.Key, System.StringComparison.OrdinalIgnoreCase))
            {
                if (CountryFlags.TryGetValue(track.Country, out var flag)) return flag;
            }
        }

        return Unknown;
    }
}
