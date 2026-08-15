namespace Erdi_ERC.Models
{
    /// <summary>
    /// Aggregierte "Frisch eingetroffen"-Zeile für die Setup-Seite:
    /// pro Strecke + Spieljahr, abgeleitet aus TrackSetups (kein eigener DB-State).
    /// </summary>
    public class SetupNewsGroup
    {
        /// <summary>Zeitfenster in Tagen, in dem Setups als "neu" bzw. "aktualisiert" gelten.</summary>
        public const int WindowDays = 14;

        public string Track { get; set; } = string.Empty;
        public string? GameYear { get; set; }
        public int NewCount { get; set; }
        public int UpdatedCount { get; set; }
        public DateTime LatestUtc { get; set; }
    }
}
