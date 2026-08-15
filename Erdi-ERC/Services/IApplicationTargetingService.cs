using Erdi_ERC.Models;

namespace Erdi_ERC.Services
{
    /// <summary>
    /// Ermittelt die Ziel-Season einer Bewerbung pro Liga. Trennt Liga-Konfiguration
    /// (<see cref="League.NextSeason"/>, <see cref="League.ApplicationsOpenForNextSeason"/>)
    /// von der Bewerbungs-Logik — einzige Quelle der Wahrheit, was eine "aktuelle" vs.
    /// "nächste" Season für eine Liga bedeutet. Wird sowohl vom User-Apply-Pfad als auch
    /// vom Admin-Dashboard genutzt.
    /// </summary>
    public interface IApplicationTargetingService
    {
        /// <summary>
        /// Liefert die Season, für die sich ein User bei dieser Liga aktuell bewerben würde.
        /// Priorität: (1) <see cref="League.NextSeason"/> wenn
        /// <see cref="League.ApplicationsOpenForNextSeason"/> true; (2) <see cref="League.CurrentSeason"/>;
        /// (3) Default <c>"current"</c> wenn beides null. Wird vom Service transaktional genutzt,
        /// daher ist Persistenz via <see cref="Application.Season"/>/<see cref="WaitlistEntry.Season"/>
        /// verbindlich — Client kann die Season NICHT überschreiben.
        /// </summary>
        Task<string> ResolveTargetSeasonAsync(string leagueId, CancellationToken ct);

        /// <summary>
        /// Liefert pro Liga die bewerbbare Season sowie ein Flag, ob sie eine
        /// "next-season"-Bewerbung ist. Wird im Apply-Formular genutzt, um den Usern die
        /// Season-Auswahl zu präsentieren.
        /// </summary>
        Task<IReadOnlyList<LeagueTargetingInfo>> GetTargetingInfoAsync(CancellationToken ct);
    }

    /// <summary>Welche Season gilt für Bewerbungen auf eine Liga?</summary>
    public record LeagueTargetingInfo(
        string LeagueId,
        string LeagueName,
        string TargetSeason,
        bool IsNextSeason)
    {
        public bool IsCurrentSeason => !IsNextSeason;
    }
}
