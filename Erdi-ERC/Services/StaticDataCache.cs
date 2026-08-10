using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace <OWNER_HANDLE>_ERC.Services
{
    public sealed class StaticDataCache : IStaticDataCache
    {
        private const string AchievementKey = "static:achievement-defs:v1";
        private const string LeaguesKey = "static:leagues:v1";
        private const string LeaguesApplyKey = "static:leagues:apply:v1";
        private static readonly TimeSpan AchievementTtl = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan LeaguesTtl = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan LeaguesApplyTtl = TimeSpan.FromMinutes(5);

        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;

        public StaticDataCache(AppDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<IReadOnlyList<AchievementDefinition>> GetActiveAchievementDefinitionsAsync(CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue<IReadOnlyList<AchievementDefinition>>(AchievementKey, out var cached) && cached is not null)
            {
                return cached;
            }

            var defs = await _db.AchievementDefinitions
                .Where(d => d.IsActive)
                .OrderBy(d => d.SortOrder)
                .ToListAsync(cancellationToken);

            _cache.Set(AchievementKey, (IReadOnlyList<AchievementDefinition>)defs, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = AchievementTtl,
                Size = 1,
                Priority = CacheItemPriority.Low
            });

            return defs;
        }

        public async Task<IReadOnlyList<League>> GetAllLeaguesAsync(CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue<IReadOnlyList<League>>(LeaguesKey, out var cached) && cached is not null)
            {
                return cached;
            }

            var leagues = await _db.Leagues
                .OrderBy(l => l.Name)
                .ToListAsync(cancellationToken);

            _cache.Set(LeaguesKey, (IReadOnlyList<League>)leagues, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = LeaguesTtl,
                Size = 1,
                Priority = CacheItemPriority.Low
            });

            return leagues;
        }

        public async Task<IReadOnlyList<League>> GetApplicationLeaguesAsync(CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue<IReadOnlyList<League>>(LeaguesApplyKey, out var cached) && cached is not null)
            {
                return cached;
            }

            var leagues = await _db.Leagues
                .AsNoTracking()
                .Where(l => !l.IsArchived && l.AcceptsApplications)
                .OrderBy(l => l.SortOrder)
                .ThenBy(l => l.Name)
                .ToListAsync(cancellationToken);

            _cache.Set(LeaguesApplyKey, (IReadOnlyList<League>)leagues, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = LeaguesApplyTtl,
                Size = 1,
                Priority = CacheItemPriority.Low
            });

            return leagues;
        }

        public void InvalidateAchievementDefinitions() => _cache.Remove(AchievementKey);
        public void InvalidateLeagues()
        {
            _cache.Remove(LeaguesKey);
            _cache.Remove(LeaguesApplyKey);
        }
    }
}
