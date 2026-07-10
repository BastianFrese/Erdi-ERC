using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace <OWNER_HANDLE>_ERC.Services
{
    /// <summary>
    /// Read-only-Implementierung: alle Abfragen laufen mit AsNoTracking, es wird
    /// nie gespeichert. Die Triage-Sortierung folgt der Liga-Reihenfolge
    /// (<see cref="League.SortOrder"/>) statt einer hartkodierten Divisions-Liste.
    /// </summary>
    public class ApplicationQueryService : IApplicationQueryService
    {
        private const string StammfahrerRole = "Stammfahrer";
        private const string ErsatzfahrerRole = "Ersatzfahrer";
        private const int ExportPageSize = 10000;

        private readonly AppDbContext _db;

        public ApplicationQueryService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<ApplicationSearchResult> SearchAsync(ApplicationSearchFilter filter)
        {
            var query = _db.ApplicationForms.AsNoTracking().AsQueryable();

            if (filter.Status.HasValue)
                query = query.Where(x => x.Status == filter.Status.Value);

            if (filter.FlaggedOnly)
                query = query.Where(x => x.IsFlagged);

            if (!string.IsNullOrWhiteSpace(filter.LeagueId))
                query = query.Where(x =>
                    x.AssignedLeagueId == filter.LeagueId ||
                    (x.AssignedLeagueId == null && x.AppliedLeagueId == filter.LeagueId));

            if (filter.WithoutLeagueOnly)
                query = query.Where(x => x.AssignedLeagueId == null && x.AppliedLeagueId == null);

            if (filter.DuplicatesOnly)
            {
                // Bewerber mit mehr als einer Bewerbung (gleiche DiscordId) — als
                // Subquery, damit alles in einem SQL-Statement bleibt.
                var duplicateIds = _db.ApplicationForms
                    .Where(a => a.DiscordId != null && a.DiscordId != "")
                    .GroupBy(a => a.DiscordId)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key);
                query = query.Where(x => duplicateIds.Contains(x.DiscordId));
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim();
                query = query.Where(x =>
                    x.DiscordName.Contains(term) ||
                    x.GamingName.Contains(term));
            }

            var totalCount = await query.CountAsync();

            List<ApplicationForm> items;
            if (filter.DuplicatesOnly)
            {
                // Duplikate gruppiert anzeigen: gleiche DiscordId nebeneinander.
                items = await query
                    .OrderBy(x => x.DiscordId)
                    .ThenByDescending(x => x.SubmittedAt)
                    .Skip((filter.Page - 1) * filter.PageSize)
                    .Take(filter.PageSize)
                    .ToListAsync();
            }
            else
            {
                items = filter.Sort switch
                {
                    "newest" => await query.OrderByDescending(x => x.SubmittedAt)
                        .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(),
                    "oldest" => await query.OrderBy(x => x.SubmittedAt)
                        .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(),
                    // Triage-Sortierung: Liga-Reihenfolge → Rolle → neueste zuerst.
                    // Liga-SortOrder per korrelierter Subquery, damit Sortieren + Paging
                    // serverseitig in einem Statement laufen.
                    _ => await query
                        .Select(a => new
                        {
                            App = a,
                            LeagueSort = _db.Leagues
                                .Where(l => l.Id == (a.AssignedLeagueId ?? a.AppliedLeagueId))
                                .Select(l => (int?)l.SortOrder)
                                .FirstOrDefault()
                        })
                        .OrderBy(x => x.LeagueSort == null ? 1 : 0)
                        .ThenBy(x => x.LeagueSort)
                        .ThenBy(x => x.App.Division)
                        .ThenBy(x =>
                            x.App.Role == StammfahrerRole ? 0 :
                            x.App.Role == ErsatzfahrerRole ? 1 : 99)
                        .ThenByDescending(x => x.App.SubmittedAt)
                        .Skip((filter.Page - 1) * filter.PageSize)
                        .Take(filter.PageSize)
                        .Select(x => x.App)
                        .ToListAsync()
                };
            }

            return new ApplicationSearchResult
            {
                Items = items,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }

        public async Task<ApplicationForm?> GetByIdAsync(int id)
            => await _db.ApplicationForms.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);

        public async Task<ApplicationFormWithHistory?> GetWithHistoryAsync(int id)
        {
            var app = await GetByIdAsync(id);
            if (app is null) return null;

            var history = await _db.AdminAuditLogs
                .AsNoTracking()
                .Where(x => x.EntityId == id.ToString() && x.EntityType == "ApplicationForm")
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return new ApplicationFormWithHistory
            {
                Application = app,
                AuditHistory = history.Select(x => new ApplicationAuditEntry
                {
                    Id = (int)x.Id,
                    ApplicationId = id,
                    Action = x.Action,
                    Actor = x.Actor,
                    CreatedAt = x.CreatedAt,
                    Details = x.Details
                }).ToList(),
                Status = app.Status,
                StatusReason = app.IsAccepted ? "Accepted" : app.IsRejected ? "Rejected" : app.IsFlagged ? "FlaggedForReview" : "Pending"
            };
        }

        public async Task<ApplicationStatistics> GetStatisticsAsync()
        {
            var q = _db.ApplicationForms.AsNoTracking();

            // Status-Zähler + jüngster Eingang in EINEM Round-Trip statt fünf:
            // EF übersetzt die Count-Prädikate zu SUM(CASE WHEN … THEN 1 ELSE 0 END).
            var agg = await q
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Accepted = g.Count(x => x.Status == ApplicationStatus.Accepted),
                    Rejected = g.Count(x => x.Status == ApplicationStatus.Rejected),
                    Flagged = g.Count(x => x.IsFlagged && x.Status == ApplicationStatus.Open),
                    LastTime = g.Max(x => (DateTime?)x.SubmittedAt)
                })
                .FirstOrDefaultAsync();

            var total = agg?.Total ?? 0;
            var accepted = agg?.Accepted ?? 0;
            var rejected = agg?.Rejected ?? 0;

            var byDivision = await q
                .GroupBy(x => x.Division)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync();

            var byRole = await q
                .GroupBy(x => x.Role)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync();

            return new ApplicationStatistics
            {
                TotalApplications = total,
                OpenApplications = total - accepted - rejected,
                AcceptedApplications = accepted,
                RejectedApplications = rejected,
                FlaggedForReviewApplications = agg?.Flagged ?? 0,
                ApplicationsByDivision = byDivision.ToDictionary(x => x.Key, x => x.Count),
                ApplicationsByRole = byRole.ToDictionary(x => x.Key, x => x.Count),
                LastApplicationTime = agg?.LastTime
            };
        }

        public async Task<ApplicationMetrics> GetMetricsAsync()
        {
            var q = _db.ApplicationForms.AsNoTracking();
            var now = DateTime.UtcNow;
            var weekCutoff = now.AddDays(-7);
            var monthCutoff = now.AddDays(-30);

            var total = await q.CountAsync();
            var acceptedCount = await q.CountAsync(x => x.Status == ApplicationStatus.Accepted);
            var thisWeek = await q.CountAsync(x => x.SubmittedAt >= weekCutoff);
            var thisMonth = await q.CountAsync(x => x.SubmittedAt >= monthCutoff);

            double avgProcessingHours = 0;
            if (acceptedCount > 0)
            {
                // TimeSpan.TotalHours kann EF nicht übersetzen → nur die zwei Zeitstempel
                // pro angenommener Bewerbung laden und im Speicher rechnen.
                var times = await q
                    .Where(x => x.Status == ApplicationStatus.Accepted && x.AcceptedAt.HasValue)
                    .Select(x => new { x.SubmittedAt, AcceptedAt = x.AcceptedAt!.Value })
                    .ToListAsync();
                if (times.Count > 0)
                    avgProcessingHours = times.Average(t => (t.AcceptedAt - t.SubmittedAt).TotalHours);
            }

            var acceptanceRate = total > 0 ? (double)acceptedCount / total : 0;

            return new ApplicationMetrics
            {
                AverageProcessingTimeHours = avgProcessingHours,
                AcceptanceRate = acceptanceRate,
                RejectionRate = 1.0 - acceptanceRate,
                ApplicationsThisWeek = thisWeek,
                ApplicationsThisMonth = thisMonth
            };
        }

        public async Task<List<LeagueCapacityRow>> GetLeagueCapacitiesAsync()
        {
            var leagues = await _db.Leagues
                .AsNoTracking()
                .Where(l => !l.IsArchived && l.IsOpenForApplications)
                .OrderBy(l => l.SortOrder).ThenBy(l => l.Name)
                .Select(l => new { l.Id, l.Name, l.ApplicationInfo, l.Capacity })
                .ToListAsync();

            if (leagues.Count == 0) return new();

            var leagueIds = leagues.Select(l => l.Id).ToList();

            // Belegte Stammplätze je Liga (Reservefahrer zählen nicht gegen die Kapazität).
            var filledByLeague = (await _db.DriverStandings
                .AsNoTracking()
                .Where(s => leagueIds.Contains(s.LeagueId) && !s.IsReserveDriver && s.Driver != "")
                .GroupBy(s => s.LeagueId)
                .Select(g => new { LeagueId = g.Key, Count = g.Count() })
                .ToListAsync())
                .ToDictionary(x => x.LeagueId, x => x.Count);

            // Offene Bewerbungen je Liga — über den Liga-Link statt Namens-Match.
            var pendingByLeague = (await _db.ApplicationForms
                .AsNoTracking()
                .Where(a => a.Status == ApplicationStatus.Open
                    && a.AppliedLeagueId != null && leagueIds.Contains(a.AppliedLeagueId))
                .GroupBy(a => a.AppliedLeagueId!)
                .Select(g => new { LeagueId = g.Key, Count = g.Count() })
                .ToListAsync())
                .ToDictionary(x => x.LeagueId, x => x.Count);

            return leagues.Select(l => new LeagueCapacityRow
            {
                LeagueId = l.Id,
                Name = l.Name,
                ApplicationInfo = l.ApplicationInfo,
                Capacity = l.Capacity,
                Filled = filledByLeague.TryGetValue(l.Id, out var f) ? f : 0,
                Pending = pendingByLeague.TryGetValue(l.Id, out var p) ? p : 0
            }).ToList();
        }

        public async Task<string> ExportCsvAsync(string? leagueId = null)
        {
            var result = await SearchAsync(new ApplicationSearchFilter
            {
                LeagueId = leagueId,
                PageSize = ExportPageSize,
                Sort = "newest"
            });

            var csv = new StringBuilder();
            csv.AppendLine("DiscordName,GamingName,Platform,Division,Role,Age,AILevel,Status,SubmittedAt,AcceptedAt");

            foreach (var app in result.Items)
            {
                var fields = new[]
                {
                    EscapeCsv(app.DiscordName),
                    EscapeCsv(app.GamingName),
                    EscapeCsv(app.Platform),
                    EscapeCsv(app.Division),
                    EscapeCsv(app.AssignedRole ?? app.Role),
                    app.Age.ToString(),
                    EscapeCsv(app.AiLevel),
                    app.Status.ToString(),
                    app.SubmittedAt.ToString("yyyy-MM-dd HH:mm"),
                    app.AcceptedAt?.ToString("yyyy-MM-dd HH:mm") ?? ""
                };
                csv.AppendLine(string.Join(",", fields));
            }

            return csv.ToString();
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";

            return value;
        }
    }
}
