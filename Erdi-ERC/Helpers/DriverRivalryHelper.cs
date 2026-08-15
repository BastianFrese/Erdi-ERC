using System.Collections.Generic;
using System.Linq;
using Erdi_ERC.Models;

namespace Erdi_ERC.Helpers
{
    public static class DriverRivalryHelper
    {
        public record RivalrySummary(string Rival, string RivalTeam, int RacesTogether, int Wins, int Losses, int Draws, IReadOnlyList<string> Recent);

        /// <summary>
        /// Berechnet für einen Fahrer die häufigsten Direktduelle innerhalb einer Liga.
        /// Ein Duell zählt nur, wenn beide Fahrer im selben Rennen eine gewertete
        /// Position (>0) haben. Wer die niedrigere Position fährt, gewinnt das Duell.
        /// </summary>
        public static IReadOnlyList<RivalrySummary> Compute(League league, string driver, int top = 5)
        {
            if (league is null || string.IsNullOrWhiteSpace(driver)) return System.Array.Empty<RivalrySummary>();

            var driverNorm = driver.Trim();
            var bucket = new Dictionary<string, (int races, int wins, int losses, int draws, string team, List<string> recent)>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var race in league.Races)
            {
                var me = race.Finishes.FirstOrDefault(f =>
                    !string.IsNullOrWhiteSpace(f.Driver) &&
                    f.Driver.Trim().Equals(driverNorm, System.StringComparison.OrdinalIgnoreCase));
                if (me is null || me.Position <= 0) continue;

                foreach (var other in race.Finishes)
                {
                    if (other.Position <= 0) continue;
                    if (string.IsNullOrWhiteSpace(other.Driver)) continue;
                    var otherName = other.Driver.Trim();
                    if (otherName.Equals(driverNorm, System.StringComparison.OrdinalIgnoreCase)) continue;

                    var teamForOther = league.Standings
                        .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.Driver) &&
                                             s.Driver.Trim().Equals(otherName, System.StringComparison.OrdinalIgnoreCase))?.Team ?? "";

                    bucket.TryGetValue(otherName, out var agg);
                    agg.recent ??= new List<string>();
                    if (string.IsNullOrEmpty(agg.team)) agg.team = teamForOther;
                    agg.races++;
                    string outcome;
                    if (me.Position < other.Position) { agg.wins++; outcome = "W"; }
                    else if (me.Position > other.Position) { agg.losses++; outcome = "L"; }
                    else { agg.draws++; outcome = "D"; }
                    agg.recent.Add(outcome);
                    bucket[otherName] = agg;
                }
            }

            return bucket
                .OrderByDescending(kv => kv.Value.races)
                .ThenByDescending(kv => kv.Value.wins)
                .Take(top)
                .Select(kv => new RivalrySummary(
                    kv.Key,
                    kv.Value.team,
                    kv.Value.races,
                    kv.Value.wins,
                    kv.Value.losses,
                    kv.Value.draws,
                    kv.Value.recent.TakeLast(8).ToList()))
                .ToList();
        }
    }
}
