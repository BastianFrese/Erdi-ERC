namespace Erdi_ERC.Helpers
{
    public static class F1TeamsHelper
    {
        public record F1Team(string Name, string CssKey, string PrimaryColor, string SecondaryColor, string LogoKey, string[] Drivers);

        public static readonly System.Collections.Generic.IReadOnlyList<F1Team> Teams = new System.Collections.Generic.List<F1Team>
        {
            new("Red Bull Racing",   "redbull",    "#3671C6", "#CC1E4A", "red-bull-racing", new[] { "Max Verstappen",    "Yuki Tsunoda" }),
            new("Mercedes",          "mercedes",   "#00D2BE", "#C0C0C0", "mercedes",        new[] { "George Russell",    "Kimi Antonelli" }),
            new("Ferrari",           "ferrari",    "#E8002D", "#FFFFFF", "ferrari",         new[] { "Charles Leclerc",   "Lewis Hamilton" }),
            new("McLaren",           "mclaren",    "#FF8000", "#000000", "mclaren",         new[] { "Lando Norris",      "Oscar Piastri" }),
            new("Aston Martin",      "astonmartin","#229971", "#CEDC00", "aston-martin",    new[] { "Fernando Alonso",   "Lance Stroll" }),
            new("Alpine",            "alpine",     "#FF87BC", "#0093CC", "alpine",          new[] { "Pierre Gasly",      "Jack Doohan" }),
            new("Williams",          "williams",   "#64C4FF", "#FFFFFF", "williams",        new[] { "Alex Albon",        "Carlos Sainz" }),
            new("Haas",              "haas",       "#B6BABD", "#E8002D", "haas",            new[] { "Esteban Ocon",      "Oliver Bearman" }),
            new("Racing Bulls",      "racingbulls","#6692FF", "#CC1E4A", "rb",              new[] { "Isack Hadjar",      "Liam Lawson" }),
            new("Audi",              "audi",       "#8A9597", "#FFFFFF", "audi",            new[] { "Nico Hülkenberg",   "Gabriel Bortoleto" }),
            new("Cadillac",            "cadillac",   "#000000", "#FFFFFF", "cadillac",        new[] { "Logan Sargeant",    "Zane Maloney" }),
        };

        private static readonly System.Collections.Generic.IReadOnlyDictionary<string, string> TeamAliases =
            new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["red bull"] = "redbull",
                ["redbull"] = "redbull",
                ["red bull racing"] = "redbull",
                ["rb"] = "racingbulls",
                ["racing bulls"] = "racingbulls",
                ["visa cash app rb"] = "racingbulls",
                ["alphatauri"] = "racingbulls",
                // Sauber wurde 2026 zu Audi (gleicher Konstrukteurs-Eintrag) → Legacy-Teamnamen
                // auf das Audi-Team mappen, damit alte Standings-Daten weiterhin Farbe/Logo bekommen.
                ["sauber"] = "audi",
                ["audi"] = "audi",
                ["stake sauber"] = "audi",
                ["kick sauber"] = "audi",
                ["alfa romeo"] = "audi",
                ["cadillac"] = "cadillac"
            };

        public static readonly System.Collections.Generic.IReadOnlyDictionary<string, F1Team> DriverToTeam =
            System.Linq.Enumerable.ToDictionary(
                System.Linq.Enumerable.SelectMany(Teams, t => System.Linq.Enumerable.Select(t.Drivers, d => (Driver: d, Team: t))),
                x => x.Driver, x => x.Team, System.StringComparer.OrdinalIgnoreCase);

        public static string? GetCssKeyForDriver(string? driverName)
        {
            if (string.IsNullOrWhiteSpace(driverName)) return null;
            return DriverToTeam.TryGetValue(driverName.Trim(), out var team) ? team.CssKey : null;
        }

        public static F1Team? GetTeamByName(string? teamName)
        {
            if (string.IsNullOrWhiteSpace(teamName)) return null;

            var normalized = teamName.Trim();

            if (TeamAliases.TryGetValue(normalized, out var aliasCssKey))
            {
                return System.Linq.Enumerable.FirstOrDefault(Teams, t =>
                    t.CssKey.Equals(aliasCssKey, System.StringComparison.OrdinalIgnoreCase));
            }

            return System.Linq.Enumerable.FirstOrDefault(Teams, t =>
                t.Name.Equals(normalized, System.StringComparison.OrdinalIgnoreCase) ||
                t.CssKey.Equals(normalized, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}



