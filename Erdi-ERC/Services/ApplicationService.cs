using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Microsoft.EntityFrameworkCore;

namespace Erdi_ERC.Services
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
        private readonly IApplicationTargetingService _targeting;
        private readonly ILogger<ApplicationService> _logger;

        public ApplicationService(
            AppDbContext db,
            IAdminAuditService audit,
            IWebhookAutomationService webhookAuto,
            IStaticDataCache staticCache,
            IApplicationTargetingService targeting,
            ILogger<ApplicationService> logger)
        {
            _db = db;
            _audit = audit;
            _webhookAuto = webhookAuto;
            _staticCache = staticCache;
            _targeting = targeting;
            _logger = logger;
        }

        // ── Submit ────────────────────────────────────────────────────────────────

        public async Task<SubmitApplicationResult> SubmitAsync(SubmitApplicationCommand cmd, CancellationToken ct)
        {
            var league = await _db.Leagues.FirstOrDefaultAsync(l => l.Id == cmd.TargetLeagueId, ct);
            if (league is null)
                throw new InvalidOperationException($"Liga '{cmd.TargetLeagueId}' existiert nicht.");
            if (league.IsArchived || !league.AcceptsApplications)
                throw new InvalidOperationException("Diese Liga nimmt keine Bewerbungen mehr an.");

            // Season serverseitig ermitteln — Client kann sie NICHT manipulieren.
            var season = await _targeting.ResolveTargetSeasonAsync(league.Id, ct);

            // Dedup: offener Pending für (User, Liga, Season)?
            var hasOpen = await _db.Applications.AsNoTracking().AnyAsync(
                a => a.DiscordId == cmd.DiscordId
                    && a.TargetLeagueId == cmd.TargetLeagueId
                    && a.Season == season
                    && a.Status == (int)ApplicationStatus.Pending,
                ct);
            if (hasOpen)
            {
                var existing = await _db.Applications.AsNoTracking().FirstAsync(
                    a => a.DiscordId == cmd.DiscordId
                        && a.TargetLeagueId == cmd.TargetLeagueId
                        && a.Season == season
                        && a.Status == (int)ApplicationStatus.Pending,
                    ct);
                return SubmitApplicationResult.AlreadyPending(existing);
            }

            // Capacity-Check: nur für Stammfahrer → ggf. Warteliste (pro Season gescoped).
            var isStamm = string.Equals(cmd.Role, "Stammfahrer", StringComparison.OrdinalIgnoreCase);
            if (isStamm && league.Capacity.HasValue)
            {
                // Akzeptierte Bewerbungen dieser Season zählen, nicht die Standing-Counts
                // (Accept erzeugt ein Standing — die doppelte Zählung würde zu false-positives führen).
                var acceptedInSeason = await _db.Applications
                    .Where(a => a.TargetLeagueId == league.Id
                        && a.Season == season
                        && a.Status == (int)ApplicationStatus.Accepted)
                    .CountAsync(ct);
                // Zusätzlich existierende Standings aus früheren Seasons, die noch in der Liga
                // aktiv sind (z.B. wenn die Liga keine eigene Standings-Cleanup pro Season hat).
                var historicalStandings = await _db.DriverStandings
                    .Where(s => s.LeagueId == league.Id && !s.IsReserveDriver)
                    .CountAsync(ct);
                var occupied = Math.Max(acceptedInSeason, historicalStandings);

                if (occupied >= league.Capacity.Value)
                {
                    return await AddToWaitlistAsync(cmd, league, season, ct);
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
                Season = season,
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
                $"League={league.Name}, Season={season}, Role={cmd.Role}");
            await _db.SaveChangesAsync(ct);

            await _webhookAuto.FireAsync(WebhookEvents.ApplicationSubmitted, new()
            {
                ["DiscordName"] = cmd.DiscordName,
                ["GamerTag"] = cmd.GamerTag,
                ["Platform"] = cmd.Platform,
                ["League"] = league.Name,
                ["Season"] = season,
                ["Role"] = cmd.Role,
            });

            return SubmitApplicationResult.Submitted(app);
        }

        private async Task<SubmitApplicationResult> AddToWaitlistAsync(
            SubmitApplicationCommand cmd, League league, string season, CancellationToken ct)
        {
            // Dedup: offener Wartelisten-Eintrag pro (User, Liga, Season).
            var existingWaitlist = await _db.WaitlistEntries.AsNoTracking().FirstOrDefaultAsync(
                w => w.DiscordId == cmd.DiscordId
                    && w.LeagueId == league.Id
                    && w.Season == season,
                ct);
            if (existingWaitlist is not null)
                return SubmitApplicationResult.AlreadyWaitlisted(existingWaitlist);

            // Position pro (Liga, Season).
            var position = await _db.WaitlistEntries
                .Where(w => w.LeagueId == league.Id && w.Season == season)
                .CountAsync(ct) + 1;

            var entry = new WaitlistEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                DiscordId = cmd.DiscordId,
                DiscordName = cmd.DiscordName,
                GamerTag = cmd.GamerTag,
                Platform = cmd.Platform,
                LeagueId = league.Id,
                Season = season,
                Position = position,
                CreatedAt = DateTime.UtcNow,
            };
            _db.WaitlistEntries.Add(entry);
            await _db.SaveChangesAsync(ct);

            await _audit.LogAsync("AddWaitlistEntry", "WaitlistEntry", entry.Id,
                $"League={league.Name}, Season={season}, Position={position}");
            await _db.SaveChangesAsync(ct);

            await _webhookAuto.FireAsync(WebhookEvents.ApplicationWaitlisted, new()
            {
                ["DiscordName"] = cmd.DiscordName,
                ["GamerTag"] = cmd.GamerTag,
                ["League"] = league.Name,
                ["Season"] = season,
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
            string? seasonFilter,
            int skip,
            int take,
            CancellationToken ct)
        {
            IQueryable<Application> q = _db.Applications.AsNoTracking();

            if (statusFilter.HasValue)
                q = q.Where(a => a.Status == (int)statusFilter.Value);
            if (!string.IsNullOrWhiteSpace(leagueFilter))
                q = q.Where(a => a.TargetLeagueId == leagueFilter);
            if (!string.IsNullOrWhiteSpace(seasonFilter))
                q = q.Where(a => a.Season == seasonFilter);

            return await q
                .OrderByDescending(a => a.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);
        }

        public async Task<bool> HasOpenApplicationAsync(string discordId, string targetLeagueId, CancellationToken ct)
        {
            // Season-aware: ermittle aktuelle Zielseason, damit der Dedup pro Saison gilt.
            // (User kann gleichzeitig für 2026 + 2027 offen sein.)
            var season = await _targeting.ResolveTargetSeasonAsync(targetLeagueId, ct);
            return await _db.Applications.AnyAsync(
                a => a.DiscordId == discordId
                    && a.TargetLeagueId == targetLeagueId
                    && a.Season == season
                    && a.Status == (int)ApplicationStatus.Pending,
                ct);
        }

        public async Task<IReadOnlyList<WaitlistEntry>> ListWaitlistAsync(string leagueId, string? seasonFilter, CancellationToken ct)
        {
            IQueryable<WaitlistEntry> q = _db.WaitlistEntries.AsNoTracking()
                .Where(w => w.LeagueId == leagueId);
            if (!string.IsNullOrWhiteSpace(seasonFilter))
                q = q.Where(w => w.Season == seasonFilter);

            return await q
                .OrderBy(w => w.Season)
                .ThenBy(w => w.Position)
                .ToListAsync(ct);
        }

        // ── Accept ────────────────────────────────────────────────────────────────

        public async Task<AcceptRejectResult> AcceptAsync(string applicationId, string adminDiscordId, string? note, CancellationToken ct)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                // AsTracking: globaler Default ist NoTracking — ohne Tracking gehen
                // die Status-/DecidedAt-Mutationen beim SaveChanges verloren.
                var app = await _db.Applications.AsTracking().FirstOrDefaultAsync(a => a.Id == applicationId, ct);
                if (app is null) return AcceptRejectResult.NotFound();

                // 1. DriverProfile + 2. DriverGamerTag upsert
                var profile = await UpsertProfileAndTagAsync(
                    app.DiscordId, app.DiscordName, app.GamerTag, app.Platform, adminDiscordId, ct);

                // 3. DriverStanding anlegen — Dedup via Trim().ToLowerInvariant()
                await UpsertStandingAsync(app.TargetLeagueId, app.GamerTag, app.Role, ct);

                // 4. Application-Status
                app.Status = (int)ApplicationStatus.Accepted;
                app.DecidedAt = DateTime.UtcNow;
                app.DecidedByDiscordId = adminDiscordId;
                if (note is not null) app.ReviewNote = note;

                // 5. Audit (zwei Einträge, in derselben Transaktion)
                await _audit.LogAsync("LinkDriverProfile", "DriverProfile", profile.DiscordId,
                    $"Application#{app.Id}, Tag={app.Platform}:{app.GamerTag}");
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

        /// <summary>DriverProfile anlegen/aktualisieren + GamerTag für die Plattform verknüpfen.</summary>
        /// <remarks>
        /// Der eingegebene EA-Name (GamerTag) wird auch als DisplayName am Profil
        /// registriert, wenn dieser frei ist — sonst zeigt die Fahrerkarte den
        /// Discord-Namen statt des EA-Namens, und der Liga-Eintrag
        /// (DriverStandings.Driver = EA-Name) löst nicht auf das Profil auf.
        /// </remarks>
        private async Task<DriverProfile> UpsertProfileAndTagAsync(
            string discordId, string discordName, string gamerTag, string platform,
            string adminDiscordId, CancellationToken ct)
        {
            var profile = await _db.DriverProfiles.AsTracking().FirstOrDefaultAsync(p => p.DiscordId == discordId, ct);
            if (profile is null)
            {
                profile = new DriverProfile
                {
                    DiscordId = discordId,
                    DiscordName = discordName,
                    // EA-Name (GamerTag) ist Default-Anzeige — ohne ihn zeigt die
                    // Fahrerkarte den Discord-Namen statt des EA-Namens.
                    DisplayName = gamerTag.Trim(),
                    PreferredPlatform = platform,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                _db.DriverProfiles.Add(profile);
            }
            else
            {
                profile.UpdatedAt = DateTime.UtcNow;
                if (string.IsNullOrEmpty(profile.PreferredPlatform))
                    profile.PreferredPlatform = platform;
                if (string.IsNullOrWhiteSpace(profile.DisplayName))
                    profile.DisplayName = gamerTag.Trim();
            }

            var existingTag = await _db.DriverGamerTags
                .FirstOrDefaultAsync(t => t.DiscordId == discordId && t.Platform == platform, ct);
            if (existingTag is null)
            {
                var anyTag = await _db.DriverGamerTags.AnyAsync(t => t.DiscordId == discordId, ct);
                _db.DriverGamerTags.Add(new DriverGamerTag
                {
                    DiscordId = discordId,
                    Platform = platform,
                    GamerTag = gamerTag,
                    IsPrimary = !anyTag,
                    LinkedAt = DateTime.UtcNow,
                    LinkedByDiscordId = adminDiscordId,
                });
            }

            return profile;
        }

        /// <summary>Ersatz-/Reservefahrer zählen nicht gegen die Stammfahrer-Kapazität.</summary>
        private static bool IsReserveRole(string role) =>
            string.Equals(role, "Ersatzfahrer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Reservefahrer", StringComparison.OrdinalIgnoreCase);

        /// <summary>Legt ein DriverStanding an, sofern der GamerTag (Trim+Lower) in der Liga noch fehlt.</summary>
        private async Task<bool> UpsertStandingAsync(string leagueId, string gamerTag, string role, CancellationToken ct)
        {
            var normalized = gamerTag.Trim().ToLowerInvariant();
            var existingStanding = await _db.DriverStandings.FirstOrDefaultAsync(
                s => s.LeagueId == leagueId
                    && s.Driver.Trim().ToLower() == normalized,
                ct);
            if (existingStanding is not null) return false;

            _db.DriverStandings.Add(new DriverStanding
            {
                LeagueId = leagueId,
                Driver = gamerTag,
                Team = string.Empty,
                Position = 0,
                Points = 0,
                Wins = 0,
                IsReserveDriver = IsReserveRole(role),
                ReserveForDriver = null,
            });
            return true;
        }

        // ── Reject ────────────────────────────────────────────────────────────────

        public async Task<AcceptRejectResult> RejectAsync(string applicationId, string adminDiscordId, string? note, CancellationToken ct)
        {
            // AsTracking: globaler Default ist NoTracking — sonst wird der Reject nie gespeichert.
            var app = await _db.Applications.AsTracking().FirstOrDefaultAsync(a => a.Id == applicationId, ct);
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
            // AsTracking: PromotedToApplicationId/Note müssen persistiert werden,
            // sonst ist der Eintrag mehrfach promotebar.
            var entry = await _db.WaitlistEntries.AsTracking().FirstOrDefaultAsync(w => w.Id == waitlistEntryId, ct);
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
            entry.Note = $"Auto-promotet zu Application#{app.Id} durch {adminDiscordId}";

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

        // ── Manuelle Registrierung (ohne Bewerbung) ───────────────────────────────

        public async Task<ManualRegisterResult> ManualRegisterAsync(
            ManualRegisterCommand cmd, string adminDiscordId, CancellationToken ct)
        {
            var league = await _db.Leagues.FirstOrDefaultAsync(l => l.Id == cmd.LeagueId, ct);
            if (league is null) return ManualRegisterResult.LeagueNotFound();

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var profile = await UpsertProfileAndTagAsync(
                    cmd.DiscordId, cmd.DiscordName, cmd.GamerTag, cmd.Platform, adminDiscordId, ct);

                var standingCreated = await UpsertStandingAsync(cmd.LeagueId, cmd.GamerTag, cmd.Role, ct);
                if (!standingCreated)
                {
                    // Pending Profile/Tag-Adds verwerfen, sonst persistiert sie ein
                    // späterer SaveChanges im selben Request-Scope.
                    _db.ChangeTracker.Clear();
                    await tx.RollbackAsync(ct);
                    return ManualRegisterResult.AlreadyRegistered();
                }

                await _audit.LogAsync("ManualRegisterDriver", "DriverProfile", profile.DiscordId,
                    $"League={league.Name}, Role={cmd.Role}, Tag={cmd.Platform}:{cmd.GamerTag}, Actor={adminDiscordId}");

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                _staticCache.InvalidateLeagues();
                return ManualRegisterResult.Ok();
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        // ── Self-Service (User) ───────────────────────────────────────────────────

        public async Task<IReadOnlyList<Application>> ListMineAsync(string discordId, CancellationToken ct)
        {
            return await _db.Applications
                .AsNoTracking()
                .Include(a => a.TargetLeague)
                .Where(a => a.DiscordId == discordId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<WaitlistEntry>> ListMyWaitlistAsync(string discordId, CancellationToken ct)
        {
            return await _db.WaitlistEntries
                .AsNoTracking()
                .Include(w => w.League)
                .Where(w => w.DiscordId == discordId && w.PromotedToApplicationId == null)
                .OrderBy(w => w.Position)
                .ToListAsync(ct);
        }

        public async Task<WithdrawResult> WithdrawAsync(string applicationId, string discordId, CancellationToken ct)
        {
            var app = await _db.Applications.AsTracking()
                .Include(a => a.TargetLeague)
                .FirstOrDefaultAsync(a => a.Id == applicationId, ct);
            if (app is null) return WithdrawResult.NotFound();
            if (!string.Equals(app.DiscordId, discordId, StringComparison.Ordinal)) return WithdrawResult.NotOwner();
            if (app.Status != (int)ApplicationStatus.Pending) return WithdrawResult.NotPending();

            _db.Applications.Remove(app);
            await _audit.LogAsync("WithdrawApplication", "Application", app.Id,
                $"League={app.TargetLeagueId}, User={discordId}");
            await _db.SaveChangesAsync(ct);

            await _webhookAuto.FireAsync(WebhookEvents.ApplicationWithdrawn, new()
            {
                ["DiscordName"] = app.DiscordName,
                ["GamerTag"] = app.GamerTag,
                ["League"] = app.TargetLeague?.Name ?? app.TargetLeagueId,
                ["Role"] = app.Role,
            });

            return WithdrawResult.Ok();
        }

        public async Task<WithdrawResult> LeaveWaitlistAsync(string entryId, string discordId, CancellationToken ct)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var entry = await _db.WaitlistEntries.AsTracking()
                    .FirstOrDefaultAsync(w => w.Id == entryId, ct);
                if (entry is null) return WithdrawResult.NotFound();
                if (!string.Equals(entry.DiscordId, discordId, StringComparison.Ordinal)) return WithdrawResult.NotOwner();

                var removedPosition = entry.Position;
                var leagueId = entry.LeagueId;
                _db.WaitlistEntries.Remove(entry);

                // Nachfolgende rücken auf, damit die 1-basierte Reihenfolge lückenlos bleibt.
                var followers = await _db.WaitlistEntries.AsTracking()
                    .Where(w => w.LeagueId == leagueId && w.Position > removedPosition)
                    .ToListAsync(ct);
                foreach (var f in followers) f.Position--;

                await _audit.LogAsync("LeaveWaitlist", "WaitlistEntry", entryId,
                    $"League={leagueId}, Position={removedPosition}, User={discordId}");
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return WithdrawResult.Ok();
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<IReadOnlyList<LeagueCapacityInfo>> GetLeagueCapacityAsync(CancellationToken ct)
        {
            var occupied = await _db.DriverStandings
                .Where(s => !s.IsReserveDriver)
                .GroupBy(s => s.LeagueId)
                .Select(g => new { LeagueId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.LeagueId, x => x.Count, ct);

            var waitlist = await _db.WaitlistEntries
                .Where(w => w.PromotedToApplicationId == null)
                .GroupBy(w => w.LeagueId)
                .Select(g => new { LeagueId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.LeagueId, x => x.Count, ct);

            var leagues = await _staticCache.GetApplicationLeaguesAsync(ct);
            return leagues
                .Select(l => new LeagueCapacityInfo(
                    l.Id,
                    l.Capacity,
                    occupied.GetValueOrDefault(l.Id),
                    waitlist.GetValueOrDefault(l.Id)))
                .ToList();
        }

        // ── Season-Aggregation (Admin) ───────────────────────────────────────────

        public async Task<IReadOnlyList<SeasonSummaryRow>> GetSeasonSummaryAsync(
            string season, CancellationToken ct)
        {
            var leagueDict = await _staticCache.GetAllLeaguesAsync(ct)
                .ContinueWith(t => t.Result.ToDictionary(l => l.Id, l => l.Name), ct);

            var appCounts = await _db.Applications.AsNoTracking()
                .Where(a => a.Season == season)
                .GroupBy(a => new { a.TargetLeagueId, a.Status })
                .Select(g => new { g.Key.TargetLeagueId, g.Key.Status, Count = g.Count() })
                .ToListAsync(ct);

            var waitlistCounts = await _db.WaitlistEntries.AsNoTracking()
                .Where(w => w.Season == season && w.PromotedToApplicationId == null)
                .GroupBy(w => w.LeagueId)
                .Select(g => new { LeagueId = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var byLeague = appCounts
                .GroupBy(x => x.TargetLeagueId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Pending = g.Where(x => x.Status == (int)ApplicationStatus.Pending).Sum(x => x.Count),
                        Accepted = g.Where(x => x.Status == (int)ApplicationStatus.Accepted).Sum(x => x.Count),
                        Rejected = g.Where(x => x.Status == (int)ApplicationStatus.Rejected).Sum(x => x.Count),
                    });

            var result = new List<SeasonSummaryRow>();
            foreach (var leagueId in leagueDict.Keys)
            {
                var counts = byLeague.GetValueOrDefault(leagueId);
                var waitlist = waitlistCounts.FirstOrDefault(w => w.LeagueId == leagueId)?.Count ?? 0;
                result.Add(new SeasonSummaryRow(
                    leagueId,
                    leagueDict[leagueId],
                    season,
                    counts?.Pending ?? 0,
                    counts?.Accepted ?? 0,
                    counts?.Rejected ?? 0,
                    waitlist));
            }
            return result;
        }

        // ── Saison-Wechsel (Admin) ────────────────────────────────────────────────

        public async Task<CloseSeasonResult> CloseSeasonAsync(
            string leagueId,
            string fromSeason,
            string toSeason,
            SeasonCloseMode mode,
            string adminDiscordId,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(fromSeason) || string.IsNullOrWhiteSpace(toSeason))
                return CloseSeasonResult.InvalidSeasons("fromSeason und toSeason sind erforderlich.");
            if (string.Equals(fromSeason, toSeason, StringComparison.Ordinal))
                return CloseSeasonResult.InvalidSeasons("fromSeason und toSeason müssen verschieden sein.");

            var league = await _db.Leagues.AsNoTracking().FirstOrDefaultAsync(l => l.Id == leagueId, ct);
            if (league is null) return CloseSeasonResult.LeagueNotFound();

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                int movedApp = 0, movedWl = 0, rejApp = 0, remWl = 0;

                if (mode == SeasonCloseMode.Rollover)
                {
                    // Offene Pending-Applications in Zielseason übernehmen.
                    var openApps = await _db.Applications.AsTracking()
                        .Where(a => a.TargetLeagueId == leagueId
                            && a.Season == fromSeason
                            && a.Status == (int)ApplicationStatus.Pending)
                        .ToListAsync(ct);
                    foreach (var a in openApps)
                    {
                        a.Season = toSeason;
                        movedApp++;
                    }

                    // Offene Waitlist-Einträge mitnehmen.
                    var openWl = await _db.WaitlistEntries.AsTracking()
                        .Where(w => w.LeagueId == leagueId
                            && w.Season == fromSeason
                            && w.PromotedToApplicationId == null)
                        .ToListAsync(ct);
                    foreach (var w in openWl)
                    {
                        w.Season = toSeason;
                        movedWl++;
                    }
                }
                else // RejectAll
                {
                    // Offene Pending-Applications als Rejected markieren.
                    var openApps = await _db.Applications.AsTracking()
                        .Where(a => a.TargetLeagueId == leagueId
                            && a.Season == fromSeason
                            && a.Status == (int)ApplicationStatus.Pending)
                        .ToListAsync(ct);
                    foreach (var a in openApps)
                    {
                        a.Status = (int)ApplicationStatus.Rejected;
                        a.DecidedAt = DateTime.UtcNow;
                        a.DecidedByDiscordId = adminDiscordId;
                        a.ReviewNote = $"Saison {fromSeason} geschlossen durch {adminDiscordId}";
                        rejApp++;
                    }

                    // Offene Waitlist löschen.
                    var openWl = await _db.WaitlistEntries.AsTracking()
                        .Where(w => w.LeagueId == leagueId
                            && w.Season == fromSeason
                            && w.PromotedToApplicationId == null)
                        .ToListAsync(ct);
                    _db.WaitlistEntries.RemoveRange(openWl);
                    remWl = openWl.Count;
                }

                await _audit.LogAsync("CloseSeason", "League", leagueId,
                    $"From={fromSeason}, To={toSeason}, Mode={mode}, MovedApp={movedApp}, MovedWl={movedWl}, RejApp={rejApp}, RemWl={remWl}, Actor={adminDiscordId}");
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                _staticCache.InvalidateLeagues();

                await _webhookAuto.FireAsync(WebhookEvents.ApplicationSeasonClosed, new()
                {
                    ["League"] = league.Name,
                    ["FromSeason"] = fromSeason,
                    ["ToSeason"] = toSeason,
                    ["Mode"] = mode.ToString(),
                    ["MovedApplications"] = movedApp.ToString(),
                    ["MovedWaitlist"] = movedWl.ToString(),
                    ["RejectedApplications"] = rejApp.ToString(),
                    ["Actor"] = adminDiscordId,
                });

                return CloseSeasonResult.Ok(movedApp, movedWl, rejApp, remWl);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }
    }
}