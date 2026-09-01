using Erdi_ERC.Models;

namespace Erdi_ERC.Helpers
{
    /// <summary>
    /// O(1)-Lookup für die Team-Auflösung einer Liga: Standings einmal als Dictionary
    /// (Key = getrimmter Fahrername, OrdinalIgnoreCase) indexieren, statt pro Finish
    /// linear über alle Standings zu scannen. Ersetzt die bisherige O(finishes × standings)-
    /// Quadratik in <see cref="RaceTeamHelper.ComputeTeamPointsForLeague"/> und
    /// <see cref="RaceTeamHelper.HasDrivenForMultipleTeams"/>.
    ///
    /// Semantik ist bitidentisch zu <see cref="RaceTeamHelper.ResolveTeamForRaceDriver"/>:
    /// Bei doppelten Fahrernamen gewinnt der ERSTE Standings-Eintrag (TryAdd = FirstOrDefault),
    /// Reserve/Guest-Assignments werden pro Race in List-Reihenfolge gescannt.
    /// </summary>
    public sealed class RaceTeamLookup
    {
        private readonly Dictionary<string, DriverStanding> _byDriver;

        public RaceTeamLookup(IEnumerable<DriverStanding> standings)
        {
            _byDriver = new Dictionary<string, DriverStanding>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in standings)
            {
                if (string.IsNullOrWhiteSpace(s.Driver)) continue;
                _byDriver.TryAdd(s.Driver.Trim(), s);
            }
        }

        /// <summary>Standings-Eintrag per getrimmtem Fahrernamen oder null.</summary>
        public DriverStanding? FindDriver(string? driverName)
        {
            var key = Key(driverName);
            return key.Length == 0 ? null : _byDriver.GetValueOrDefault(key);
        }

        /// <summary>Team eines (Haupt-)Fahrers, getrimmt, oder null wenn ohne Team.</summary>
        public string? TeamOf(string? driverName)
        {
            var standing = FindDriver(driverName);
            return string.IsNullOrWhiteSpace(standing?.Team) ? null : standing!.Team!.Trim();
        }

        /// <summary>
        /// Main-Driver eines Reservisten für ein konkretes Rennen (First-wins, identisch
        /// zum Original-FirstOrDefault über die Assignment-Liste).
        /// </summary>
        public static string? ReserveMainDriver(RaceResult race, string normalizedDriver)
        {
            return race.ReserveAssignments
                .FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.ReserveDriver)
                                     && a.ReserveDriver!.Trim().Equals(normalizedDriver, StringComparison.OrdinalIgnoreCase))
                ?.MainDriver;
        }

        /// <summary>
        /// Liga-Hauptfahrer eines Cross-League-Gasts (Sentinel "(kein Hauptfahrer)" ausgeschlossen).
        /// </summary>
        public static string? GuestMainDriver(RaceResult race, string normalizedDriver)
        {
            return race.GuestAssignments?
                .FirstOrDefault(g => !string.IsNullOrWhiteSpace(g.GuestDriver)
                                     && g.GuestDriver!.Trim().Equals(normalizedDriver, StringComparison.OrdinalIgnoreCase)
                                     && !string.IsNullOrWhiteSpace(g.MainDriver)
                                     && g.MainDriver.Trim() != Services.StatsService.GuestSentinelNoMain)
                ?.MainDriver;
        }

        /// <summary>Key-Normalisierung, identisch zur Original-Logik (Trim, leer → leer).</summary>
        public static string Key(string? s) => s?.Trim() ?? string.Empty;
    }
}