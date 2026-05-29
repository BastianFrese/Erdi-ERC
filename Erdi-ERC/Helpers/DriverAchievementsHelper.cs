using System;
using System.Collections.Generic;
using System.Linq;
using <OWNER_HANDLE>_ERC.Models;

namespace <OWNER_HANDLE>_ERC.Helpers
{
    public static class DriverAchievementsHelper
    {
        public record Achievement(
            string Key,
            string Title,
            string Description,
            string Icon,
            string Tone,
            string Tier,
            string Category,
            bool IsUnlocked,
            int Current,
            int Target,
            DateTime? UnlockedAt);

        private const string Bronze = "bronze";
        private const string Silver = "silver";
        private const string Gold = "gold";
        private const string Platinum = "platinum";

        public const string CatWins = "Siege";
        public const string CatPodiums = "Podien";
        public const string CatPace = "Pace";
        public const string CatConsistency = "Konstanz";
        public const string CatCareer = "Karriere";

        private sealed class AchievementContext
        {
            public List<DriverRaceEntry> Finished { get; init; } = new();
            public List<DriverRaceEntry> Wins { get; init; } = new();
            public List<DriverRaceEntry> Podiums { get; init; } = new();
            public int BestStreak { get; init; }
            public DriverRaceEntry? BestStreakRace { get; init; }
        }

        public static IReadOnlyList<Achievement> Compute(DriverDetailViewModel m)
            => Compute(m, null, null);

        public static IReadOnlyList<Achievement> Compute(DriverDetailViewModel m, IEnumerable<CustomAchievement>? custom)
            => Compute(m, custom, null);

        public static IReadOnlyList<Achievement> Compute(
            DriverDetailViewModel m,
            IEnumerable<CustomAchievement>? custom,
            IEnumerable<AchievementDefinition>? definitions)
        {
            if (m == null) return Array.Empty<Achievement>();

            var ordered = m.Races.OrderBy(r => r.Date).ToList();
            var finished = ordered.Where(r => r.Position > 0).ToList();
            var wins = finished.Where(r => r.Position == 1).ToList();
            var podiums = finished.Where(r => r.Position is >= 1 and <= 3).ToList();

            int streak = 0, bestStreak = 0;
            DriverRaceEntry? bestStreakRace = null;
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Position > 0)
                {
                    streak++;
                    if (streak > bestStreak)
                    {
                        bestStreak = streak;
                        bestStreakRace = ordered[i];
                    }
                }
                else streak = 0;
            }

            var ctx = new AchievementContext
            {
                Finished = finished,
                Wins = wins,
                Podiums = podiums,
                BestStreak = bestStreak,
                BestStreakRace = bestStreakRace
            };

            var defs = (definitions ?? GetDefaultDefinitions())
                .Where(d => d.IsActive)
                .OrderBy(d => d.SortOrder)
                .ThenBy(d => d.Title)
                .ToList();

            var list = new List<Achievement>(defs.Count);
            foreach (var d in defs)
            {
                var (current, unlockedAt) = Evaluate(d, m, ctx);
                var unlocked = current >= d.Target;
                var capped = Math.Min(current, d.Target);
                list.Add(new Achievement(
                    d.Key, d.Title, d.Description,
                    string.IsNullOrWhiteSpace(d.Icon) ? "bi-award-fill" : d.Icon,
                    string.IsNullOrWhiteSpace(d.Tone) ? "amber" : d.Tone,
                    string.IsNullOrWhiteSpace(d.Tier) ? Gold : d.Tier,
                    string.IsNullOrWhiteSpace(d.Category) ? "Spezial" : d.Category,
                    unlocked, capped, d.Target,
                    unlocked ? unlockedAt : null));
            }

            int TierRank(string t) => t switch
            {
                Platinum => 0,
                Gold => 1,
                Silver => 2,
                Bronze => 3,
                _ => 4
            };

