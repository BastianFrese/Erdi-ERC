namespace Erdi_ERC.Services
{
    /// <summary>
    /// All automation event types the system can fire.
    /// </summary>
    public static class WebhookEvents
    {
        public const string RaceResultSaved      = "race.result.saved";
        public const string PenaltySaved         = "stewarding.penalty.saved";
        public const string NewsPostPublished    = "community.news.published";
        public const string RaceWeekendSaved     = "calendar.weekend.saved";
        public const string StreamScheduled      = "community.stream.scheduled";
        public const string HighlightApproved    = "community.highlight.approved";
        public const string VotePollPublished    = "community.vote.published";

        // ── Bewerbungen ──────────────────────────────────────────────────────────
        public const string ApplicationSubmitted  = "application.submitted";
        public const string ApplicationAccepted   = "application.accepted";
        public const string ApplicationRejected   = "application.rejected";
        public const string ApplicationWaitlisted = "application.waitlisted";
        public const string ApplicationWithdrawn  = "application.withdrawn";
        public const string ApplicationSeasonClosed = "application.season.closed";

        /// <summary>Human-readable labels shown in the admin UI.</summary>
        public static readonly IReadOnlyList<(string Key, string Label, string Icon, string Description)> All =
        [
            (RaceResultSaved,      "Rennergebnis gespeichert",      "bi-trophy-fill",                 "Wird ausgelöst, wenn ein neues Rennergebnis eingetragen wird."),
            (PenaltySaved,         "Stewarding-Entscheidung",       "bi-shield-fill-exclamation",     "Wird ausgelöst, wenn eine Strafe/Entscheidung gespeichert wird."),
            (NewsPostPublished,    "Community-News veröffentlicht", "bi-newspaper",                   "Wird ausgelöst, wenn ein News-Beitrag veröffentlicht wird."),
            (RaceWeekendSaved,     "Renn-Wochenende gespeichert",   "bi-calendar-event",              "Wird ausgelöst, wenn ein Race-Weekend angelegt/aktualisiert wird."),
            (StreamScheduled,      "Stream eingetragen",            "bi-camera-video-fill",           "Wird ausgelöst, wenn ein neuer Stream-Termin angelegt wird."),
            (HighlightApproved,    "Highlight freigegeben",         "bi-play-circle-fill",            "Wird ausgelöst, wenn ein Highlight-Clip freigegeben wird."),
            (VotePollPublished,    "Community-Voting gestartet",    "bi-bar-chart-fill",              "Wird ausgelöst, wenn ein neues Community-Voting angelegt wird."),
            (ApplicationSubmitted,  "Bewerbung eingereicht",        "bi-envelope-paper",              "Wird ausgelöst, wenn ein User eine Bewerbung absendet."),
            (ApplicationAccepted,   "Bewerbung angenommen",         "bi-check-circle",                "Wird ausgelöst, wenn ein Admin eine Bewerbung annimmt."),
            (ApplicationRejected,   "Bewerbung abgelehnt",          "bi-x-circle",                    "Wird ausgelöst, wenn ein Admin eine Bewerbung ablehnt."),
            (ApplicationWaitlisted, "Bewerbung auf Warteliste",     "bi-hourglass-split",             "Wird ausgelöst, wenn eine Bewerbung wegen voller Liga auf die Warteliste umgeleitet wird."),
            (ApplicationWithdrawn,  "Bewerbung zurückgezogen",      "bi-arrow-counterclockwise",      "Wird ausgelöst, wenn ein User seine offene Bewerbung selbst zurückzieht."),
            (ApplicationSeasonClosed, "Saison geschlossen",         "bi-calendar-x",                 "Wird ausgelöst, wenn ein Admin eine Saison für eine Liga schließt (Rollover oder RejectAll)."),
        ];
    }
}
