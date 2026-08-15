namespace Erdi_ERC.Helpers
{
    /// <summary>
    /// Ordnet den frei eingegebenen / gewählten Input-Device-String eines Fahrers
    /// einer bekannten Sim-Racing-Hardware-Marke (Lenkrad-Hersteller) zu, damit auf
    /// der Fahrer-Karte das passende Marken-Logo angezeigt werden kann.
    /// </summary>
    public static class SimHardwareHelper
    {
        public record SimBrand(string Name, string Key, string[] Aliases);

        /// <summary>Die vier offiziell unterstützten Marken (Logo-Assets unter /images/logos/{Key}.svg).</summary>
        public static readonly System.Collections.Generic.IReadOnlyList<SimBrand> Brands =
            new System.Collections.Generic.List<SimBrand>
            {
                new("Fanatec",      "fanatec",      new[] { "fanatec", "csl", "clubsport", "podium", "gran turismo dd" }),
                new("MOZA Racing",  "moza",         new[] { "moza" }),
                new("Logitech G",   "logitech",     new[] { "logitech", "logi", "driving force", "g29", "g920", "g923", "g25", "g27" }),
                new("Thrustmaster", "thrustmaster", new[] { "thrustmaster", "tmx", "t300", "t248", "t150", "t-gt", "ts-xw", "ts-pc", "t500" }),
                new("Gamepad",      "gamepad",      new[] { "gamepad", "controller", "pad", "dualsense", "dualshock", "dual sense", "dual shock", "xbox controller", "ps controller" }),
            };

        /// <summary>
        /// Findet die Hardware-Marke zu einem frei eingegebenen Text (z.B. "Fanatec CSL DD",
        /// "Moza R9", "Logitech G923", "Gamepad"). Gibt null zurück, wenn nichts Passendes erkannt wird.
        /// </summary>
        public static SimBrand? Resolve(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var s = input.Trim().ToLowerInvariant();

            // 1) Primär: Markenname direkt enthalten.
            foreach (var b in Brands)
            {
                if (s.Contains(b.Name.ToLowerInvariant()) || s.Contains(b.Key))
                {
                    return b;
                }
            }

            // 2) Sekundär: bekannte Modell-Aliase.
            foreach (var b in Brands)
            {
                foreach (var a in b.Aliases)
                {
                    if (s.Contains(a))
                    {
                        return b;
                    }
                }
            }

            return null;
        }
    }
}
