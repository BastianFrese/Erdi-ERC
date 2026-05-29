namespace <OWNER_HANDLE>_ERC.Models
{
    public class <OWNER_HANDLE>10ViewModel
    {
        public string TwitchChannel { get; set; } = "erdi10";
        public List<League> Leagues { get; set; } = new();
    }

    public class AllRacesViewModel
    {
        public List<AllRacesLeagueGroup> Leagues { get; set; } = new();
    }

    public class AllRacesLeagueGroup
    {
        public string LeagueId { get; set; } = string.Empty;
        public string LeagueName { get; set; } = string.Empty;
        public List<AllRaceItem> Races { get; set; } = new();
    }

    public class AllRaceItem
    {
        public int RaceId { get; set; }
        public DateTime Date { get; set; }
        public string Track { get; set; } = string.Empty;
        public string Winner { get; set; } = string.Empty;
        public int? WinnerRaceTimeMs { get; set; }
        public string? P2 { get; set; }
        public string? P3 { get; set; }
    }

    public class TrackSetup
    {
        public int Id { get; set; }
        public string Track { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? SetupInfo { get; set; }
        public string SetupText { get; set; } = string.Empty;
        /// <summary>
        /// 0 = öffentlich, 1 = Login + Community-Guild, 3 = Twitch Sub T1, 4 = Twitch Sub T2, 5 = Twitch Sub T3.
        /// Wert 2 ist deprecated (früher "Discord Rolle") und wird zur Laufzeit wie 1 behandelt.
        /// </summary>
        public int RequiredAccessTier { get; set; }
        public string? RequiredRoleLabel { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class SetupAccessRoleMapping
    {
        public int Id { get; set; }
        public int Tier { get; set; }
        public string RoleId { get; set; } = string.Empty;
        public string? Label { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class League
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsArchived { get; set; }
        public string? ArchivedName { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public List<DriverStanding> Standings { get; set; } = new();
        public List<RaceResult> Races { get; set; } = new();
    }

    public class DriverStanding
    {
        public int RowId { get; set; }
        public string LeagueId { get; set; } = string.Empty;
        public int Position { get; set; }
        public string Driver { get; set; } = string.Empty;
        public string Team { get; set; } = string.Empty;
        public int? DriverNumber { get; set; }
        public int Points { get; set; }
        public int Wins { get; set; }
        public bool IsReserveDriver { get; set; }
        public string? ReserveForDriver { get; set; }
        public int ReserveStarts { get; set; }
        public int ReservePointsForMain { get; set; }
    }

    public class RaceResult
    {
        public int RowId { get; set; }
        public string LeagueId { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Track { get; set; } = string.Empty;
        public string Winner { get; set; } = string.Empty;
        public string FastestLap { get; set; } = string.Empty;
        public List<RaceFinish> Finishes { get; set; } = new();
        public List<RaceReserveAssignment> ReserveAssignments { get; set; } = new();
    }

    /// <summary>Stores each driver's finishing position for a single race.</summary>
    public class RaceFinish
    {
        public int Id { get; set; }
        public int RaceResultId { get; set; }
        public string Driver { get; set; } = string.Empty;
        /// <summary>1-based finishing position. 0 = DNF, -1 = DNS.</summary>
        public int Position { get; set; }
        public bool FastestLap { get; set; }
        public int? RaceTimeMs { get; set; }
    }

    public class RaceReserveAssignment
    {
        public int Id { get; set; }
        public int RaceResultId { get; set; }
        public string ReserveDriver { get; set; } = string.Empty;
        public string MainDriver { get; set; } = string.Empty;
    }

    /// <summary>
    /// Strecken-Wochenende: eine Strecke wird (meist innerhalb derselben Woche) von mehreren Ligen gefahren.
    /// Pro Wochenende eine Distanz (25/35/50/100 %). Verbunden mit N <see cref="RaceWeekendLeg"/>s — pro Liga max. 1 Termin.
    /// </summary>
    public class RaceWeekend
    {
        public int Id { get; set; }
        public int Order { get; set; }
        public string Track { get; set; } = string.Empty;
        public int DistancePercent { get; set; } = 100;
        public List<RaceWeekendLeg> Legs { get; set; } = new();
    }

    /// <summary>
    /// Konkreter Liga-Termin innerhalb eines <see cref="RaceWeekend"/>.
    /// Pro Weekend × Liga existiert maximal ein Leg (DB-unique).
    /// </summary>
    public class RaceWeekendLeg
    {
        public int Id { get; set; }
        public int RaceWeekendId { get; set; }
        public RaceWeekend? Weekend { get; set; }
        public string LeagueId { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }

    /// <summary>
    /// Singleton-Tabelle für die Render-Settings der öffentlichen Race-Calendar-Seite.
    /// Es existiert immer genau eine Zeile (Id=1).
    /// </summary>
    public class RaceCalendarSettings
    {
        public int Id { get; set; } = 1;
        public string? BackgroundImageFileName { get; set; }
        public string? SeasonTitle { get; set; }
        public string? SeasonSubtitle { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class RealLifeEvent
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ImageFileName { get; set; }
        public string? YouTubeUrl { get; set; }
        public bool IsUpcoming { get; set; }
        public List<RealLifeEventImage> Images { get; set; } = new();
    }

    public class RealLifeEventImage
    {
        public int Id { get; set; }
        public int EventId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string? Caption { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }

    public class LeagueResultsViewModel
    {
        public League League { get; set; } = new();
        public List<RaceResult> Races { get; set; } = new();
    }

    public class RaceResultDetailViewModel
    {
        public League League { get; set; } = new();
        public RaceResult Race { get; set; } = new();
        public List<RaceResultDetailRow> Rows { get; set; } = new();
    }

    public class RaceResultDetailRow
    {
        public int Position { get; set; }
        public string Driver { get; set; } = string.Empty;
        public string Team { get; set; } = string.Empty;
        public int Points { get; set; }
        public int? RaceTimeMs { get; set; }
        public int? GapToLeaderMs { get; set; }
        public bool FastestLap { get; set; }
    }

    public class DriverDetailViewModel
    {
        public string LeagueId { get; set; } = string.Empty;
        public string LeagueName { get; set; } = string.Empty;
        public string Driver { get; set; } = string.Empty;
        public string Team { get; set; } = string.Empty;
        public bool IsReserveDriver { get; set; }
        public string? ReserveForDriver { get; set; }
        public int TotalPoints { get; set; }
        public int Wins { get; set; }
        public int Podiums { get; set; }
        public int FastestLaps { get; set; }
        public int? BestFinish { get; set; }
        public double? AverageFinish { get; set; }
        public List<DriverRaceEntry> Races { get; set; } = new();
        public List<<OWNER_HANDLE>_ERC.Helpers.DriverAchievementsHelper.Achievement> Achievements { get; set; } = new();
        public List<<OWNER_HANDLE>_ERC.Helpers.DriverRivalryHelper.RivalrySummary> Rivalries { get; set; } = new();
    }

    public class DriverRaceEntry
    {
        public int RaceId { get; set; }
        public string LeagueId { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Track { get; set; } = string.Empty;
        public int Position { get; set; }
        public int Points { get; set; }
        public bool FastestLap { get; set; }
        public int? RaceTimeMs { get; set; }
        public int? GapToLeaderMs { get; set; }
        public string Team { get; set; } = string.Empty;
        public bool WasReserve { get; set; }
        public string? ReserveForDriver { get; set; }
    }

    public class LeaguePenalty
    {
        public int Id { get; set; }
        public string LeagueId { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Driver { get; set; } = string.Empty;
        /// <summary>Zeitstrafe | Gridstrafe | Punkteabzug | DSQ | Verwarnung</summary>
        public string PenaltyType { get; set; } = "Punkteabzug";
        public int Points { get; set; }
        public string? RaceTrack { get; set; }
        public string? SecondDriver { get; set; }
        /// <summary>Kurzbeschreibung des Vorfalls (internes + öffentliches Feld)</summary>
        public string? Incident { get; set; }
        /// <summary>Offizielle Begründung / Urteil (öffentlich sichtbar)</summary>
        public string Reason { get; set; } = string.Empty;
        public bool IsPublic { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
    }

    public class StreamSchedule
    {
        public int Id { get; set; }
        public DateTime StartAt { get; set; }
        public int DurationMinutes { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Url { get; set; }
        public bool IsRecurring { get; set; }
        public int? DayOfWeek { get; set; }
        public TimeSpan? TimeOfDay { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Manuell vom Admin vergebene Achievements für einzelne Fahrer.
    /// Werden zusätzlich zu den automatisch berechneten (DriverAchievementsHelper) angezeigt.
    /// </summary>
    public class CustomAchievement
    {
        public int Id { get; set; }
        public string? LeagueId { get; set; }
        public string Driver { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-award-fill";
        public string Tone { get; set; } = "amber";
        public string Tier { get; set; } = "gold";
        public string Category { get; set; } = "Spezial";
        public DateTime AwardedAt { get; set; } = DateTime.UtcNow;
    }
}

