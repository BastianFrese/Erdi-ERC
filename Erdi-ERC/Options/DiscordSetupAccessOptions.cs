namespace <OWNER_HANDLE>_ERC.Options
{
    public sealed class DiscordSetupAccessOptions
    {
        public string GuildId { get; set; } = string.Empty;

        /// <summary>
        /// Mindestdauer (in Tagen), die ein User auf dem Community-Discord sein muss,
        /// bevor er Zugang zu Setups (Tier >= 1) erhält. Wirkt gegen Drive-by-Joiner,
        /// die nur kurz beitreten, Setups abgreifen und dann wieder gehen.
        /// 0 = Tenure-Check deaktiviert. Default: 7 Tage.
        /// </summary>
        public int MinGuildTenureDays { get; set; } = 7;
    }
}
