namespace <OWNER_HANDLE>_ERC.Options
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
    }
}
