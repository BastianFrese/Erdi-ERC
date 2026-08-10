using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using Microsoft.EntityFrameworkCore;

namespace <OWNER_HANDLE>_ERC.Services
{
    /// <summary>
    /// Implementierung von <see cref="IApplicationService"/>. Nutzt den scoped
    /// <see cref="AppDbContext"/> für State-Lookups, <see cref="IAdminAuditService"/>
    /// für Audit-Trail, <see cref="IWebhookAutomationService"/> für Discord-Posts
    /// (fire-and-forget post-commit) und <see cref="IStaticDataCache"/> für
    /// Cache-Invalidierung nach Liga-Mutationen.
    /// </summary>
    public sealed class ApplicationService : IApplicationService
    {
        private readonly AppDbContext _db;
        private readonly IAdminAuditService _audit;
        private readonly IWebhookAutomationService _webhookAuto;
        private readonly IStaticDataCache _staticCache;
        private readonly ILogger<ApplicationService> _logger;

        public ApplicationService(
            AppDbContext db,
            IAdminAuditService audit,
            IWebhookAutomationService webhookAuto,
            IStaticDataCache staticCache,
            ILogger<ApplicationService> logger)
        {
            _db = db;
            _audit = audit;
            _webhookAuto = webhookAuto;
            _staticCache = staticCache;
            _logger = logger;
        }

        // ── Submit ────────────────────────────────────────────────────────────────

        public async Task<SubmitApplicationResult> SubmitAsync(SubmitApplicationCommand cmd, CancellationToken ct)
        {
            var league = await _db.Leagues.FirstOrDefaultAsync(l => l.Id == cmd.TargetLeagueId, ct);
            if (league is null)
                throw new InvalidOperationException($"Liga '{cmd.TargetLeagueId}' existiert nicht.");
            if (league.IsArchived)
                throw new InvalidOperationException("Diese Liga nimmt keine Bewerbungen mehr an.");

            // Dedup: offener Pending für dieselbe Liga?
            var hasOpen = await HasOpenApplicationAsync(cmd.DiscordId, cmd.TargetLeagueId, ct);
            if (hasOpen)
            {
                var existing = await _db.Applications.FirstAsync(
                    a => a.DiscordId == cmd.DiscordId
                        && a.TargetLeagueId == cmd.TargetLeagueId
                        && a.Status == (int)ApplicationStatus.Pending,
                    ct);
                return SubmitApplicationResult.AlreadyPending(existing);
            }

            // Capacity-Check: nur für Stammfahrer → ggf. Warteliste
            var isStamm = string.Equals(cmd.Role, "Stammfahrer", StringComparison.OrdinalIgnoreCase);
            if (isStamm && league.Capacity.HasValue)
            {
                var currentStamm = await _db.DriverStandings
                    .Where(s => s.LeagueId == league.Id && !s.IsReserveDriver)
                    .CountAsync(ct);

                if (currentStamm >= league.Capacity.Value)
                {
                    return await AddToWaitlistAsync(cmd, league, ct);
                }
            }

            // Reguläre Bewerbung
            var app = new Application
            {
                Id = Guid.NewGuid().ToString("N"),
                DiscordId = cmd.DiscordId,
                DiscordName = cmd.DiscordName,
                GamerTag = cmd.GamerTag,
                Platform = cmd.Platform,
                TargetLeagueId = league.Id,
                Role = cmd.Role,
                Motivation = cmd.Motivation,
                Status = (int)ApplicationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                DiscordJoinWarning = cmd.DiscordJoinWarning,
                DiscordJoinWarningDetail = cmd.DiscordJoinWarningDetail,
            };
            _db.Applications.Add(app);
            await _db.SaveChangesAsync(ct);

            await _audit.LogAsync("SubmitApplication", "Application", app.Id,
                $"League={league.Name}, Role={cmd.Role}");
            await _db.SaveChangesAsync(ct);

            await _webhookAuto.FireAsync(WebhookEvents.ApplicationSubmitted, new()
            {
                ["DiscordName"] = cmd.DiscordName,
                ["GamerTag"] = cmd.GamerTag,
                ["Platform"] = cmd.Platform,
                ["League"] = league.Name,
                ["Role"] = cmd.Role,
            });

            return SubmitApplicationResult.Submitted(app);
        }

