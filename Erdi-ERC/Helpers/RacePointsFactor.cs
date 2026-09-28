namespace Erdi_ERC.Helpers
{
    /// <summary>
    /// Punkte-Faktor für abgebrochene Rennen. Ein Rennen, das vorzeitig endet, vergibt nicht
    /// die vollen Punkte, sondern nur einen Anteil (<see cref="Erdi_ERC.Models.RaceResult.PointsPercent"/>).
    ///
    /// Die Regel lebt bewusst nur hier: StatsService (Liga-Tabelle), die Team-Wertungen und
    /// die Anzeige-Pfade müssen alle denselben Wert sehen, sonst weicht die Konstrukteurs-
    /// wertung von der Fahrertabelle ab.
    /// </summary>
    public static class RacePointsFactor
    {
        /// <summary>Voller Punkteanspruch (regulär beendetes Rennen). Auch der Default
        /// für Bestandsdaten und für Rennen ohne Distanzinformation.</summary>
        public const int Full = 100;

        /// <summary>Wert bei Abbruch nach mindestens <see cref="SeventyFivePercentThreshold"/>
        /// gefahrener Distanz.</summary>
        public const int SeventyFive = 75;

        /// <summary>Wert bei frühem Abbruch.</summary>
        public const int Half = 50;

        /// <summary>Ab dieser gefahrenen Quote gilt der 75-%-Satz statt 50 %.</summary>
        public const double SeventyFivePercentThreshold = 0.75;

        /// <summary>Die im Admin-Formular angebotenen Werte (auch die Validierungs-Whitelist).</summary>
        public static readonly int[] AllowedPercents = { Full, SeventyFive, Half };

        /// <summary>Mappt beliebige Eingaben auf einen erlaubten Wert; alles Unbekannte wird
        /// als <see cref="Full"/> behandelt (fail-open, damit Altdaten nie Punkte verlieren).</summary>
        public static int Normalize(int percent) =>
            Array.IndexOf(AllowedPercents, percent) >= 0 ? percent : Full;

        /// <summary>Wendet den Faktor auf einen ganzzahligen Grundwert an.
        /// 50 % aus 25 ergibt exakt 12,5 — es wird bewusst nicht gerundet.</summary>
        public static decimal Apply(int basePoints, int percent) =>
            basePoints * Normalize(percent) / 100m;

        /// <summary>
        /// Leitet den Faktor aus der gefahrenen Distanz ab: gefahrene Runden des Führenden
        /// gegen die Soll-Runden.
        ///
        /// Unbekannte oder unplausible Distanzen (Zeitrennen, Altdaten, keine gewerteten
        /// Fahrer) ergeben <see cref="Full"/> — ein fehlendes Signal darf nie Punkte kosten.
        /// </summary>
        public static int DeriveFromDistance(int? totalLaps, int? completedLaps)
        {
            if (totalLaps is not > 0 || completedLaps is not > 0)
            {
                return Full;
            }

            var ratio = (double)completedLaps.Value / totalLaps.Value;
            if (ratio >= 1.0)
            {
                return Full;
            }

            return ratio >= SeventyFivePercentThreshold ? SeventyFive : Half;
        }

        /// <summary>Gefahrene Quote in Prozent (für die Review-Anzeige "31 / 44 Runden (70 %)"),
        /// oder null, wenn sie nicht bestimmbar ist.</summary>
        public static int? PercentCompleted(int? totalLaps, int? completedLaps)
        {
            if (totalLaps is not > 0 || completedLaps is not > 0)
            {
                return null;
            }

            return (int)Math.Round(100.0 * completedLaps.Value / totalLaps.Value);
        }
    }
}
