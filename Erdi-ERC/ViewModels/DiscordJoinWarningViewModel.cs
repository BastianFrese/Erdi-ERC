namespace Erdi_ERC.ViewModels
{
    /// <summary>
    /// ViewModel für die Discord-Join-Warnung im Bewerbungsformular.
    /// Wird vom Bewerbungs-Partial verwendet, wenn der OAuth-Claim meldet,
    /// dass der Bewerber noch nicht im Community- oder Liga-Discord ist.
    /// </summary>
    public class DiscordJoinWarningViewModel
    {
        public bool JoinedCommunity { get; set; }
        public bool JoinedLeague { get; set; }
        public string? CommunityInviteUrl { get; set; }
        public string? LeagueInviteUrl { get; set; }
    }
}