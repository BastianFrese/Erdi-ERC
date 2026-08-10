using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using Microsoft.EntityFrameworkCore;

namespace <OWNER_HANDLE>_ERC.Services
{
    /// <summary>
    /// Vereinheitlichte Stream-Schedule-Abfrage. Vergleicht gegen UTC, damit
    /// Sommer-/Winterzeit-Wechsel keine Drift verursachen und damit Home und
    /// Admin denselben Termin liefern.
    /// </summary>
    public sealed class StreamScheduleQueryService : IStreamScheduleQueryService
    {
        private readonly AppDbContext _db;

        public StreamScheduleQueryService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<StreamSchedule?> GetNextStreamScheduleAsync(CancellationToken cancellationToken = default)
        {
            var schedules = await _db.StreamSchedules.AsNoTracking().ToListAsync(cancellationToken);
            if (schedules.Count == 0) return null;

            // Alle Zeitpunkte werden als UTC behandelt (DB-Storage, Server-Vergleich).
            var nowUtc = DateTime.UtcNow;

            return schedules
                .Select(x =>
                {
                    var nextStart = x.IsRecurring && x.DayOfWeek.HasValue && x.TimeOfDay.HasValue
                        ? ComputeNextOccurrenceUtc(x.DayOfWeek.Value, x.TimeOfDay.Value, nowUtc)
                        : DateTime.SpecifyKind(x.StartAt, DateTimeKind.Utc);

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
                .Where(x => x.StartAt >= nowUtc)
                .OrderBy(x => x.StartAt)
                .FirstOrDefault();
        }

        /// <summary>
        /// Berechnet den nächsten UTC-Termin eines wiederkehrenden Streams.
        /// Liegt der heutige Termin in der Vergangenheit, wird 7 Tage addiert.
        /// </summary>
        private static DateTime ComputeNextOccurrenceUtc(int dayOfWeek, TimeSpan timeOfDay, DateTime fromUtc)
        {
            var daysUntil = ((dayOfWeek - (int)fromUtc.DayOfWeek) + 7) % 7;
            var candidate = DateTime.SpecifyKind(fromUtc.Date.AddDays(daysUntil).Add(timeOfDay), DateTimeKind.Utc);
            if (candidate < fromUtc)
            {
                candidate = candidate.AddDays(7);
            }
            return candidate;
        }
    }
}