            if (custom != null)
            {
                foreach (var c in custom)
                {
                    list.Add(new Achievement(
                        Key: $"custom-{c.Id}",
                        Title: c.Title,
                        Description: c.Description,
                        Icon: string.IsNullOrWhiteSpace(c.Icon) ? "bi-award-fill" : c.Icon,
                        Tone: string.IsNullOrWhiteSpace(c.Tone) ? "amber" : c.Tone,
                        Tier: string.IsNullOrWhiteSpace(c.Tier) ? Gold : c.Tier,
                        Category: string.IsNullOrWhiteSpace(c.Category) ? "Spezial" : c.Category,
                        IsUnlocked: true,
                        Current: 1,
                        Target: 1,
                        UnlockedAt: c.AwardedAt));
                }
            }

            return list
                .OrderBy(a => a.IsUnlocked ? 0 : 1)
                .ThenBy(a => TierRank(a.Tier))
                .ThenBy(a => a.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static (int Current, DateTime? UnlockedAt) Evaluate(
            AchievementDefinition d, DriverDetailViewModel m, AchievementContext ctx)
        {
            DateTime? NthDate(IList<DriverRaceEntry> list, int n) =>
                list.Count >= n ? list[n - 1].Date : (DateTime?)null;

            switch (d.Metric)
            {
                case AchievementMetric.TotalWins:
                    return (ctx.Wins.Count, NthDate(ctx.Wins, d.Target));
                case AchievementMetric.TotalPodiums:
                    return (ctx.Podiums.Count, NthDate(ctx.Podiums, d.Target));
                case AchievementMetric.TotalFastestLaps:
                    return (m.FastestLaps,
                        m.Races.OrderBy(r => r.Date).Where(r => r.FastestLap).Skip(d.Target - 1).FirstOrDefault()?.Date);
                case AchievementMetric.TotalFinishedRaces:
                    return (ctx.Finished.Count, NthDate(ctx.Finished, d.Target));
                case AchievementMetric.BestFinishStreak:
                    return (ctx.BestStreak, ctx.BestStreakRace?.Date);
                case AchievementMetric.AverageFinishMaxX10:
                    var threshold = d.Target / 10.0;
                    var ok = m.AverageFinish is double a && ctx.Finished.Count >= 5 && a <= threshold;
                    return (ok ? d.Target : 0, ok ? ctx.Finished.LastOrDefault()?.Date : null);
                case AchievementMetric.WinWithFastestLap:
                    var pw = ctx.Wins.FirstOrDefault(w => w.FastestLap);
                    return (pw != null ? d.Target : 0, pw?.Date);
                case AchievementMetric.ReserveInPoints:
                    var rp = m.Races.OrderBy(r => r.Date)
                        .FirstOrDefault(r => r.WasReserve && r.Position is >= 1 and <= 10);
                    return (rp != null ? d.Target : 0, rp?.Date);
                default:
                    return (0, null);
            }
        }

        /// <summary>Built-in Default-Definitionen, identisch zu den vorherigen hardcodierten Achievements.</summary>
        public static IReadOnlyList<AchievementDefinition> GetDefaultDefinitions()
        {
            return new List<AchievementDefinition>
            {
                Def("first-win",         "First Win",          "Hol dir deinen ersten Sieg.",                   "bi-trophy-fill",          "amber",  Bronze,   CatWins,        AchievementMetric.TotalWins, 1,  10),
                Def("hi-five",           "High Five",          "Sammle insgesamt 5 Siege.",                     "bi-stars",                "amber",  Silver,   CatWins,        AchievementMetric.TotalWins, 5,  11),
                Def("legend",            "Legend",             "Sammle insgesamt 10 Siege.",                    "bi-gem",                  "violet", Gold,     CatWins,        AchievementMetric.TotalWins, 10, 12),
                Def("hall-of-fame",      "Hall of Fame",       "Sammle insgesamt 25 Siege.",                    "bi-crown-fill",           "violet", Platinum, CatWins,        AchievementMetric.TotalWins, 25, 13),
                Def("first-podium",      "Auf dem Treppchen",  "Erreiche dein erstes Podium.",                  "bi-award-fill",           "cyan",   Bronze,   CatPodiums,     AchievementMetric.TotalPodiums, 1, 20),
                Def("podium-machine",    "Podium Machine",     "Erreiche 10 Podien.",                           "bi-award",                "cyan",   Silver,   CatPodiums,     AchievementMetric.TotalPodiums, 10, 21),
                Def("podium-master",     "Podium Master",      "Erreiche 25 Podien.",                           "bi-award-fill",           "cyan",   Gold,     CatPodiums,     AchievementMetric.TotalPodiums, 25, 22),
                Def("speed-demon",       "Speed Demon",        "Fahre mindestens eine schnellste Runde.",       "bi-lightning-charge-fill","violet", Bronze,   CatPace,        AchievementMetric.TotalFastestLaps, 1, 30),
                Def("turbo",             "Turbo Mode",         "Sammle 5 schnellste Runden.",                   "bi-fire",                 "amber",  Silver,   CatPace,        AchievementMetric.TotalFastestLaps, 5, 31),
                Def("purple-sector",     "Purple Sector",      "Sammle 15 schnellste Runden.",                  "bi-lightning-fill",       "violet", Gold,     CatPace,        AchievementMetric.TotalFastestLaps, 15, 32),
                Def("perfect-weekend",   "Perfect Weekend",    "Sieg und Fastest Lap im selben Rennen.",        "bi-flag-fill",            "green",  Gold,     CatPace,        AchievementMetric.WinWithFastestLap, 1, 33),
                Def("iceman",            "Iceman",             "5 Rennen in Folge ohne DNF gefinisht.",         "bi-snow2",                "cyan",   Silver,   CatConsistency, AchievementMetric.BestFinishStreak, 5, 40),
                Def("iceman-pro",        "Iceman Pro",         "10 Rennen in Folge ohne DNF gefinisht.",        "bi-snow",                 "cyan",   Gold,     CatConsistency, AchievementMetric.BestFinishStreak, 10, 41),
                Def("metronome",         "Metronome",          "Ø Finish-Position ≤ 6 (mind. 5 Rennen).",       "bi-graph-up",             "green",  Silver,   CatConsistency, AchievementMetric.AverageFinishMaxX10, 60, 42),
                Def("metronome-elite",   "Metronome Elite",    "Ø Finish-Position ≤ 4 (mind. 5 Rennen).",       "bi-graph-up-arrow",       "green",  Gold,     CatConsistency, AchievementMetric.AverageFinishMaxX10, 40, 43),
                Def("super-sub",         "Super Sub",          "Als Reservist in die Punkte gefahren.",         "bi-shield-check",         "green",  Silver,   CatCareer,      AchievementMetric.ReserveInPoints, 1, 50),
                Def("veteran",           "Veteran",            "Absolviere 20 Rennen.",                         "bi-stopwatch-fill",       "violet", Silver,   CatCareer,      AchievementMetric.TotalFinishedRaces, 20, 51),
                Def("legend-of-the-grid","Legend of the Grid", "Absolviere 50 Rennen.",                         "bi-stopwatch",            "violet", Gold,     CatCareer,      AchievementMetric.TotalFinishedRaces, 50, 52),
            };

            static AchievementDefinition Def(string key, string title, string desc, string icon, string tone, string tier, string cat, AchievementMetric metric, int target, int sort)
                => new()
                {
                    Key = key, Title = title, Description = desc,
                    Icon = icon, Tone = tone, Tier = tier, Category = cat,
                    Metric = metric, Target = target, IsActive = true, IsBuiltIn = true, SortOrder = sort
                };
        }

        /// <summary>Höchstwertiges freigeschaltetes Achievement (Featured Badge im Header).</summary>
        public static Achievement? GetFeatured(IEnumerable<Achievement> all)
        {
            int Rank(string t) => t switch
            {
                Platinum => 4,
                Gold => 3,
                Silver => 2,
                Bronze => 1,
                _ => 0
            };
            return all.Where(a => a.IsUnlocked)
                      .OrderByDescending(a => Rank(a.Tier))
                      .ThenByDescending(a => a.UnlockedAt ?? DateTime.MinValue)
                      .FirstOrDefault();
        }
    }
}
