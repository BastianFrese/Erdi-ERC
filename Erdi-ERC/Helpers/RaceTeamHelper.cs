using Erdi_ERC.Models;

namespace Erdi_ERC.Helpers
{
    /// <summary>
    /// Team-Auflösung für Rennergebnisse (inkl. Reserve-Fahrer-Logik) — geteilt zwischen
    /// Home- (Startseite), Races- (Renn-Detail) und Stats-Controller (Ewige Liste).
    /// </summary>
    public static class RaceTeamHelper
    {
        /// <summary>
        /// Team-Auflösung per Standings-Liste (Kompatibilitäts-Wrapper): baut einen einmaligen
        /// <see cref="RaceTeamLookup"/> und delegiert. Bestehende Caller bleiben unverändert.
        /// </summary>
        public static string? ResolveTeamForRaceDriver(IEnumerable<DriverStanding> standings, RaceResult race, string? driverName)
            => ResolveTeamForRaceDriver(new RaceTeamLookup(standings), race, driverName);

        /// <summary>
        /// Team-Auflösung mit vorgebautem Lookup (O(1) statt linearer Standings-Scan).
        /// Decision-Order bitidentisch zum alten Pfad: Reserve → Gast → Standings → ReserveFor-Fallback.
        /// </summary>
        public static string? ResolveTeamForRaceDriver(RaceTeamLookup lookup, RaceResult race, string? driverName)
        {
            if (string.IsNullOrWhiteSpace(driverName)) return null;
            var normalizedDriver = driverName.Trim();

            var raceMainDriver = RaceTeamLookup.ReserveMainDriver(race, normalizedDriver);
            if (!string.IsNullOrWhiteSpace(raceMainDriver))
            {
                var mainTeam = lookup.TeamOf(raceMainDriver);
                if (!string.IsNullOrWhiteSpace(mainTeam))
                {
                    return mainTeam;
                }
            }

            // Pfad 1.5: Cross-League-Gastfahrer → Liga-Hauptfahrer → dessen Team.
            // Sentinel "(kein Hauptfahrer)" (Bestandsdaten ohne Zuordnung) wird ignoriert.
            var guestMainDriver = RaceTeamLookup.GuestMainDriver(race, normalizedDriver);
            if (!string.IsNullOrWhiteSpace(guestMainDriver))
            {
                var guestMainTeam = lookup.TeamOf(guestMainDriver);
                if (!string.IsNullOrWhiteSpace(guestMainTeam))
                {
                    return guestMainTeam;
                }
            }

            var standing = lookup.FindDriver(normalizedDriver);
            if (standing is null) return null;

            if (!string.IsNullOrWhiteSpace(standing.Team))
            {
                return standing.Team.Trim();
            }

            if (standing.IsReserveDriver && !string.IsNullOrWhiteSpace(standing.ReserveForDriver))
            {
                var fallbackTeam = lookup.TeamOf(standing.ReserveForDriver);
                return fallbackTeam;
            }

            return null;
        }

        public static decimal? ComputeTeamPointsForLeague(League league, string? teamName)
        {
            int[] legacyMap = { 25, 21, 18, 16, 14, 12, 10, 8, 7, 6, 5, 4, 3, 2, 1, 0, 0, 0, 0, 0 };
            return ComputeTeamPointsForLeague(league, teamName, legacyMap);
        }

        /// <summary>
        /// Berechnet die Team-Punkte einer Liga aus den Renn-Ergebnissen mit der übergebenen
        /// Punkteskala. Neue Aufrufer (Overall-Constructors, Tests) sollten diese Überladung
        /// verwenden und die Map aus <c>IOptions&lt;F1ScoringOptions&gt;</c> injizieren — sonst
        /// weicht die Liga-Wertung von der Razor-Tabelle und der Gesamtwertung ab.
        /// </summary>
        public static decimal? ComputeTeamPointsForLeague(League league, string? teamName, int[] pointMap)
        {
            if (string.IsNullOrWhiteSpace(teamName)) return null;
            if (pointMap is null || pointMap.Length == 0) return null;

            var normalizedTeam = teamName.Trim();
            var total = 0m;

            // Ein Lookup pro Liga statt linearer Standings-Scan pro Finish (O(finishes) statt O(finishes × standings)).
            var lookup = new RaceTeamLookup(league.Standings);

            foreach (var race in league.Races.OrderBy(r => r.Date).ThenBy(r => r.RowId))
            {
                foreach (var finish in race.Finishes.Where(f => f.Position > 0))
                {
                    var resolvedTeam = ResolveTeamForRaceDriver(lookup, race, finish.Driver);
                    if (string.IsNullOrWhiteSpace(resolvedTeam) || !resolvedTeam.Equals(normalizedTeam, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var idx = finish.Position - 1;
                    if (idx >= 0 && idx < pointMap.Length)
                    {
                        // Abgebrochene Rennen vergeben nur einen Anteil (siehe RacePointsFactor).
                        total += RacePointsFactor.Apply(pointMap[idx], race.PointsPercent);
                    }
                }
            }

            return total;
        }

        public static bool HasDrivenForMultipleTeams(League league, string driverName)
        {
            if (string.IsNullOrWhiteSpace(driverName)) return false;

            var normalizedDriver = driverName.Trim();
            var lookup = new RaceTeamLookup(league.Standings);

            var teams = league.Races
                .OrderBy(r => r.Date)
                .ThenBy(r => r.RowId)
                .Where(r => r.Finishes.Any(f => !string.IsNullOrWhiteSpace(f.Driver)
                                                && f.Driver.Trim().Equals(normalizedDriver, StringComparison.OrdinalIgnoreCase)))
                .Select(r => ResolveTeamForRaceDriver(lookup, r, normalizedDriver))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return teams.Count > 1;
        }

        /// <summary>
        /// Liefert true, wenn <paramref name="driverName"/> in <paramref name="race"/> als
        /// Cross-League-Gast mit gültigem Liga-Hauptfahrer eingetragen ist. Sentinel
        /// <c>(kein Hauptfahrer)</c> zählt NICHT als gültige Zuordnung — diese Fahrer
        /// werden weiterhin ohne Marker gerendert (Bestandsdaten ohne Auflösung).
        /// </summary>
        public static bool IsRaceGuest(RaceResult race, string? driverName)
        {
            if (string.IsNullOrWhiteSpace(driverName)) return false;
            var trimmed = driverName.Trim();
            return race.GuestAssignments?.Any(g =>
                !string.IsNullOrWhiteSpace(g.GuestDriver)
                && g.GuestDriver.Trim().Equals(trimmed, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(g.MainDriver)
                && g.MainDriver != Services.StatsService.GuestSentinelNoMain) ?? false;
        }
    }
}
