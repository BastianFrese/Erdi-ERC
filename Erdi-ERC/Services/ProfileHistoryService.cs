using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;

namespace Erdi_ERC.Services
{
    /// <summary>
    /// Liefert die Rennhistorie eines Fahrers (über alle Ligen) als <see cref="DriverDetailViewModel"/>-Fragment.
    /// Die Aggregation lädt alle Ligen inkl. Standings/Races/Finishes — teuer genug, um sie
    /// pro Alias-Set 2 Min. zu cachen (Key = SHA-256-Hash des normalisierten Alias-Sets).
    /// Die aufrufende View bleibt live (user-spezifisches HTML: eigenes Profil, Edit-Buttons).
    /// </summary>
    public sealed class ProfileHistoryService
    {
        private const string CacheKeyPrefix = "profile-history:v1:";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;

        public ProfileHistoryService(AppDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        /// <summary>
        /// Aggregiert Rennen/Siege/Podien/Fastest-Laps/Team/Punkte über alle Ligen für die
        /// übergebenen Fahrer-Aliase. Erwartet getrimmte Aliase; leer → null.
        /// </summary>
        public async Task<DriverDetailViewModel?> GetHistoryAsync(IReadOnlyCollection<string> aliases, CancellationToken ct = default)
        {
            // Die normalisierte Liste ist zugleich Basis des Cache-Keys UND das Match-Set.
            // Sie darf NICHT aus dem zusammengejointen Key zurückgeparst werden: Fahrernamen
            // enthalten selbst das Trennzeichen ("ERC | Max"), ein Split zerlegt sie in
            // Fragmente ("erc ", " max"), die nie auf eine Standings-/Finish-Zeile passen —
            // das Profil bliebe leer, obwohl der Fahrer Ergebnisse hat.
            var normalized = aliases
                .Select(a => a.Trim())
                .Where(a => a.Length > 0)
                .Select(a => a.ToLowerInvariant())
                .Distinct()
                .OrderBy(a => a, StringComparer.Ordinal)
                .ToList();

            if (normalized.Count == 0) return null;

            // Längenpräfix je Eintrag: hält den Hash eindeutig, auch wenn ein Alias das
            // Trennzeichen enthält ("ERC | Max" vs. "ERC" + "Max" sind so unterscheidbar).
            var keyPart = string.Join("|", normalized.Select(a => a.Length + ":" + a));

            var cacheKey = CacheKeyPrefix + Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(keyPart))).ToLowerInvariant();

            if (_cache.TryGetValue<DriverDetailViewModel>(cacheKey, out var cached) && cached is not null)
            {
                return cached;
            }

            var history = await BuildAsync(new HashSet<string>(normalized, StringComparer.OrdinalIgnoreCase), ct);

            _cache.Set(cacheKey, history, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl,
                Priority = CacheItemPriority.Low,
                Size = 1
            });

            return history;
        }

        private async Task<DriverDetailViewModel> BuildAsync(HashSet<string> aliasSet, CancellationToken ct)
        {
            var leagues = await _db.Leagues
                .AsNoTracking()
                .AsSplitQuery()
                .Include(l => l.Standings)
                .Include(l => l.Races).ThenInclude(r => r.Finishes)
                .Include(l => l.Races).ThenInclude(r => r.ReserveAssignments)
                .ToListAsync(ct);

            var races = new List<DriverRaceEntry>();
            int wins = 0, podiums = 0, fastest = 0, totalPoints = 0;
            string? team = null;
            int? driverNumber = null;

            foreach (var l in leagues)
            {
                var standing = l.Standings.FirstOrDefault(s => aliasSet.Contains(s.Driver?.Trim() ?? ""));
                if (standing is not null)
                {
                    totalPoints += standing.Points;
                    team ??= standing.Team;
                    driverNumber ??= standing.DriverNumber;
                }

                foreach (var r in l.Races)

                {
                    var finish = r.Finishes.FirstOrDefault(f => aliasSet.Contains(f.Driver?.Trim() ?? ""));
                    if (finish is null) continue;

                    if (finish.Position == 1) wins++;
                    if (finish.Position is >= 1 and <= 3) podiums++;
                    if (finish.FastestLap) fastest++;

                    var reserveFor = r.ReserveAssignments.FirstOrDefault(a => aliasSet.Contains(a.ReserveDriver?.Trim() ?? ""))?.MainDriver;

                    races.Add(new DriverRaceEntry
                    {
                        RaceId = r.RowId,
                        LeagueId = l.Id,
                        Date = r.Date,
                        Track = r.Track,
                        Position = finish.Position,
                        Points = 0,
                        FastestLap = finish.FastestLap,
                        RaceTimeMs = finish.RaceTimeMs,
                        Team = standing?.Team ?? string.Empty,
                        WasReserve = !string.IsNullOrWhiteSpace(reserveFor),
                        ReserveForDriver = reserveFor
                    });
                }
            }

            var detail = new DriverDetailViewModel
            {
                Driver = string.Empty, // Name setzt der Controller aus dem Profil
                Team = team ?? string.Empty,
                DriverNumber = driverNumber,
                TotalPoints = totalPoints,
                Wins = wins,
                Podiums = podiums,
                FastestLaps = fastest,
                BestFinish = races.Where(r => r.Position > 0).Select(r => (int?)r.Position).DefaultIfEmpty(null).Min(),
                AverageFinish = races.Where(r => r.Position > 0).Select(r => (double)r.Position).DefaultIfEmpty().Average(),
                Races = races.OrderByDescending(r => r.Date).ToList()
            };

            return detail;
        }
    }
}