namespace Erdi_ERC.Models
{
    public class Erdi10ViewModel
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
        public bool IsGuestWinner { get; set; }
        public string? WinnerGuestForMain { get; set; }
        public bool IsGuestP2 { get; set; }
        public string? P2GuestForMain { get; set; }
        public bool IsGuestP3 { get; set; }
        public string? P3GuestForMain { get; set; }
    }

    public class TrackSetup
    {
        public int Id { get; set; }
        public string Track { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? SetupInfo { get; set; }
        public string SetupText { get; set; } = string.Empty;
        // Spieljahr / Kurzlabel, z.B. "26" oder "25". Wird verwendet, um Setups nach F1-Version zu unterscheiden.
        public string? GameYear { get; set; } = null;
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

    public class SetupBlockedUser
    {
        public string DiscordId { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
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
        /// <summary>Manueller Punkte-Bonus/-Malus, der bei jeder Neuberechnung erhalten bleibt
        /// und auf die aus den Rennen abgeleiteten Punkte addiert wird (z.B. Strafpunkte).</summary>
        public int PointsAdjustment { get; set; }
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

        /// <summary>Saison-Label (z.B. "2026"). Null = nicht zugeordnet. Steuert die
        /// saison-bezogene Tabellen-Berechnung, wenn die Liga eine aktuelle Saison gesetzt hat.</summary>
        public string? Season { get; set; }

        public List<RaceFinish> Finishes { get; set; } = new();
        public List<RaceReserveAssignment> ReserveAssignments { get; set; } = new();
        public List<RaceGuestAssignment> GuestAssignments { get; set; } = new();
    }

    public class RaceFinish
    {
        public int Id { get; set; }
        public int RaceResultId { get; set; }
        public string Driver { get; set; } = string.Empty;
        /// <summary>1-based finishing position. 0 = DNF, -1 = DNS.</summary>
        public int Position { get; set; }
        public bool FastestLap { get; set; }
        public int? RaceTimeMs { get; set; }

        /// <summary>1-based Startposition aus dem Qualifying (Pole = 1). Null = nicht erfasst.</summary>
        public int? QualifyingPosition { get; set; }
    }

    public class RaceReserveAssignment
    {
        public int Id { get; set; }
        public int RaceResultId { get; set; }
        public string ReserveDriver { get; set; } = string.Empty;
        public string MainDriver { get; set; } = string.Empty;
    }

    /// <summary>
    /// Cross-League-Gastfahrer in einem Rennen dieser Liga. Pflicht-Zuordnung:
    /// <see cref="GuestDriver"/> MUSS einem <see cref="MainDriver"/> zugewiesen werden, der
    /// Liga-Hauptfahrer (kein Reserve) ist. Sentinel-Wert <c>(kein Hauptfahrer)</c>
    /// markiert Bestandsdaten ohne Zuordnung — diese zählen weder in Standings noch
    /// in Team-Punkten.
    /// </summary>
    public class RaceGuestAssignment
    {
        public int Id { get; set; }
        public int RaceResultId { get; set; }
        public string GuestDriver { get; set; } = string.Empty;
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
        /// <summary>Startnummer aus den Standings (für die Fahrer-Karte). Null, wenn nicht vergeben.</summary>
        public int? DriverNumber { get; set; }
        public bool IsReserveDriver { get; set; }
        public string? ReserveForDriver { get; set; }
        public int TotalPoints { get; set; }
        public int Wins { get; set; }
        public int Podiums { get; set; }
        public int FastestLaps { get; set; }
        public int? BestFinish { get; set; }
        public double? AverageFinish { get; set; }
        public List<DriverRaceEntry> Races { get; set; } = new();
        public List<Erdi_ERC.Helpers.DriverAchievementsHelper.Achievement> Achievements { get; set; } = new();
        public List<Erdi_ERC.Helpers.DriverRivalryHelper.RivalrySummary> Rivalries { get; set; } = new();
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
        /// <summary>Startnummer des Hauptfahrers, automatisch aus den Standings übernommen.</summary>
        public int? DriverNumber { get; set; }
        /// <summary>Freier PenaltyType. Standardtypen: DSQ, Zeitstrafe, Gridstrafe, Verwarnung, "Zeitstrafe + Strafpunkte".</summary>
        public string PenaltyType { get; set; } = "Zeitstrafe";
        public int Points { get; set; }
        /// <summary>Strafpunkte-Gesamtstand des Fahrers (in dieser Liga) zum Zeitpunkt dieses
        /// Dokuments, inkl. der Punkte dieses Dokuments. Wird NUR beim Anlegen gesetzt und
        /// danach nie wieder aktualisiert — alte Berichte dürfen den Wert nicht nachziehen.</summary>
        public int? DriverPointsTotal { get; set; }
        public string? RaceTrack { get; set; }
        public string? SecondDriver { get; set; }
        /// <summary>Startnummer des zweiten beteiligten Fahrers.</summary>
        public int? SecondDriverNumber { get; set; }
        /// <summary>Wenn true, wird der Vorfall als "between two drivers" dargestellt.</summary>
        public bool IsBetweenTwoDrivers { get; set; }
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
        public string? Title { get; set; }
        public string? Url { get; set; }
        public TimeSpan? TimeOfDay { get; set; }
        public DateTime StartAt { get; set; } = DateTime.UtcNow;
        public bool IsRecurring { get; set; }
        // Use nullable int to match usage in views/controllers (values 0..6 or null)
        public int? DayOfWeek { get; set; }
        public int DurationMinutes { get; set; } = 120;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Eine Zeile der Liga-übergreifenden Constructors-Wertung. Wird von
    /// <c>OverallConstructorsService.ComputeAsync</c> erzeugt und in
    /// <c>Views/Races/Constructors.cshtml</c> gerendert.
    /// </summary>
    public class OverallConstructorRow
    {
        public int Position { get; set; }
        public string CssKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string PrimaryColor { get; set; } = "#888888";
        public string SecondaryColor { get; set; } = "#222222";
        public int Points { get; set; }
        public int Wins { get; set; }
        public int SecondPlaces { get; set; }
        public int ThirdPlaces { get; set; }
        public int Top5 { get; set; }
        public int Top10 { get; set; }
        /// <summary>Beste je gefahrene Position. 0 = kein Treffer oder DNF-only.</summary>
        public int BestPosition { get; set; }
        public int Podiums { get; set; }
        public int Events { get; set; }
        public int LeaguesRaced { get; set; }
        public List<OverallConstructorLeagueBreakdown> PerLeague { get; set; } = new();
    }

    public class OverallConstructorLeagueBreakdown
    {
        public string LeagueId { get; set; } = string.Empty;
        public int Points { get; set; }
        public int Events { get; set; }
        public int Wins { get; set; }
        public int BestPosition { get; set; }
    }

    public class OverallConstructorsViewModel
    {
        public List<OverallConstructorRow> Rows { get; set; } = new();
        public List<string> OverallLeagueIds { get; set; } = new();
        public List<string> OverallLeagueNames { get; set; } = new();
        public int TotalEvents { get; set; }
    }

}

