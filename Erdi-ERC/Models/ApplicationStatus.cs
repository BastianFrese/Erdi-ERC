namespace <OWNER_HANDLE>_ERC.Models
{
    /// <summary>
    /// Persistierter Status einer Bewerbung — genau EIN Zustand pro Bewerbung.
    /// Ersetzt die früheren Bool-Flags <c>IsAccepted</c>/<c>IsRejected</c>, die
    /// widersprüchliche Kombinationen zuließen (beides true gleichzeitig).
    /// Die numerischen Werte sind in der DB gespeichert und in der Computed Column
    /// <c>ActiveDiscordKey</c> (Dedup-Guard) referenziert — NICHT umnummerieren!
    /// </summary>
    public enum ApplicationStatus
    {
        /// <summary>Eingegangen, noch nicht entschieden.</summary>
        Open = 0,

        /// <summary>Angenommen (Zeitpunkt in <c>AcceptedAt</c>).</summary>
        Accepted = 1,

        /// <summary>Abgelehnt (Zeitpunkt in <c>RejectedAt</c>); erneute Bewerbung möglich.</summary>
        Rejected = 2
    }
}
