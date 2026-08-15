using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using Microsoft.EntityFrameworkCore;

namespace <OWNER_HANDLE>_ERC.Services
{
    /// <summary>
    /// Ermittelt die Ziel-Season einer Bewerbung pro Liga. Wird vom
    /// <see cref="ApplicationService"/> transaktional beim Submit genutzt, damit der
    /// Client die Season nicht fälschen kann. Read-Only — keine Mutationen.
    /// </summary>
    public sealed class ApplicationTargetingService : IApplicationTargetingService
    {
        private readonly AppDbContext _db;
        private readonly IStaticDataCache _staticCache;

        public ApplicationTargetingService(AppDbContext db, IStaticDataCache staticCache)
        {
            _db = db;
            _staticCache = staticCache;
        }

        public async Task<string> ResolveTargetSeasonAsync(string leagueId, CancellationToken ct)
        {
            // Read direkt aus DB (nicht Cache) — Accept-Pfad braucht frische Werte,
            // und Liga-Konfiguration wechselt nicht so oft, dass ein Cache hier lohnt.
            var league = await _db.Leagues.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == leagueId, ct);
            if (league is null) return "current";

            // Priorität 1: Next-Season-Toggle explizit an.
            if (league.ApplicationsOpenForNextSeason
                && !string.IsNullOrWhiteSpace(league.NextSeason))
            {
                return league.NextSeason;
            }

            // Priorität 2: aktuelle Season.
            if (!string.IsNullOrWhiteSpace(league.CurrentSeason))
            {
                return league.CurrentSeason;
            }

            // Fallback: Liga hat gar keine Season konfiguriert (z.B. archiviert/neu).
            return "current";
        }

        public async Task<IReadOnlyList<LeagueTargetingInfo>> GetTargetingInfoAsync(CancellationToken ct)
        {
            var leagues = await _staticCache.GetApplicationLeaguesAsync(ct);
            var result = new List<LeagueTargetingInfo>(leagues.Count);
            foreach (var league in leagues)
            {
                var targetSeason = await ResolveTargetSeasonAsync(league.Id, ct);
                var isNext = league.ApplicationsOpenForNextSeason
                    && !string.IsNullOrWhiteSpace(league.NextSeason)
                    && string.Equals(targetSeason, league.NextSeason, StringComparison.Ordinal);
                result.Add(new LeagueTargetingInfo(league.Id, league.Name, targetSeason, isNext));
            }
            return result;
        }
    }
}
