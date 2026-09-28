namespace Erdi_ERC.Helpers
{
    /// <summary>
    /// Terminberechnung für wiederkehrende Streams.
    ///
    /// Rechnet bewusst in SERVER-LOKALZEIT: <c>DayOfWeek</c> und <c>TimeOfDay</c> kommen
    /// aus dem Admin-Formular (lokale Wanduhrzeit), und <c>StreamSchedule.StartAt</c>
    /// liegt in derselben Semantik in der DB (siehe Docs/Features/Zeitzonen-Konvention.md).
    ///
    /// Vorher rechnete diese Stelle (doppelt vorhanden in AdminCommunityController und
    /// StreamScheduleQueryService) mit <c>DateTime.UtcNow</c>. Folge: im Sommer lag der
    /// berechnete Termin 2 h daneben, und zwischen 00:00 und 02:00 lokal kippte der
    /// UTC-Wochentag auf den Vortag — ein für Mittwoch 21:00 eingetragener Stream wurde
    /// dann für Donnerstag berechnet.
    /// </summary>
    public static class StreamScheduleMath
    {
        /// <summary>
        /// Nächster Termin (Wanduhrzeit) ab <paramref name="fromLocal"/>.
        /// Liegt der heutige Termin bereits in der Vergangenheit, wird eine Woche addiert.
        /// </summary>
        public static DateTime ComputeNextOccurrence(int dayOfWeek, TimeSpan timeOfDay, DateTime fromLocal)
        {
            var daysUntil = ((dayOfWeek - (int)fromLocal.DayOfWeek) + 7) % 7;
            var candidate = fromLocal.Date.AddDays(daysUntil).Add(timeOfDay);
            if (candidate < fromLocal)
            {
                candidate = candidate.AddDays(7);
            }

            // Ergebnis explizit als Wanduhrzeit (Kind=Unspecified) zurückgeben, damit alle
            // StartAt-Werte der Tabelle gleich behandelt werden: Unspecified == Wanduhrzeit,
            // Utc == echter Zeitpunkt. So fällt ein falsches ToLocalTime() sofort auf.
            return DateTime.SpecifyKind(candidate, DateTimeKind.Unspecified);
        }
    }
}
