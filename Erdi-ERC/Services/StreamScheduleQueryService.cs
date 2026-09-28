using Erdi_ERC.Data;
using Erdi_ERC.Helpers;
using Erdi_ERC.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Erdi_ERC.Services
{
    /// <summary>
    /// Vereinheitlichte Stream-Schedule-Abfrage — Home und Admin bekommen denselben Termin.
    ///
    /// Alle Vergleiche laufen in SERVER-LOKALZEIT: <c>StreamSchedule.StartAt</c> wird aus
    /// dem Admin-Formular als lokale Wanduhrzeit gespeichert (siehe
    /// Docs/Features/Zeitzonen-Konvention.md). Der frühere Vergleich gegen
    /// <c>DateTime.UtcNow</c> hielt einen Stream im Sommer 2 h zu lange für "kommend"
    /// und Home/Admin zeigten denselben Termin unterschiedlich an.
    /// </summary>
    public sealed class StreamScheduleQueryService : IStreamScheduleQueryService
    {
        private const string CacheKey = "stream-schedule:next:v1";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;

        public StreamScheduleQueryService(AppDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<StreamSchedule?> GetNextStreamScheduleAsync(CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue<StreamSchedule?>(CacheKey, out var cached))
            {
                return cached;
            }

            var result = await ComputeNextAsync(cancellationToken);

            _cache.Set(CacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl,
                Priority = CacheItemPriority.Low,
                Size = 1
            });

            return result;
        }

        private async Task<StreamSchedule?> ComputeNextAsync(CancellationToken cancellationToken)
        {
            var schedules = await _db.StreamSchedules.AsNoTracking().ToListAsync(cancellationToken);
            if (schedules.Count == 0) return null;

            // "Jetzt" in Server-Lokalzeit — die Spalte enthält Wanduhrzeit, kein UTC.
            var nowLocal = DateTime.Now;

            return schedules
                .Select(x =>
                {
                    var nextStart = x.IsRecurring && x.DayOfWeek.HasValue && x.TimeOfDay.HasValue
                        ? StreamScheduleMath.ComputeNextOccurrence(x.DayOfWeek.Value, x.TimeOfDay.Value, nowLocal)
                        : x.StartAt;

                    return new StreamSchedule
                    {
                        Id = x.Id,
                        Title = x.Title,
                        Url = x.Url,
                        DurationMinutes = x.DurationMinutes,
                        IsRecurring = x.IsRecurring,
                        DayOfWeek = x.DayOfWeek,
                        TimeOfDay = x.TimeOfDay,
                        StartAt = nextStart,
                        CreatedAt = x.CreatedAt
                    };
                })
                .Where(x => x.StartAt >= nowLocal)
                .OrderBy(x => x.StartAt)
                .FirstOrDefault();
        }
    }
}
