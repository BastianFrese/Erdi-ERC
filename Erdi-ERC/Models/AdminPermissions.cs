namespace <OWNER_HANDLE>_ERC.Models
{
    /// <summary>
    /// Granulare Berechtigungsschlüssel im Admin-System.
    /// Jeder Wert ist ein String-Schlüssel, der in der DB und als Claim gespeichert wird.
    /// Superadmins erhalten automatisch alle Bereiche, unabhängig von Einzelrechten.
    /// </summary>
    public static class AdminPermissions
    {
        // ── Bewerbungen ────────────────────────────────────────────────────────────
        /// <summary>Bewerbungen einsehen (Dashboard, Liste, Detailansicht)</summary>
        public const string ApplicationsView    = "applications.view";
        /// <summary>Bewerbungen annehmen / ablehnen</summary>
        public const string ApplicationsManage  = "applications.manage";
        /// <summary>Bewerbungsmetriken &amp; Statistiken</summary>
        public const string ApplicationsMetrics = "applications.metrics";

        // ── Liga ──────────────────────────────────────────────────────────────────
        /// <summary>Liga-Tabellen, Fahrer, Events verwalten</summary>
        public const string LeagueStandings = "league.standings";
        /// <summary>Rennergebnisse eintragen</summary>
        public const string LeagueRaces     = "league.races";

        // ── Community ─────────────────────────────────────────────────────────────
        /// <summary>Community Hub (News, Votes, Highlights, Strafen)</summary>
        public const string CommunityHub        = "community.hub";
        /// <summary>Real-Life-Events verwalten</summary>
        public const string CommunityEvents     = "community.events";
        /// <summary>Stream-Pläne verwalten</summary>
        public const string CommunityStreams     = "community.streams";
        /// <summary>Stewarding: Strafen anlegen, bearbeiten, löschen</summary>
        public const string CommunityStewarding = "community.stewarding";

        // ── Fahrer ────────────────────────────────────────────────────────────────
        /// <summary>Fahrer-Achievements vergeben</summary>
        public const string DriversAchievements  = "drivers.achievements";
        /// <summary>Achievement-Definitionen verwalten</summary>
        public const string DriversDefinitions   = "drivers.definitions";
        /// <summary>Fahrerkarten anzeigen und Anzeigenamen korrigieren</summary>
        public const string DriversCards         = "drivers.cards";

        // ── System ────────────────────────────────────────────────────────────────
        /// <summary>Hintergrundmusik verwalten</summary>
        public const string SystemMusic      = "system.music";
        /// <summary>Track-Setups verwalten</summary>
        public const string SystemSetups     = "system.setups";
        /// <summary>Audit Logs einsehen</summary>
        public const string SystemAuditLogs  = "system.auditlogs";
        /// <summary>Discord Webhooks verwalten und Nachrichten senden</summary>
        public const string SystemWebhooks    = "system.webhooks";
        /// <summary>Über-mich-Profile erstellen und verwalten</summary>
        public const string SystemAboutMe     = "system.aboutme";
        /// <summary>Regelwerk-Dokumente hochladen und verwalten</summary>
        public const string SystemRegelwerk   = "system.regelwerk";
        /// <summary><OWNER_HANDLE>-Troll-System verwalten (Gags, Gewichte, eigene Gags, Settings)</summary>
        public const string SystemTroll       = "system.troll";

        // ── Rückwärtskompatible Gruppen-Aliases ───────────────────────────────────
        // (Legacy-Wert: ein Admin, dem früher "applications" zugewiesen wurde,
        //  gilt weiterhin für alle applications.* Policies.)
        public const string Applications = "applications";
        public const string Community    = "community";
        public const string Drivers      = "drivers";
        public const string System       = "system";

        /// <summary>
        /// Gruppierte Übersicht aller granularen Berechtigungen für die Admin-UI.
        /// </summary>
        public static readonly IReadOnlyList<PermissionGroup> Groups =
        [
            new("Bewerbungen", "bi-clipboard-check", "***REMOVED***5b8cff",
            [
                new(ApplicationsView,    "Einsehen",         "bi-eye"),
                new(ApplicationsManage,  "Annehmen/Ablehnen","bi-check2-circle"),
                new(ApplicationsMetrics, "Metriken",         "bi-bar-chart-line"),
            ]),
            new("Liga", "bi-flag-fill", "***REMOVED***39ff14",
            [
                new(LeagueStandings, "Tabellen & Fahrer", "bi-list-ol"),
                new(LeagueRaces,     "Rennergebnisse",    "bi-trophy-fill"),
            ]),
            new("Community", "bi-people-fill", "***REMOVED***ff6b35",
            [
                new(CommunityHub,        "Hub (News/Votes)",  "bi-newspaper"),
                new(CommunityEvents,     "Events",            "bi-calendar-event"),
                new(CommunityStreams,    "Streams",           "bi-camera-video"),
                new(CommunityStewarding,"Stewarding",        "bi-shield-fill-exclamation"),
            ]),
            new("Fahrer", "bi-person-badge-fill", "***REMOVED***c084fc",
            [
                new(DriversAchievements, "Achievements",  "bi-award"),
                new(DriversDefinitions,  "Definitionen",  "bi-journal-text"),
                new(DriversCards,        "Fahrerkarten",  "bi-person-vcard-fill"),
            ]),
            new("System", "bi-gear-fill", "***REMOVED***ffb800",
            [
                new(SystemMusic,      "Hintergrundmusik", "bi-music-note-beamed"),
                    new(SystemSetups,    "Track Setups",     "bi-wrench-adjustable"),
                    new(SystemAuditLogs, "Audit Logs",       "bi-journal-code"),
                    new(SystemWebhooks,  "Discord Webhooks", "bi-discord"),
                    new(SystemRegelwerk, "Regelwerk",        "bi-file-earmark-text"),
                    new(SystemTroll,     "Troll-System",     "bi-emoji-laughing"),
            ]),
        ];

        /// <summary>Flache Liste aller granularen Berechtigungen (ohne Legacy-Aliases).</summary>
        public static IReadOnlyList<PermissionEntry> All =>
            Groups.SelectMany(g => g.Permissions).ToList();
    }

    /// <param name="Key">Eindeutiger String-Schlüssel (wird in DB &amp; Claims gespeichert)</param>
    /// <param name="Label">Anzeigename in der UI</param>
    /// <param name="Icon">Bootstrap-Icons-Klasse</param>
    public record PermissionEntry(string Key, string Label, string Icon);

    /// <param name="Label">Gruppenname</param>
    /// <param name="Icon">Gruppen-Icon</param>
    /// <param name="Color">Akzentfarbe (CSS-Farbwert)</param>
    /// <param name="Permissions">Enthaltene Einzelberechtigungen</param>
    public record PermissionGroup(string Label, string Icon, string Color, IReadOnlyList<PermissionEntry> Permissions);
}
