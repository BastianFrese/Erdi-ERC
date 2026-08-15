namespace Erdi_ERC.Options
{
    /// <summary>
    /// Allgemeine, redaktionell anpassbare Einstellungen der Web-App.
    /// Sektion: <c>Application</c>
    /// </summary>
    public sealed class ApplicationOptions
    {
        public const string SectionName = "Application";

        /// <summary>Twitch-Channel, der im Stream-Embed angezeigt wird.</summary>
        public string TwitchChannel { get; set; } = "erdi10";

        /// <summary>Name + Version, der als HTTP User-Agent für externe APIs (z. B. Discord) verwendet wird.</summary>
        public string UserAgentName { get; set; } = "Erdi-ERC";
        public string UserAgentVersion { get; set; } = "1.0";

        /// <summary>
        /// Das aktuelle F1-Spieljahr (z.B. "F1 26"). Wird sitewide als Bezeichnung verwendet –
        /// kein Hardcoding von Jahreszahlen in Views oder Controllern.
        /// </summary>
        public string F1GameYear { get; set; } = "F1 26";

        /// <summary>Kurzes Jahr-Label, z.B. "26" oder "2026".</summary>
        public string F1GameYearShort { get; set; } = "26";

        /// <summary>Discord-Einladungslink zum Community-Server.</summary>
        public string DiscordInviteCommunity { get; set; } = "https://discord.gg/VyhjtDXYYV";

        /// <summary>Discord-Einladungslink zum Liga-Server.</summary>
        public string DiscordInviteLeague { get; set; } = "https://discord.gg/fRqH8DA95d";
    }
}
