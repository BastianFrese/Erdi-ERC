using System.ComponentModel.DataAnnotations;

namespace Erdi_ERC.Models
{
    /// <summary>
    /// Eine konfigurierbare Achievement-Definition. Im Admin verwaltbar.
    /// </summary>
    public class AchievementDefinition
    {
        public int Id { get; set; }

        [Required, MaxLength(64)]
        public string Key { get; set; } = string.Empty;

        [Required, MaxLength(128)]
        public string Title { get; set; } = string.Empty;

        [Required, MaxLength(512)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(64)]
        public string Icon { get; set; } = "bi-award-fill";

        [MaxLength(32)]
        public string Tone { get; set; } = "amber";

        [MaxLength(32)]
        public string Tier { get; set; } = "gold";

        [MaxLength(64)]
        public string Category { get; set; } = "Spezial";

        public AchievementMetric Metric { get; set; } = AchievementMetric.TotalWins;

        /// <summary>Schwellwert. Für AverageFinishMaxX10 = max. Avg * 10 (z.B. 60 = 6,0).</summary>
        public int Target { get; set; } = 1;

        public bool IsActive { get; set; } = true;

        public bool IsBuiltIn { get; set; }

        public int SortOrder { get; set; }
    }

    public enum AchievementMetric
    {
        TotalWins = 0,
        TotalPodiums = 1,
        TotalFastestLaps = 2,
        TotalFinishedRaces = 3,
        BestFinishStreak = 4,
        AverageFinishMaxX10 = 5,
        WinWithFastestLap = 6,
        ReserveInPoints = 7
    }
}
