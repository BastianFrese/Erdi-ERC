namespace Erdi_ERC.Options
{
    /// <summary>
    /// Lebensdauer- und Refresh-Settings für den Login-Cookie.
    /// Sektion: <c>Auth:Cookie</c>
    /// </summary>
    public sealed class AuthCookieOptions
    {
        public const string SectionName = "Auth:Cookie";

        /// <summary>Gesamt-Lebensdauer des Cookies in Stunden.</summary>
        public int ExpireHours { get; set; } = 2;

        /// <summary>Intervall in Minuten, ab dem die Discord-Mitgliedschaft neu geprüft wird.</summary>
        public int RefreshDiscordMembershipMinutes { get; set; } = 15;
    }
}
