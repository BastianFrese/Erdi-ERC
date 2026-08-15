namespace Erdi_ERC.Options
{
    /// <summary>
    /// Discord-Guild-IDs, die für die Bewerbungsprüfung (Community + Liga) abgefragt werden.
    /// Sektion: <c>Discord:Guilds</c>
    /// </summary>
    public sealed class DiscordGuildOptions
    {
        public const string SectionName = "Discord:Guilds";

        public string CommunityGuildId { get; set; } = string.Empty;
        public string LeagueGuildId { get; set; } = string.Empty;

        /// <summary>
        /// Einladungslink zum Community-Discord. Wird im Bewerbungs-Partial als
        /// Hinweis-Box gerendert, wenn der Bewerber laut Claim nicht beigetreten ist.
        /// Leer = kein Link, nur Text.
        /// </summary>
        public string? CommunityInviteUrl { get; set; }

        /// <summary>
        /// Einladungslink zum Liga-Discord. Wird im Bewerbungs-Partial als
        /// Hinweis-Box gerendert, wenn der Bewerber laut Claim nicht beigetreten ist.
        /// Leer = kein Link, nur Text.
        /// </summary>
        public string? LeagueInviteUrl { get; set; }
    }
}