using System.Text.RegularExpressions;

namespace Erdi_ERC.Helpers
{
    /// <summary>
    /// Anzeige-Hilfe fuer Fahrernamen.
    ///
    /// Die gespeicherten Namen tragen das Clantag "ERC |" vorneweg
    /// ("ERC | michi582015"). Auf der eigenen Seite ist das Rauschen: in einer Tabelle,
    /// in der JEDER Eintrag so beginnt, unterscheidet es nichts und steht in der
    /// wichtigsten Spalte.
    ///
    /// Wichtig: Das ist eine reine ANZEIGE-Funktion. Der Schluessel fuer das Nachschlagen
    /// eines Profils (<c>asp-route-driverName</c>) bleibt der volle, gespeicherte Name —
    /// gekuerzt wird nur, was auf dem Bildschirm steht.
    /// </summary>
    public static class DriverNameHelper
    {
        // Nur am Anfang, nur das genaue Tag. "Eric | Berger" und "Marcus ERC | X"
        // bleiben unberuehrt, ebenso "ERCLES" (kein Trenner nach dem Tag).
        private static readonly Regex Clantag =
            new(@"^ERC\s*\|\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Name ohne fuehrendes Clantag, getrimmt. Null/leer -> leerer String.</summary>
        public static string Display(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            return Clantag.Replace(name, string.Empty).Trim();
        }
    }
}
