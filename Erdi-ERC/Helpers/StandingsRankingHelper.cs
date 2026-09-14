using Erdi_ERC.Models;

namespace Erdi_ERC.Helpers
{
    /// <summary>
    /// Meisterschafts-Sortierung mit F1-Standard-Tiebreaker: Bei gleicher Punktzahl
    /// entscheidet, wer die meisten besseren Positionen hat (meiste 1. Plätze, dann
    /// meiste 2., dann 3., …), zuletzt der Name. Geteilt zwischen StatsService
    /// (Rebuild = Quelle der Wahrheit), AdminLeagueController.ResortStandings und
    /// dem Ewige-Liste-Export, damit überall dieselbe Reihenfolge entsteht.
    /// </summary>
    public static class StandingsRankingHelper
    {
        /// <summary>Maximale Position, die im Tiebreaker verglichen wird (Grid-Größe).</summary>
        public const int MaxPositions = 20;

        /// <summary>
        /// Zählt pro Fahrer, wie oft er jede Position belegt hat (1-basiert, Index 0 = Position 1).
        /// Nur Positionen &gt; 0 und &lt;= <paramref name="maxPositions"/> werden gezählt.
        /// </summary>
        public static Dictionary<string, int[]> BuildPositionCounts(
            IEnumerable<RaceFinish> finishes, int maxPositions = MaxPositions)
        {
            var counts = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in finishes)
            {
                if (f.Position <= 0 || f.Position > maxPositions || string.IsNullOrWhiteSpace(f.Driver)) continue;
                var name = f.Driver.Trim();
                if (!counts.TryGetValue(name, out var arr))
                {
                    arr = new int[maxPositions];
                    counts[name] = arr;
                }
                arr[f.Position - 1]++;
            }
            return counts;
        }

        /// <summary>
        /// Sortiert Standings für die Meisterschaft: Punkte absteigend, dann wer die meisten
        /// besseren Positionen hat (meiste 1. Plätze, dann 2., dann 3., …), dann Name.
        /// </summary>
        public static List<DriverStanding> Rank(
            IEnumerable<DriverStanding> standings,
            IReadOnlyDictionary<string, int[]> positionCounts)
        {
            return standings
                .OrderByDescending(s => s.Points)
                .ThenByDescending(
                    s => positionCounts.TryGetValue(s.Driver, out var c) ? c : Array.Empty<int>(),
                    PositionCountComparer.Instance)
                .ThenBy(s => s.Driver, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private sealed class PositionCountComparer : IComparer<int[]>
        {
            public static readonly PositionCountComparer Instance = new();

            public int Compare(int[]? x, int[]? y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x is null) return -1;
                if (y is null) return 1;

                var len = Math.Min(x.Length, y.Length);
                for (int i = 0; i < len; i++)
                {
                    var cmp = x[i].CompareTo(y[i]);
                    if (cmp != 0) return cmp;
                }
                return x.Length.CompareTo(y.Length);
            }
        }
    }
}
