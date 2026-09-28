using Erdi_ERC.Models;

namespace Erdi_ERC.Helpers
{
    /// <summary>
    /// Sichtbarkeitsregel für Ersatzfahrer in Renn-Ergebnislisten.
    ///
    /// Ein Ersatzfahrer steht ab der Aufnahme in den Kader in <see cref="DriverStanding"/> und
    /// bekam dadurch bisher sofort eine Zeile in der Results-Tabelle — nur mit "DNS"-Zellen.
    /// Er soll aber erst auftauchen, wenn er in der Liga tatsächlich ein Rennen gefahren ist.
    /// Jede Ergebniszeile zählt dabei als "gefahren", auch ein DNF (Position 0).
    /// </summary>
    public static class ReserveDriverFilter
    {
        /// <summary>
        /// Liefert das Roster der Liga ohne jene Ersatzfahrer, die noch kein Rennen gefahren
        /// sind — nach <see cref="DriverStanding.Position"/> sortiert (wie zuvor im View).
        /// Stammfahrer bleiben unverändert, auch ohne Renneinsatz.
        /// </summary>
        public static List<DriverStanding> VisibleStandings(League league)
        {
            ArgumentNullException.ThrowIfNull(league);

            var driversWithResult = league.Races
                .SelectMany(r => r.Finishes)
                .Select(f => (f.Driver ?? string.Empty).Trim())
                .Where(name => name.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return league.Standings
                .Where(s => !s.IsReserveDriver || driversWithResult.Contains((s.Driver ?? string.Empty).Trim()))
                .OrderBy(s => s.Position)
                .ToList();
        }

        /// <summary>
        /// True, wenn der Fahrer in dieser Liga mindestens eine Ergebniszeile hat.
        /// </summary>
        public static bool HasRaced(League league, string? driverName)
        {
            ArgumentNullException.ThrowIfNull(league);
            if (string.IsNullOrWhiteSpace(driverName)) return false;

            var normalized = driverName.Trim();
            return league.Races
                .SelectMany(r => r.Finishes)
                .Any(f => (f.Driver ?? string.Empty).Trim().Equals(normalized, StringComparison.OrdinalIgnoreCase));
        }
    }
}
