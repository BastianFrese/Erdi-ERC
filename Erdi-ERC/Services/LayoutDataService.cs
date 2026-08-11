using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Helpers;
using <OWNER_HANDLE>_ERC.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace <OWNER_HANDLE>_ERC.Services
{
    /// <summary>
    /// Liefert die für _Layout.cshtml benötigten Daten (Winner-Team-Theme + aktiver Stream)
    /// aus dem Memory-Cache. Vermeidet 2-3 DB-Roundtrips pro Page-Load.
    /// </summary>
    public sealed class LayoutDataService : ILayoutDataService
    {
        private const string CacheKey = "layout-data:v1";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

        private static readonly LayoutData EmptyData = new(
            WinnerTeamKey: null,
            WinnerCarPrimary: "***REMOVED***e10600",
            WinnerCarPrimaryLight: "***REMOVED***ff2a1f",
            WinnerCarPrimaryDark: "***REMOVED***7a0300",
            WinnerCarSecondary: "***REMOVED***ffffff",
            ActiveStream: null,
            LatestSetupActivityUtc: null);

        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;

        public LayoutDataService(AppDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<LayoutData> GetLayoutDataAsync(CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue<LayoutData>(CacheKey, out var cached) && cached is not null)
            {
                return cached;
            }

            var data = await BuildAsync(cancellationToken);

            _cache.Set(CacheKey, data, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl,
                Priority = CacheItemPriority.Low,
                Size = 1
            });

            return data;
        }

        private async Task<LayoutData> BuildAsync(CancellationToken ct)
        {
            string? winnerTeamKey = null;

            try
            {
                var latestRace = await _db.RaceResults
                    .AsNoTracking()
                    .Include(r => r.ReserveAssignments)
                    .Include(r => r.GuestAssignments)
                    .OrderByDescending(r => r.Date)
                    .ThenByDescending(r => r.RowId)
                    .FirstOrDefaultAsync(ct);

                if (!string.IsNullOrWhiteSpace(latestRace?.Winner))
                {
                    winnerTeamKey = F1TeamsHelper.GetCssKeyForDriver(latestRace.Winner);

                    if (winnerTeamKey == null)
                    {
                        // Fallback: Standings konsultieren – Reserve-Mapping & ReserveForDriver-Kette berücksichtigen.
                        var winnerLower = latestRace.Winner!.ToLower();
                        var leagueId = latestRace.LeagueId;

                        var winnerStanding = await _db.DriverStandings
                            .AsNoTracking()
                            .FirstOrDefaultAsync(s =>
                                s.LeagueId == leagueId
                                && !string.IsNullOrWhiteSpace(s.Driver)
                                && s.Driver.ToLower() == winnerLower, ct);

                        string? resolvedTeam = winnerStanding?.Team;

                        var raceMainDriver = latestRace.ReserveAssignments
                            .FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.ReserveDriver)
                                && a.ReserveDriver.ToLower() == winnerLower)?.MainDriver;

                        // Cross-League-Gast: Winner ist Gastfahrer → Team erbt vom Liga-Hauptfahrer.
                        // Sentinel "(kein Hauptfahrer)" zaehlt als "kein Team".
                        var guestMainDriver = raceMainDriver is null
                            ? latestRace.GuestAssignments?
                                .FirstOrDefault(g => !string.IsNullOrWhiteSpace(g.GuestDriver)
                                    && g.GuestDriver.ToLower() == winnerLower
                                    && !string.IsNullOrWhiteSpace(g.MainDriver)
                                    && g.MainDriver != StatsService.GuestSentinelNoMain)?
                                .MainDriver
                            : null;

                        var effectiveMainDriver = raceMainDriver ?? guestMainDriver;

                        if (string.IsNullOrWhiteSpace(resolvedTeam) && !string.IsNullOrWhiteSpace(effectiveMainDriver))
                        {
                            var mainLower = effectiveMainDriver.ToLower();
                            resolvedTeam = await _db.DriverStandings
                                .AsNoTracking()
                                .Where(s => s.LeagueId == leagueId
                                    && !string.IsNullOrWhiteSpace(s.Driver)
                                    && s.Driver.ToLower() == mainLower)
                                .Select(s => s.Team)
                                .FirstOrDefaultAsync(ct);
                        }

                        if (string.IsNullOrWhiteSpace(resolvedTeam)
                            && winnerStanding?.IsReserveDriver == true
                            && !string.IsNullOrWhiteSpace(winnerStanding.ReserveForDriver))
                        {
                            var fallbackLower = winnerStanding.ReserveForDriver!.ToLower();
                            resolvedTeam = await _db.DriverStandings
                                .AsNoTracking()
                                .Where(s => s.LeagueId == leagueId
                                    && !string.IsNullOrWhiteSpace(s.Driver)
                                    && s.Driver.ToLower() == fallbackLower)
                                .Select(s => s.Team)
                                .FirstOrDefaultAsync(ct);
                        }

                        winnerTeamKey = F1TeamsHelper.GetTeamByName(resolvedTeam)?.CssKey;
                    }
                }
            }
            catch
            {
                // Layout darf hier niemals knallen.
                winnerTeamKey = null;
            }

            var (primary, primaryLight, primaryDark, secondary) = ResolveColors(winnerTeamKey);

            StreamSchedule? activeStream = null;
            try
            {
                // UtcNow konsistent zur Speicherung in AdminCommunityController (UtcNow für recurring).
                var now = DateTime.UtcNow;
                var candidates = await _db.StreamSchedules
                    .AsNoTracking()
                    .Where(s => s.StartAt <= now)
                    .OrderByDescending(s => s.StartAt)
                    .Take(20)
                    .ToListAsync(ct);

                activeStream = candidates.FirstOrDefault(s =>
                    s.StartAt.AddMinutes(s.DurationMinutes <= 0 ? 120 : s.DurationMinutes) >= now);
            }
            catch
            {
                activeStream = null;
            }

            // Jüngste Setup-Aktivität (UpdatedAt deckt auch neu angelegte Setups ab,
            // da beide Timestamps beim Anlegen gesetzt werden). Speist das "!"-Badge
            // am Setups-Navlink; läuft über denselben 30s-Cache wie der Rest.
            DateTime? latestSetupActivity = null;
            try
            {
                latestSetupActivity = await _db.TrackSetups
                    .AsNoTracking()
                    .MaxAsync(x => (DateTime?)x.UpdatedAt, ct);
            }
            catch
            {
                latestSetupActivity = null;
            }

            return new LayoutData(
                WinnerTeamKey: winnerTeamKey,
                WinnerCarPrimary: primary,
                WinnerCarPrimaryLight: primaryLight,
                WinnerCarPrimaryDark: primaryDark,
                WinnerCarSecondary: secondary,
                ActiveStream: activeStream,
                LatestSetupActivityUtc: latestSetupActivity);
        }

        private static (string primary, string primaryLight, string primaryDark, string secondary) ResolveColors(string? teamKey)
        {
            if (string.IsNullOrEmpty(teamKey))
            {
                return (EmptyData.WinnerCarPrimary, EmptyData.WinnerCarPrimaryLight, EmptyData.WinnerCarPrimaryDark, EmptyData.WinnerCarSecondary);
            }

            var team = F1TeamsHelper.Teams.FirstOrDefault(t =>
                t.CssKey.Equals(teamKey, StringComparison.OrdinalIgnoreCase));

            if (team is null)
            {
                return (EmptyData.WinnerCarPrimary, EmptyData.WinnerCarPrimaryLight, EmptyData.WinnerCarPrimaryDark, EmptyData.WinnerCarSecondary);
            }

            return (team.PrimaryColor, Shade(team.PrimaryColor, 0.25), Shade(team.PrimaryColor, -0.55), team.SecondaryColor);
        }

        private static string Shade(string hex, double factor)
        {
            if (string.IsNullOrWhiteSpace(hex) || hex[0] != '***REMOVED***' || hex.Length != 7) return hex;
            int r = Convert.ToInt32(hex.Substring(1, 2), 16);
            int g = Convert.ToInt32(hex.Substring(3, 2), 16);
            int b = Convert.ToInt32(hex.Substring(5, 2), 16);
            int Adjust(int c) => Math.Max(0, Math.Min(255, (int)Math.Round(c + (factor >= 0 ? (255 - c) * factor : c * factor))));
            return $"***REMOVED***{Adjust(r):X2}{Adjust(g):X2}{Adjust(b):X2}";
        }
    }
}