        private async Task<SubmitApplicationResult> AddToWaitlistAsync(
            SubmitApplicationCommand cmd, League league, CancellationToken ct)
        {
            var existingWaitlist = await _db.WaitlistEntries.FirstOrDefaultAsync(
                w => w.DiscordId == cmd.DiscordId && w.LeagueId == league.Id, ct);
            if (existingWaitlist is not null)
                return SubmitApplicationResult.AlreadyWaitlisted(existingWaitlist);

            var position = await _db.WaitlistEntries
                .Where(w => w.LeagueId == league.Id)
                .CountAsync(ct) + 1;

            var entry = new WaitlistEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                DiscordId = cmd.DiscordId,
                DiscordName = cmd.DiscordName,
                GamerTag = cmd.GamerTag,
                Platform = cmd.Platform,
                LeagueId = league.Id,
                Position = position,
                CreatedAt = DateTime.UtcNow,
            };
            _db.WaitlistEntries.Add(entry);
            await _db.SaveChangesAsync(ct);

            await _audit.LogAsync("AddWaitlistEntry", "WaitlistEntry", entry.Id,
                $"League={league.Name}, Position={position}");
            await _db.SaveChangesAsync(ct);

            await _webhookAuto.FireAsync(WebhookEvents.ApplicationWaitlisted, new()
            {
                ["DiscordName"] = cmd.DiscordName,
                ["GamerTag"] = cmd.GamerTag,
                ["League"] = league.Name,
                ["Position"] = position.ToString(),
            });

            return SubmitApplicationResult.Waitlisted(entry);
        }

        // ── Reads ─────────────────────────────────────────────────────────────────

        public async Task<Application?> GetByIdAsync(string id, CancellationToken ct)
        {
            return await _db.Applications
                .Include(a => a.TargetLeague)
                .FirstOrDefaultAsync(a => a.Id == id, ct);
        }

        public async Task<IReadOnlyList<Application>> ListAsync(
            ApplicationStatus? statusFilter,
            string? leagueFilter,
            int skip,
            int take,
            CancellationToken ct)
        {
            IQueryable<Application> q = _db.Applications.AsNoTracking();

            if (statusFilter.HasValue)
                q = q.Where(a => a.Status == (int)statusFilter.Value);
            if (!string.IsNullOrWhiteSpace(leagueFilter))
                q = q.Where(a => a.TargetLeagueId == leagueFilter);

            return await q
                .OrderByDescending(a => a.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);
        }

        public async Task<bool> HasOpenApplicationAsync(string discordId, string targetLeagueId, CancellationToken ct)
        {
            return await _db.Applications.AnyAsync(
                a => a.DiscordId == discordId
                    && a.TargetLeagueId == targetLeagueId
                    && a.Status == (int)ApplicationStatus.Pending,
                ct);
        }

        public async Task<IReadOnlyList<WaitlistEntry>> ListWaitlistAsync(string leagueId, CancellationToken ct)
        {
            return await _db.WaitlistEntries
                .AsNoTracking()
                .Where(w => w.LeagueId == leagueId)
                .OrderBy(w => w.Position)
                .ToListAsync(ct);
        }

        // ── Accept ────────────────────────────────────────────────────────────────

