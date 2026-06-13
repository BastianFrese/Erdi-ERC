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

        /// <summary>Nur Ligen mit diesem Schalter erscheinen im öffentlichen Bewerbungsformular.</summary>
        public bool IsOpenForApplications { get; set; }

        /// <summary>Kurzinfo fürs Bewerbungsformular (z.B. "Freitags 20:00 · KI bis 105").</summary>
        public string? ApplicationInfo { get; set; }

        // Navigation
        public List<DriverStanding> Standings { get; set; } = new();
        public List<RaceResult> Races { get; set; } = new();
    }
}
