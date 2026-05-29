namespace <OWNER_HANDLE>_ERC.Options
{
    /// <summary>
    /// Konfiguration für die Fuzzy-Suche bei der Fahrer-Eintragung.
    /// </summary>
    public class DriverMatchingOptions
    {
        public const string SectionName = "DriverMatching";

        /// <summary>Maximale Levenshtein-Distanz, ab der ein Vorschlag noch angezeigt wird.</summary>
        public int MaxLevenshteinDistance { get; set; } = 2;

        /// <summary>Maximale Anzahl an Vorschlägen, die der Endpoint zurückgibt.</summary>
        public int MaxSuggestions { get; set; } = 5;

        /// <summary>Minimale Eingabelänge, ab der Vorschläge berechnet werden.</summary>
        public int MinQueryLength { get; set; } = 2;

        /// <summary>Erlaubte Plattform-Werte (für Bewerbung &amp; Profilverwaltung).</summary>
        public string[] AllowedPlatforms { get; set; } = new[] { "Steam", "EA", "Xbox", "Playstation" };
    }
}
