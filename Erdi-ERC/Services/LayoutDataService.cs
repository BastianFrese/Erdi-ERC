using Erdi_ERC.Data;
using Erdi_ERC.Helpers;
using Erdi_ERC.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Erdi_ERC.Services
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
            WinnerCarPrimary: "#e10600",
            WinnerCarPrimaryLight: "#ff2a1f",
            WinnerCarPrimaryDark: "#7a0300",
            WinnerCarSecondary: "#ffffff",
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
            // 3 unabhängige DB-Reads AUFEINANDERFOLGEND statt parallel: alle nutzen
            // denselben scoped AppDbContext, und parallele Queries auf einem Context
            // werfen "A second operation was started on this context instance ..."
            // (EF-Core erlaubt kein Concurrent-Use). Die Folge-Resolution
            // (Winner-Team-Lookup) haengt am ersten Read und bleibt sequentiell.
            var latestRace = await LoadLatestRaceAsync(ct);
            var activeStream = await LoadActiveStreamAsync(ct);
            var latestSetupActivity = await LoadLatestSetupActivityAsync(ct);

            var winnerTeamKey = await ResolveWinnerTeamKeyAsync(latestRace, ct);
            var (primary, primaryLight, primaryDark, secondary) = ResolveColors(winnerTeamKey);

            return new LayoutData(
                WinnerTeamKey: winnerTeamKey,
                WinnerCarPrimary: primary,
                WinnerCarPrimaryLight: primaryLight,
                WinnerCarPrimaryDark: primaryDark,
                WinnerCarSecondary: secondary,
                ActiveStream: activeStream,
                LatestSetupActivityUtc: latestSetupActivity);
        }

        private async Task<RaceResult?> LoadLatestRaceAsync(CancellationToken ct)
        {
            try
            {
                return await _db.RaceResults
                    .AsNoTracking()
                    .AsSplitQuery()
                    .Include(r => r.ReserveAssignments)
                    .Include(r => r.GuestAssignments)
                    .OrderByDescending(r => r.Date)
                    .ThenByDescending(r => r.RowId)
                    .FirstOrDefaultAsync(ct);
            }
            catch
            {
                return null;
            }
        }

        private async Task<StreamSchedule?> LoadActiveStreamAsync(CancellationToken ct)
        {
            try
            {
                // Server-Lokalzeit: StreamSchedule.StartAt wird als lokale Wanduhrzeit
                // gespeichert (Admin-Formular, siehe Docs/Features/Zeitzonen-Konvention.md).
                // Mit DateTime.UtcNow erschien das "Live jetzt"-Banner im Sommer erst 2 h
                // nach Streamstart und blieb entsprechend 2 h zu lange stehen.
                var now = DateTime.Now;
                var candidates = await _db.StreamSchedules
                    .AsNoTracking()
                    .Where(s => s.StartAt <= now)
                    .OrderByDescending(s => s.StartAt)
                    .Take(20)
                    .ToListAsync(ct);

                return candidates.FirstOrDefault(s =>
                    s.StartAt.AddMinutes(s.DurationMinutes <= 0 ? 120 : s.DurationMinutes) >= now);
            }
            catch
            {
                return null;
            }
        }

        private async Task<DateTime?> LoadLatestSetupActivityAsync(CancellationToken ct)
        {
            try
            {
                return await _db.TrackSetups
                    .AsNoTracking()
                    .MaxAsync(x => (DateTime?)x.UpdatedAt, ct);
            }
            catch
            {
                return null;
            }
        }

        private async Task<string?> ResolveWinnerTeamKeyAsync(RaceResult? latestRace, CancellationToken ct)
        {
            if (latestRace is null || string.IsNullOrWhiteSpace(latestRace.Winner))
                return null;

            var winnerTeamKey = F1TeamsHelper.GetCssKeyForDriver(latestRace.Winner);
            if (winnerTeamKey != null) return winnerTeamKey;

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

            return F1TeamsHelper.GetTeamByName(resolvedTeam)?.CssKey;
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
            if (string.IsNullOrWhiteSpace(hex) || hex[0] != '#' || hex.Length != 7) return hex;
            int r = Convert.ToInt32(hex.Substring(1, 2), 16);
            int g = Convert.ToInt32(hex.Substring(3, 2), 16);
            int b = Convert.ToInt32(hex.Substring(5, 2), 16);
            int Adjust(int c) => Math.Max(0, Math.Min(255, (int)Math.Round(c + (factor >= 0 ? (255 - c) * factor : c * factor))));
            return $"#{Adjust(r):X2}{Adjust(g):X2}{Adjust(b):X2}";
        }
    }
}
