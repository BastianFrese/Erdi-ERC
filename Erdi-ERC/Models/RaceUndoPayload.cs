namespace Erdi_ERC.Models
{
    public class RaceUndoPayload
    {
        public RaceResultSnapshot Race { get; set; } = new();
        public List<RaceFinishSnapshot> Finishes { get; set; } = new();
        public List<RaceReserveAssignmentSnapshot> ReserveAssignments { get; set; } = new();
    }

    public class RaceResultSnapshot
    {
        public int RowId { get; set; }
        public string LeagueId { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Track { get; set; } = string.Empty;
        public string Winner { get; set; } = string.Empty;
        public string FastestLap { get; set; } = string.Empty;
        public string? Season { get; set; }

        /// <summary>Initializer ist Pflicht: Snapshots, die vor diesem Feld geschrieben wurden,
        /// haben den Wert nicht im JSON — ohne Default würde der Restore 0 % setzen und dem
        /// wiederhergestellten Rennen alle Punkte nehmen.</summary>
        public int PointsPercent { get; set; } = 100;
    }

    public class RaceFinishSnapshot
    {
        public int Id { get; set; }
        public int RaceResultId { get; set; }
        public string Driver { get; set; } = string.Empty;
        public int Position { get; set; }
        public bool FastestLap { get; set; }
        public int? RaceTimeMs { get; set; }
        public int? QualifyingPosition { get; set; }
    }

    public class RaceReserveAssignmentSnapshot
    {
        public int Id { get; set; }
        public int RaceResultId { get; set; }
        public string ReserveDriver { get; set; } = string.Empty;
        public string MainDriver { get; set; } = string.Empty;
    }
}
