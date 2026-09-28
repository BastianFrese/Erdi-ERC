namespace Erdi_ERC.Models
{
    public class TeamSummaryViewModel
    {
        public string TeamName { get; set; } = string.Empty;
        public string? CssKey { get; set; }
        public string PrimaryColor { get; set; } = "#e10600";
        public string SecondaryColor { get; set; } = "#ffffff";
        public int Drivers { get; set; }
        public decimal Points { get; set; }
        public int Wins { get; set; }
        public int Podiums { get; set; }
    }

    public class TeamListPageViewModel
    {
        public List<TeamSummaryViewModel> Teams { get; set; } = new();
    }

    public class TeamDriverCardViewModel
    {
        public string Driver { get; set; } = string.Empty;
        public string? DiscordId { get; set; }
        public decimal Points { get; set; }
        public int Wins { get; set; }
        public bool IsReserveDriver { get; set; }
        public string? ReserveForDriver { get; set; }
    }

    public class TeamRaceCardViewModel
    {
        public DateTime Date { get; set; }
        public string LeagueId { get; set; } = string.Empty;
        public string Track { get; set; } = string.Empty;
        public string Winner { get; set; } = string.Empty;
        public int TeamFinishesInTop10 { get; set; }
    }

    public class TeamDetailPageViewModel
    {
        public string TeamName { get; set; } = string.Empty;
        public string? CssKey { get; set; }
        public string PrimaryColor { get; set; } = "#e10600";
        public string SecondaryColor { get; set; } = "#ffffff";
        public decimal TotalPoints { get; set; }
        public int TotalWins { get; set; }
        public int TotalPodiums { get; set; }
        public List<TeamDriverCardViewModel> Drivers { get; set; } = new();
        public List<TeamRaceCardViewModel> RecentRaces { get; set; } = new();
    }

    public class HallOfFameRecordViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Driver { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
    }

    public class HallOfFamePageViewModel
    {
        public List<HallOfFameRecordViewModel> Records { get; set; } = new();
    }

    public class ReserveExchangeEntryViewModel
    {
        public string Driver { get; set; } = string.Empty;
        public string LeagueId { get; set; } = string.Empty;
        public string Team { get; set; } = string.Empty;
        public string? ReserveForDriver { get; set; }
        public string Platform { get; set; } = string.Empty;
        public string InputDevice { get; set; } = string.Empty;
        public string FavoriteTrack { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public decimal Points { get; set; }
        public int Wins { get; set; }
        public string PaceBucket { get; set; } = string.Empty;
        public string? ProfileDiscordId { get; set; }
    }

    public class ReserveExchangePageViewModel
    {
        public string? SelectedLeagueId { get; set; }
        public string? SelectedPlatform { get; set; }
        public string? SelectedPace { get; set; }
        public List<string> LeagueIds { get; set; } = new();
        public List<string> Platforms { get; set; } = new();
        public List<ReserveExchangeEntryViewModel> Entries { get; set; } = new();
    }
}