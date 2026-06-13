using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace <OWNER_HANDLE>_ERC.Services
{
    /// <summary>
    /// Implementierung des Application Management Service mit umfassendem Workflow und Audit-Trail.
    /// </summary>
    public class ApplicationManagementService : IApplicationManagementService
    {
        private readonly AppDbContext _db;
        private readonly IDriverProfileService _driverProfiles;
        private readonly IAdminAuditService _audit;
        private readonly IWebhookAutomationService _webhookAuto;

        public ApplicationManagementService(
            AppDbContext db,
            IDriverProfileService driverProfiles,
            IAdminAuditService audit,
            IWebhookAutomationService webhookAuto)
        {
            _db = db;
            _driverProfiles = driverProfiles;
            _audit = audit;
            _webhookAuto = webhookAuto;
        }

        // ---- Status & Tracking ----

        public async Task<List<ApplicationForm>> GetAllApplicationsAsync(
            string? divisionFilter = null,
            bool? acceptedFilter = null,
            int pageSize = 100,
            int pageNumber = 1,
            bool excludeRejected = false,
            string? sort = null)
        {
            var query = _db.ApplicationForms.AsQueryable();

            if (!string.IsNullOrWhiteSpace(divisionFilter))
                query = query.Where(x => x.Division == divisionFilter);

            if (acceptedFilter.HasValue)
                query = query.Where(x => x.IsAccepted == acceptedFilter.Value);

            if (excludeRejected)
                query = query.Where(x => !x.IsRejected);

            // "newest"/"oldest" = reine Sortierung nach Eingangszeit (was zuletzt/zuerst kam).
            // Default (null) behält die Triage-Gruppierung nach Division → Rolle → neueste zuerst.
            IOrderedQueryable<ApplicationForm> ordered = sort switch
            {
                "newest" => query.OrderByDescending(x => x.SubmittedAt),
                "oldest" => query.OrderBy(x => x.SubmittedAt),
                _ => query
                    .OrderBy(x =>
                        x.Division == "Main Division 1" ? 0 :
                        x.Division == "Second Crossplay Division 2" ? 1 :
                        x.Division == "Rookie Crossplay Division 3" ? 2 :
                        x.Division == "Community Crossplay Event" ? 3 : 99)
                    .ThenBy(x => x.Division)
                    .ThenBy(x =>
                        x.Role == "Stammfahrer" ? 0 :
                        x.Role == "Ersatzfahrer" ? 1 : 99)
                    .ThenByDescending(x => x.SubmittedAt)
            };

            return await ordered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<ApplicationStatistics> GetStatisticsAsync()
        {
            var q = _db.ApplicationForms.AsNoTracking();

            var total = await q.CountAsync();
            var accepted = await q.CountAsync(x => x.IsAccepted);
            var rejected = await q.CountAsync(x => x.IsRejected);

            var byDivision = await q
                .GroupBy(x => x.Division)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync();

            var byRole = await q
                .GroupBy(x => x.Role)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync();

            var lastTime = total > 0
                ? await q.MaxAsync(x => (DateTime?)x.SubmittedAt)
                : null;

            return new ApplicationStatistics
            {
                TotalApplications = total,
                OpenApplications = total - accepted - rejected,
                AcceptedApplications = accepted,
                RejectedApplications = rejected,
                ApplicationsByDivision = byDivision.ToDictionary(x => x.Key, x => x.Count),
                ApplicationsByRole = byRole.ToDictionary(x => x.Key, x => x.Count),
                LastApplicationTime = lastTime
            };
        }

        public async Task<ApplicationFormWithHistory?> GetApplicationWithHistoryAsync(int id)
        {
            var app = await _db.ApplicationForms.FindAsync(id);
            if (app is null) return null;

            var history = await _db.AdminAuditLogs
                .Where(x => x.EntityId == id.ToString() && x.EntityType == "ApplicationForm")
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            var status = DetermineApplicationStatus(app);

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
                Status = status,
                StatusReason = app.IsAccepted ? "Accepted" : app.IsRejected ? "Rejected" : "Pending"
            };
        }

        // ---- Workflow ----

        public async Task<ApplicationActionResult> AcceptApplicationAsync(int id, string actorId, string? overrideLeagueId = null)
        {
            var app = await _db.ApplicationForms.FindAsync(id);
            if (app is null)
                return ApplicationActionResult.ErrorResult("Bewerbung nicht gefunden", "NOT_FOUND");

            if (app.IsAccepted)
                return ApplicationActionResult.ErrorResult("Bewerbung bereits akzeptiert", "ALREADY_ACCEPTED");

            // Ziel-Liga auflösen: explizite Admin-Auswahl schlägt die beworbene Liga.
            var targetLeagueId = !string.IsNullOrWhiteSpace(overrideLeagueId) ? overrideLeagueId : app.AppliedLeagueId;
            League? league = null;
            if (!string.IsNullOrWhiteSpace(targetLeagueId))
            {
                league = await _db.Leagues.FirstOrDefaultAsync(l => l.Id == targetLeagueId && !l.IsArchived);
            }

            try
            {
                // Use an explicit DB transaction so linking the driver profile and
                // setting the application as accepted happen atomically. If linking
                // fails, no partial accept state is persisted.
                await using var tx = await _db.Database.BeginTransactionAsync();

                DriverProfile profile;
                try
                {
                    // Link/create the driver profile first. This SaveChangesAsync call
                    // participates in the transaction.
                    profile = await _driverProfiles.LinkApplicationAsync(app, actorId);
                }
                catch (Exception ex)
                {
                    await _audit.LogAsync("LinkDriverProfileFailed", "DriverProfile", id.ToString(), ex.Message);
                    // Rollback implicit on dispose if not committed.
                    return ApplicationActionResult.ErrorResult($"Profil-Linking fehlgeschlagen: {ex.Message}", "PROFILE_LINKING_FAILED");
                }

                try
                {
                    app.IsAccepted = true;
                    app.AcceptedAt = DateTime.UtcNow;
                    // Eine zuvor abgelehnte Bewerbung gilt nach Annahme nicht mehr als abgelehnt.
                    app.IsRejected = false;
                    app.RejectedAt = null;
                    if (league is not null)
                    {
                        // Division als Liga-Name pflegen (Anzeige, Sortierung, Webhooks).
                        app.Division = league.Name;
                    }
                    await _db.SaveChangesAsync();

                    // Fahrer direkt in die Ziel-Liga eintragen, damit er in Standings
                    // und Autofill auftaucht. Läuft in derselben Transaktion.
                    if (league is not null)
                    {
                        var driverName = ResolveDriverName(profile.DisplayName, app.GamingName);
                        var isReserve = IsReserveRole(app.AssignedRole ?? app.Role);
                        await EnsureStandingAsync(league, driverName, isReserve, id);
                    }

                    await tx.CommitAsync();
                }
                catch (DbUpdateConcurrencyException dex)
                {
                    try { await tx.RollbackAsync(); } catch { }
                    await _audit.LogAsync("AcceptApplicationConcurrency", "ApplicationForm", id.ToString(), dex.Message);
                    return ApplicationActionResult.ErrorResult("Konflikt beim Aktualisieren der Bewerbung. Bitte Seite neu laden und erneut versuchen.", "CONCURRENCY_CONFLICT");
                }
                catch (Exception ex)
                {
                    // Attempt to roll back and report error.
                    try { await tx.RollbackAsync(); } catch { }
                    await _audit.LogAsync("AcceptApplicationError", "ApplicationForm", id.ToString(), ex.Message);
                    return ApplicationActionResult.ErrorResult($"Fehler beim Akzeptieren: {ex.Message}", "ACCEPT_ERROR");
                }

                // Audit/log + webhooks after successful commit
                await _audit.LogAsync("LinkDriverProfile", "DriverProfile", profile.DiscordId,
                    $"Application***REMOVED***{id}, Tag={app.Platform}:{app.GamingName}");

                await _audit.LogAsync("AcceptApplication", "ApplicationForm", id.ToString(),
                    $"Division={app.Division}, User={app.DiscordName}, Actor={actorId}");

                await _webhookAuto.FireAsync(WebhookEvents.ApplicationAccepted, new()
                {
                    ["DiscordName"] = app.DiscordName,
                    ["GamingName"]  = app.GamingName,
                    ["Platform"]    = app.Platform,
                    ["Division"]    = app.Division,
                    ["Role"]        = app.Role,
                    ["Actor"]       = actorId
                });

                var successMessage = league is not null
                    ? $"Bewerbung akzeptiert, Profil erstellt und Liga \"{league.Name}\" zugewiesen"
                    : "Bewerbung akzeptiert und Profil erstellt";
                return ApplicationActionResult.SuccessResult(successMessage, app);
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("AcceptApplicationError", "ApplicationForm", id.ToString(), ex.Message);
                return ApplicationActionResult.ErrorResult($"Fehler beim Akzeptieren: {ex.Message}", "ACCEPT_ERROR");
            }
        }

        public async Task<ApplicationActionResult> RejectApplicationAsync(int id, string reason, string actorId)
        {
            var app = await _db.ApplicationForms.FindAsync(id);
            if (app is null)
                return ApplicationActionResult.ErrorResult("Bewerbung nicht gefunden", "NOT_FOUND");

            if (app.IsAccepted)
                return ApplicationActionResult.ErrorResult("Kann akzeptierte Bewerbung nicht ablehnen", "ALREADY_ACCEPTED");

            try
            {
                // Bewerbung als abgelehnt markieren + Grund an die Review-Notiz anhängen.
                app.IsAccepted = false;
                app.IsRejected = true;
                app.RejectedAt = DateTime.UtcNow;
                app.ReviewNote = string.IsNullOrWhiteSpace(app.ReviewNote) ? reason : app.ReviewNote + "\n" + reason;

                _db.ApplicationForms.Update(app);
                await _db.SaveChangesAsync();

                await _audit.LogAsync("RejectApplication", "ApplicationForm", id.ToString(),
                    $"Reason={reason}, Actor={actorId}");

                await _webhookAuto.FireAsync(WebhookEvents.ApplicationRejected, new()
                {
                    ["DiscordName"] = app.DiscordName,
                    ["GamingName"]  = app.GamingName,
                    ["Platform"]    = app.Platform,
                    ["Reason"]      = reason,
                    ["Actor"]       = actorId
                });

                return ApplicationActionResult.SuccessResult("Bewerbung abgelehnt", app);
            }
            catch (DbUpdateConcurrencyException dex)
            {
                await _audit.LogAsync("RejectApplicationConcurrency", "ApplicationForm", id.ToString(), dex.Message);
                return ApplicationActionResult.ErrorResult("Konflikt beim Ablehnen der Bewerbung. Bitte Seite neu laden und erneut versuchen.", "CONCURRENCY_CONFLICT");
            }
            catch (Exception ex)
            {
                return ApplicationActionResult.ErrorResult($"Fehler beim Ablehnen: {ex.Message}", "REJECT_ERROR");
            }
        }

        public async Task<ApplicationActionResult> UnacceptApplicationAsync(int id, string reason, string actorId)
        {
            var app = await _db.ApplicationForms.FindAsync(id);
            if (app is null)
                return ApplicationActionResult.ErrorResult("Bewerbung nicht gefunden", "NOT_FOUND");

            if (!app.IsAccepted)
                return ApplicationActionResult.ErrorResult("Bewerbung ist nicht akzeptiert", "NOT_ACCEPTED");

            try
            {
                // Use a transaction to make the unaccept operation atomic
                await using var tx = await _db.Database.BeginTransactionAsync();

                app.IsAccepted = false;
                app.AcceptedAt = null;
                // Clear assigned role
                app.AssignedRole = null;
                // Append audit note
                app.ReviewNote = string.IsNullOrWhiteSpace(app.ReviewNote) ? reason : app.ReviewNote + "\n" + reason;

                // Ensure EF marks the entity as modified
                _db.Entry(app).State = EntityState.Modified;
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                await _audit.LogAsync("UnacceptApplication", "ApplicationForm", id.ToString(),
                    $"Reason={reason}, Actor={actorId}");

                // No webhook for unaccept

                return ApplicationActionResult.SuccessResult("Akzeptanz rückgängig gemacht", app);
            }
            catch (DbUpdateConcurrencyException dex)
            {
                await _audit.LogAsync("UnacceptApplicationConcurrency", "ApplicationForm", id.ToString(), dex.Message);
                return ApplicationActionResult.ErrorResult("Konflikt beim Rückgängigmachen der Akzeptanz. Bitte Seite neu laden und erneut versuchen.", "CONCURRENCY_CONFLICT");
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("UnacceptApplicationError", "ApplicationForm", id.ToString(), ex.Message);
                return ApplicationActionResult.ErrorResult($"Fehler beim Rückgängigmachen: {ex.Message}", "UNACCEPT_ERROR");
            }
        }

        public async Task<ApplicationActionResult> DeleteApplicationAsync(int id, string actorId)
        {
            var app = await _db.ApplicationForms.FindAsync(id);
            if (app is null)
                return ApplicationActionResult.ErrorResult("Bewerbung nicht gefunden", "NOT_FOUND");

            try
            {
                _db.ApplicationForms.Remove(app);
                await _db.SaveChangesAsync();

                await _audit.LogAsync("DeleteApplication", "ApplicationForm", id.ToString(), $"Actor={actorId}");

                return ApplicationActionResult.SuccessResult("Bewerbung gelöscht", app);
            }
            catch (DbUpdateConcurrencyException dex)
            {
                await _audit.LogAsync("DeleteApplicationConcurrency", "ApplicationForm", id.ToString(), dex.Message);
                return ApplicationActionResult.ErrorResult("Konflikt beim Löschen der Bewerbung. Bitte Seite neu laden und erneut versuchen.", "CONCURRENCY_CONFLICT");
            }
            catch (Exception ex)
            {
                return ApplicationActionResult.ErrorResult($"Fehler beim Löschen: {ex.Message}", "DELETE_ERROR");
            }
        }

        public async Task<ApplicationActionResult> FlagForReviewAsync(int id, string reason, string actorId)
        {
            var app = await _db.ApplicationForms.FindAsync(id);
            if (app is null)
                return ApplicationActionResult.ErrorResult("Bewerbung nicht gefunden", "NOT_FOUND");

            try
            {
                await _audit.LogAsync("FlagApplicationForReview", "ApplicationForm", id.ToString(),
                    $"Reason={reason}, Actor={actorId}");

                // Flagging ist ein interner Admin-Vorgang; kein automatisches Discord-Event.

                return ApplicationActionResult.SuccessResult("Bewerbung gekennzeichnet", app);
            }
            catch (Exception ex)
            {
                return ApplicationActionResult.ErrorResult($"Fehler beim Kennzeichnen: {ex.Message}", "FLAG_ERROR");
            }
        }

        // ---- Automatisierung ----

        public async Task<int> RemoveExpiredApplicationsAsync()
        {
            var cutoff = DateTime.UtcNow.AddHours(-48);
            var expired = await _db.ApplicationForms
                .Where(x => x.IsAccepted && x.AcceptedAt.HasValue && x.AcceptedAt.Value <= cutoff)
                .ToListAsync();

            if (expired.Count == 0)
                return 0;

            _db.ApplicationForms.RemoveRange(expired);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("RemoveExpiredApplications", "ApplicationForm", "*",
                $"Removed {expired.Count} expired applications");

            return expired.Count;
        }

        public async Task<BatchApplicationResult> AcceptMultipleAsync(int[] applicationIds, string actorId)
        {
            var result = new BatchApplicationResult();

            foreach (var id in applicationIds)
            {
                var actionResult = await AcceptApplicationAsync(id, actorId);
                result.Details.Add(actionResult);
                result.TotalProcessed++;

                if (actionResult.Success)
                    result.SuccessCount++;
                else
                    result.FailureCount++;
            }

            await _audit.LogAsync("BatchAcceptApplications", "ApplicationForm", "*",
                $"Processed {result.TotalProcessed}, Success={result.SuccessCount}, Failed={result.FailureCount}");

            return result;
        }

        // ---- Reporting ----

        public async Task<string> ExportAsCSVAsync(string? divisionFilter = null)
        {
            var apps = await GetAllApplicationsAsync(divisionFilter, pageSize: 10000);

            var csv = new StringBuilder();
            csv.AppendLine("DiscordName,GamingName,Platform,Division,Role,Age,AILevel,IsAccepted,SubmittedAt,AcceptedAt");

            foreach (var app in apps)
            {
                var fields = new[]
                {
                    EscapeCSV(app.DiscordName),
                    EscapeCSV(app.GamingName),
                    EscapeCSV(app.Platform),
                    EscapeCSV(app.Division),
                    EscapeCSV(app.Role),
                    app.Age.ToString(),
                    EscapeCSV(app.AiLevel),
                    app.IsAccepted ? "Yes" : "No",
                    app.SubmittedAt.ToString("yyyy-MM-dd HH:mm"),
                    app.AcceptedAt?.ToString("yyyy-MM-dd HH:mm") ?? ""
                };
                csv.AppendLine(string.Join(",", fields));
            }

            return csv.ToString();
        }

        public async Task<ApplicationMetrics> GetMetricsAsync()
        {
            var q = _db.ApplicationForms.AsNoTracking();
            var now = DateTime.UtcNow;
            var weekCutoff = now.AddDays(-7);
            var monthCutoff = now.AddDays(-30);

            var total = await q.CountAsync();
            var acceptedCount = await q.CountAsync(x => x.IsAccepted);
            var thisWeek = await q.CountAsync(x => x.SubmittedAt >= weekCutoff);
            var thisMonth = await q.CountAsync(x => x.SubmittedAt >= monthCutoff);

            double avgProcessingHours = 0;
            if (acceptedCount > 0)
            {
                // TimeSpan.TotalHours kann EF nicht übersetzen → nur die zwei Zeitstempel
                // pro angenommener Bewerbung laden und im Speicher rechnen.
                var times = await q
                    .Where(x => x.IsAccepted && x.AcceptedAt.HasValue)
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

        // ---- Helpers ----

        private ApplicationStatus DetermineApplicationStatus(ApplicationForm app)
        {
            if (app.IsAccepted)
            {
                var expiry = app.AcceptedAt?.AddHours(48);
                if (expiry < DateTime.UtcNow)
                    return ApplicationStatus.Expired;
                return ApplicationStatus.Accepted;
            }

            if (app.IsRejected)
                return ApplicationStatus.Rejected;

            return ApplicationStatus.Pending;
        }

        private static string EscapeCSV(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                return $"\"{value.Replace("\"", "\"\"")}\"";

            return value;
        }

        // ── Liga-Zuweisung ───────────────────────────────────────────────────────

        public async Task<ApplicationActionResult> AssignToLeagueAsync(int id, string leagueId, string? assignedRole, string actorId)
        {
            var app = await _db.ApplicationForms.FindAsync(id);
            if (app is null)
                return ApplicationActionResult.ErrorResult("Bewerbung nicht gefunden", "NOT_FOUND");

            var league = await _db.Leagues.FirstOrDefaultAsync(l => l.Id == leagueId && !l.IsArchived);
            if (league is null)
                return ApplicationActionResult.ErrorResult("Liga nicht gefunden oder archiviert", "LEAGUE_NOT_FOUND");

            try
            {
                var driverName = await ResolveDriverNameAsync(app);
                var isReserve = IsReserveRole(assignedRole ?? app.AssignedRole ?? app.Role);
                var previousDivision = app.Division;

                await EnsureStandingAsync(league, driverName, isReserve, id);
                await RemoveEmptyStandingsElsewhereAsync(app, league.Id, previousDivision, driverName);

                app.Division = league.Name;
                _db.ApplicationForms.Update(app);
                await _db.SaveChangesAsync();

                await _audit.LogAsync("AssignApplicationToLeague", "ApplicationForm", id.ToString(),
                    $"League={league.Id} ({league.Name}), Driver={driverName}, Actor={actorId}");

                return ApplicationActionResult.SuccessResult($"Liga \"{league.Name}\" zugewiesen", app);
            }
            catch (DbUpdateConcurrencyException dex)
            {
                await _audit.LogAsync("AssignLeagueConcurrency", "ApplicationForm", id.ToString(), dex.Message);
                return ApplicationActionResult.ErrorResult("Konflikt bei der Liga-Zuweisung. Bitte Seite neu laden und erneut versuchen.", "CONCURRENCY_CONFLICT");
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("AssignLeagueError", "ApplicationForm", id.ToString(), ex.Message);
                return ApplicationActionResult.ErrorResult($"Fehler bei der Liga-Zuweisung: {ex.Message}", "ASSIGN_LEAGUE_ERROR");
            }
        }

        /// <summary>Trägt den Fahrer in die Liga ein, falls er dort noch nicht steht.</summary>
        private async Task<bool> EnsureStandingAsync(League league, string driverName, bool isReserve, int applicationId)
        {
            var normalized = driverName.Trim();
            if (normalized.Length == 0)
                return false;

            var normalizedLower = normalized.ToLowerInvariant();
            var exists = await _db.DriverStandings
                .AnyAsync(s => s.LeagueId == league.Id
                    && s.Driver != null
                    && s.Driver.Trim().ToLower() == normalizedLower);
            if (exists)
                return false;

            var standing = new DriverStanding
            {
                LeagueId = league.Id,
                Driver = normalized,
                Team = string.Empty,
                DriverNumber = null,
                Position = 0,
                Points = 0,
                Wins = 0,
                IsReserveDriver = isReserve,
                ReserveForDriver = null,
                ReserveStarts = 0,
                ReservePointsForMain = 0
            };

            _db.DriverStandings.Add(standing);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("AdminAction", "DriverStanding", standing.RowId.ToString(),
                $"Added standing for driver={standing.Driver} in league={league.Id} via application***REMOVED***{applicationId}");

            return true;
        }

        /// <summary>
        /// Entfernt leere Auto-Einträge (0 Punkte/Siege/Position) des Fahrers aus der
        /// ursprünglich beworbenen bzw. zuvor zugewiesenen Liga, wenn der Admin ihn
        /// in eine andere Liga packt. Einträge mit echten Ergebnissen bleiben unangetastet.
        /// </summary>
        private async Task RemoveEmptyStandingsElsewhereAsync(ApplicationForm app, string targetLeagueId, string? previousDivision, string driverName)
        {
            var candidateLeagueIds = new List<string>();
            if (!string.IsNullOrWhiteSpace(app.AppliedLeagueId))
                candidateLeagueIds.Add(app.AppliedLeagueId);

            if (!string.IsNullOrWhiteSpace(previousDivision))
            {
                var previousLeagueId = await _db.Leagues
                    .Where(l => l.Name == previousDivision)
                    .Select(l => l.Id)
                    .FirstOrDefaultAsync();
                if (previousLeagueId is not null)
                    candidateLeagueIds.Add(previousLeagueId);
            }

            candidateLeagueIds = candidateLeagueIds
                .Where(x => !string.Equals(x, targetLeagueId, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (candidateLeagueIds.Count == 0)
                return;

            var normalizedLower = driverName.Trim().ToLowerInvariant();
            var staleStandings = await _db.DriverStandings
                .Where(s => candidateLeagueIds.Contains(s.LeagueId)
                    && s.Driver != null
                    && s.Driver.Trim().ToLower() == normalizedLower
                    && s.Points == 0 && s.Wins == 0 && s.Position == 0)
                .ToListAsync();
            if (staleStandings.Count == 0)
                return;

            _db.DriverStandings.RemoveRange(staleStandings);
            await _db.SaveChangesAsync();

            foreach (var stale in staleStandings)
            {
                await _audit.LogAsync("AdminAction", "DriverStanding", stale.RowId.ToString(),
                    $"Removed empty standing for driver={stale.Driver} in league={stale.LeagueId} after league reassignment of application***REMOVED***{app.Id}");
            }
        }

        /// <summary>Ermittelt den Anzeigenamen des Fahrers über das Profil, sonst den EA-Namen.</summary>
        private async Task<string> ResolveDriverNameAsync(ApplicationForm app)
        {
            DriverProfile? profile = null;
            if (!string.IsNullOrWhiteSpace(app.DiscordId))
                profile = await _driverProfiles.GetByDiscordIdAsync(app.DiscordId);
            profile ??= await _driverProfiles.FindByDriverNameAsync(app.GamingName);

            return ResolveDriverName(profile?.DisplayName, app.GamingName);
        }

        private static string ResolveDriverName(string? displayName, string gamingName)
            => !string.IsNullOrWhiteSpace(displayName) ? displayName.Trim() : gamingName.Trim();

        private static bool IsReserveRole(string? role)
            => string.Equals(role, "Ersatzfahrer", StringComparison.OrdinalIgnoreCase);

        // ── Neue Methoden: Review-Workflow & Rollen ──────────────────────────────
        public async Task<ApplicationForm?> GetApplicationByIdAsync(int id)
            => await _db.ApplicationForms.FindAsync(id);

        public async Task UpdateApplicationDirectAsync(ApplicationForm app, string actorId)
        {
            app.SubmittedAt = app.SubmittedAt; // kein Touch auf SubmittedAt
            _db.ApplicationForms.Update(app);
            await _db.SaveChangesAsync();
        }
    }
}