        public async Task<AcceptRejectResult> AcceptAsync(string applicationId, string adminDiscordId, string? note, CancellationToken ct)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == applicationId, ct);
                if (app is null) return AcceptRejectResult.NotFound();

                // 1. DriverProfile upsert
                var profile = await _db.DriverProfiles.FirstOrDefaultAsync(p => p.DiscordId == app.DiscordId, ct);
                if (profile is null)
                {
                    profile = new DriverProfile
                    {
                        DiscordId = app.DiscordId,
                        DiscordName = app.DiscordName,
                        PreferredPlatform = app.Platform,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    };
                    _db.DriverProfiles.Add(profile);
                }
                else
                {
                    profile.UpdatedAt = DateTime.UtcNow;
                    if (string.IsNullOrEmpty(profile.PreferredPlatform))
                        profile.PreferredPlatform = app.Platform;
                }

                // 2. DriverGamerTag upsert
                var existingTag = await _db.DriverGamerTags
                    .FirstOrDefaultAsync(t => t.DiscordId == app.DiscordId && t.Platform == app.Platform, ct);
                if (existingTag is null)
                {
                    var anyTag = await _db.DriverGamerTags.AnyAsync(t => t.DiscordId == app.DiscordId, ct);
                    _db.DriverGamerTags.Add(new DriverGamerTag
                    {
                        DiscordId = app.DiscordId,
                        Platform = app.Platform,
                        GamerTag = app.GamerTag,
                        IsPrimary = !anyTag,
                        LinkedAt = DateTime.UtcNow,
                        LinkedByDiscordId = adminDiscordId,
                    });
                }

                // 3. DriverStanding anlegen — Dedup via Trim().ToLowerInvariant()
                var normalized = app.GamerTag.Trim().ToLowerInvariant();
                var existingStanding = await _db.DriverStandings.FirstOrDefaultAsync(
                    s => s.LeagueId == app.TargetLeagueId
                        && s.Driver.Trim().ToLower() == normalized,
                    ct);
                if (existingStanding is null)
                {
                    var isReserve = string.Equals(app.Role, "Reservefahrer", StringComparison.OrdinalIgnoreCase);
                    _db.DriverStandings.Add(new DriverStanding
                    {
                        LeagueId = app.TargetLeagueId,
                        Driver = app.GamerTag,
                        Team = string.Empty,
                        Position = 0,
                        Points = 0,
                        Wins = 0,
                        IsReserveDriver = isReserve,
                        ReserveForDriver = null,
                    });
                }

                // 4. Application-Status
                app.Status = (int)ApplicationStatus.Accepted;
                app.DecidedAt = DateTime.UtcNow;
                app.DecidedByDiscordId = adminDiscordId;
                if (note is not null) app.ReviewNote = note;

                // 5. Audit (zwei Einträge, in derselben Transaktion)
                await _audit.LogAsync("LinkDriverProfile", "DriverProfile", profile.DiscordId,
                    $"Application***REMOVED***{app.Id}, Tag={app.Platform}:{app.GamerTag}");
                await _audit.LogAsync("AcceptApplication", "Application", app.Id,
                    $"League={app.TargetLeagueId}, Actor={adminDiscordId}");

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                _staticCache.InvalidateLeagues(); // Counts könnten sich ändern

                // 6. Webhook post-commit
                await _webhookAuto.FireAsync(WebhookEvents.ApplicationAccepted, new()
                {
                    ["DiscordName"] = app.DiscordName,
                    ["GamerTag"] = app.GamerTag,
                    ["Platform"] = app.Platform,
                    ["League"] = app.TargetLeagueId,
                    ["Role"] = app.Role,
                    ["Actor"] = adminDiscordId,
                });

                return AcceptRejectResult.Ok(app);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        // ── Reject ────────────────────────────────────────────────────────────────

        public async Task<AcceptRejectResult> RejectAsync(string applicationId, string adminDiscordId, string? note, CancellationToken ct)
        {
            var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == applicationId, ct);
            if (app is null) return AcceptRejectResult.NotFound();

            app.Status = (int)ApplicationStatus.Rejected;
            app.DecidedAt = DateTime.UtcNow;
            app.DecidedByDiscordId = adminDiscordId;
            if (note is not null) app.ReviewNote = note;

            await _audit.LogAsync("RejectApplication", "Application", app.Id,
                $"League={app.TargetLeagueId}, Actor={adminDiscordId}");
            await _db.SaveChangesAsync(ct);

            await _webhookAuto.FireAsync(WebhookEvents.ApplicationRejected, new()
            {
                ["DiscordName"] = app.DiscordName,
                ["GamerTag"] = app.GamerTag,
                ["Platform"] = app.Platform,
                ["League"] = app.TargetLeagueId,
                ["Role"] = app.Role,
                ["Actor"] = adminDiscordId,
            });

            return AcceptRejectResult.Ok(app);
        }

        // ── Waitlist-Promotion ────────────────────────────────────────────────────

        public async Task<PromoteResult> PromoteFromWaitlistAsync(string waitlistEntryId, string adminDiscordId, CancellationToken ct)
        {
            var entry = await _db.WaitlistEntries.FirstOrDefaultAsync(w => w.Id == waitlistEntryId, ct);
            if (entry is null) return PromoteResult.NotFound("Wartelisten-Eintrag nicht gefunden.");

            var app = new Application
            {
                Id = Guid.NewGuid().ToString("N"),
                DiscordId = entry.DiscordId,
                DiscordName = entry.DiscordName,
                GamerTag = entry.GamerTag,
                Platform = entry.Platform,
                TargetLeagueId = entry.LeagueId,
                Role = "Stammfahrer",
                Motivation = null,
                Status = (int)ApplicationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
            };
            _db.Applications.Add(app);

            entry.PromotedToApplicationId = app.Id;
            entry.Note = $"Auto-promotet zu Application***REMOVED***{app.Id} durch {adminDiscordId}";

            await _audit.LogAsync("PromoteFromWaitlist", "WaitlistEntry", entry.Id,
                $"ApplicationId={app.Id}, League={entry.LeagueId}");
            await _db.SaveChangesAsync(ct);

            var league = await _db.Leagues.FirstOrDefaultAsync(l => l.Id == entry.LeagueId, ct);
            await _webhookAuto.FireAsync(WebhookEvents.ApplicationSubmitted, new()
            {
                ["DiscordName"] = entry.DiscordName,
                ["GamerTag"] = entry.GamerTag,
                ["Platform"] = entry.Platform,
                ["League"] = league?.Name ?? entry.LeagueId,
                ["Role"] = "Stammfahrer",
            });

            return PromoteResult.Ok(app);
        }
    }
}