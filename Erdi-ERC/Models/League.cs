using System;
using System.Collections.Generic;

namespace <OWNER_HANDLE>_ERC.Models
{
    public class League
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsArchived { get; set; }
        public string? ArchivedName { get; set; }
        public DateTime? ArchivedAt { get; set; }

        /// <summary>Anzeigereihenfolge — niedrigere Zahl erscheint zuerst.</summary>
        public int SortOrder { get; set; } = 0;

        /// <summary>Soll-Anzahl Stammfahrer-Plätze (für Kapazitäts-/Warteliste-Anzeige). Null = unbegrenzt.</summary>
        public int? Capacity { get; set; }

        /// <summary>Streichresultate: Anzahl der schwächsten Rennergebnisse, die je Fahrer aus
        /// der Meisterschaftswertung genommen werden. Null/0 = alle Rennen zählen.</summary>
        public int? DropWorstResults { get; set; }

        /// <summary>Aktuelle Saison (z.B. "2026"). Ist sie gesetzt, zählt die Tabelle nur Rennen
        /// dieser Saison; neue Rennen werden automatisch damit getaggt. Null = alle Rennen zählen.</summary>
        public string? CurrentSeason { get; set; }

        /// <summary>Opt-in: Diese Liga zählt in die Liga-übergreifende Constructors-Meisterschaft
        /// (Punkte aller Ligen werden hier aggregiert). Default true; Spaß-/Probier-Ligen können
        /// ihn ausschalten, damit ihre Ergebnisse den Gesamtkampf nicht verzerren.</summary>
        public bool CountsTowardOverall { get; set; } = true;

        // Navigation
        public List<DriverStanding> Standings { get; set; } = new();
        public List<RaceResult> Races { get; set; } = new();
    }
}
