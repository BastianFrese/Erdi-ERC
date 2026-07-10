using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using Microsoft.EntityFrameworkCore;

namespace <OWNER_HANDLE>_ERC.Services
{
    /// <summary>
    /// State-Machine des Bewerbungs-Workflows. Jeder Übergang ist transaktional,
    /// auditiert (Audit VOR dem Save bzw. LogAndSave nach Commit — siehe
    /// <see cref="IAdminAuditService"/>) und hat genau einen Code-Pfad.
    /// </summary>
    public class ApplicationWorkflowService : IApplicationWorkflowService
    {
        private const string ErsatzfahrerRole = "Ersatzfahrer";
        private const int CleanupAfterDays = 30;
        private const int DefaultTrialDays = 30;
        private const int MaxTrialDays = 365;
        private const int ReviewNoteMaxLength = 1000;

        private readonly AppDbContext _db;
        private readonly IDriverProfileService _driverProfiles;
        private readonly IAdminAuditService _audit;
        private readonly IWebhookAutomationService _webhookAuto;

        public ApplicationWorkflowService(
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

        // ── Accept ───────────────────────────────────────────────────────────────

        public async Task<ApplicationActionResult> AcceptAsync(int id, string? leagueId, string? assignedRole, string actorId)
        {
            var app = await _db.ApplicationForms.FindAsync(id);
            if (app is null)
                return ApplicationActionResult.ErrorResult("Bewerbung nicht gefunden", "NOT_FOUND");

            if (app.Status == ApplicationStatus.Accepted)
                return ApplicationActionResult.ErrorResult("Bewerbung ist bereits angenommen", "ALREADY_ACCEPTED");

            // Ziel-Liga auflösen: explizite Admin-Auswahl schlägt die beworbene Liga.
            // Eine EXPLIZIT gewählte, aber ungültige Liga ist ein Fehler; eine nicht mehr
            // auflösbare beworbene Liga dagegen erlaubt (Annahme ohne Liga-Eintrag).
            League? league = null;
            var explicitChoice = !string.IsNullOrWhiteSpace(leagueId);
            var targetLeagueId = explicitChoice ? leagueId : app.AppliedLeagueId;
            if (!string.IsNullOrWhiteSpace(targetLeagueId))
            {
                league = await _db.Leagues.FirstOrDefaultAsync(l => l.Id == targetLeagueId && !l.IsArchived);
                if (league is null && explicitChoice)
                    return ApplicationActionResult.ErrorResult("Gewählte Liga nicht gefunden oder archiviert", "LEAGUE_NOT_FOUND");
            }

            var effectiveRole = string.IsNullOrWhiteSpace(assignedRole) ? app.Role : assignedRole;
            var capacityWarning = league is not null && !IsReserveRole(effectiveRole)
                ? await BuildCapacityWarningAsync(league)
                : null;

            try
            {
                // Profil-Link, Status-Wechsel und Standings-Eintrag atomar halten:
                // schlägt ein Schritt fehl, bleibt kein halber Annahme-Zustand zurück.
                await using var tx = await _db.Database.BeginTransactionAsync();

                DriverProfile profile;
                try
                {
                    profile = await _driverProfiles.LinkApplicationAsync(app, actorId);
                }
                catch (Exception ex)
                {
                    await _audit.LogAsync("LinkDriverProfileFailed", "DriverProfile", id.ToString(), ex.Message);
                    return ApplicationActionResult.ErrorResult($"Profil-Linking fehlgeschlagen: {ex.Message}", "PROFILE_LINKING_FAILED");
                }

                try
                {
                    app.Status = ApplicationStatus.Accepted;
                    app.AcceptedAt = DateTime.UtcNow;
                    app.RejectedAt = null;
                    // Review-Markierung ist mit der Annahme erledigt.
                    app.IsFlagged = false;
                    app.FlaggedAt = null;
                    if (!string.IsNullOrWhiteSpace(assignedRole))
                        app.AssignedRole = assignedRole;
                    if (league is not null)
                    {
                        app.AssignedLeagueId = league.Id;
                        // Division als Name-Snapshot pflegen (Anzeige, Webhooks, Altdaten-Kompatibilität).
                        app.Division = league.Name;
                    }
                    await _db.SaveChangesAsync();

                    if (league is not null)
                    {
                        var driverName = ResolveDriverName(profile.DisplayName, app.GamingName);
                        await EnsureStandingAsync(league, driverName, IsReserveRole(effectiveRole), id,
                            app.PreferredNumber, app.PreferredTeam);
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
                    try { await tx.RollbackAsync(); } catch { }
                    await _audit.LogAsync("AcceptApplicationError", "ApplicationForm", id.ToString(), ex.Message);
                    return ApplicationActionResult.ErrorResult($"Fehler beim Annehmen: {ex.Message}", "ACCEPT_ERROR");
                }

                // Audit + Webhook nach erfolgreichem Commit — dürfen die Annahme nicht kippen.
                try
                {
                    await _audit.LogAsync("LinkDriverProfile", "DriverProfile", profile.DiscordId,
                        $"Application***REMOVED***{id}, Tag={app.Platform}:{app.GamingName}");
                    await _audit.LogAndSaveAsync("AcceptApplication", "ApplicationForm", id.ToString(),
                        $"League={league?.Id ?? "(keine)"}, Role={app.AssignedRole ?? app.Role}, User={app.DiscordName}, Actor={actorId}");
                    await _webhookAuto.FireAsync(WebhookEvents.ApplicationAccepted, new()
                    {
                        ["DiscordName"] = app.DiscordName,
                        ["DiscordId"]   = app.DiscordId ?? "",
                        ["GamingName"]  = app.GamingName,
                        ["Platform"]    = app.Platform,
                        ["Division"]    = app.Division,
                        ["Role"]        = app.AssignedRole ?? app.Role,
                        ["Actor"]       = actorId
                    });
                }
                catch (Exception postEx)
                {
                    await _audit.LogAsync("AcceptApplicationPostCommitError", "ApplicationForm", id.ToString(), postEx.Message);
                }

                var message = league is not null
                    ? $"Bewerbung angenommen und in Liga \"{league.Name}\" eingetragen"
                    : "Bewerbung angenommen (ohne Liga-Eintrag — keine gültige Liga hinterlegt)";
                if (capacityWarning is not null)
                    message += $" — {capacityWarning}";
                return ApplicationActionResult.SuccessResult(message, app);
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("AcceptApplicationError", "ApplicationForm", id.ToString(), ex.Message);
                return ApplicationActionResult.ErrorResult($"Fehler beim Annehmen: {ex.Message}", "ACCEPT_ERROR");
            }
        }

        // ── Reject ───────────────────────────────────────────────────────────────

        public async Task<ApplicationActionResult> RejectAsync(int id, string reason, string actorId)
        {
            var app = await _db.ApplicationForms.FindAsync(id);
            if (app is null)
                return ApplicationActionResult.ErrorResult("Bewerbung nicht gefunden", "NOT_FOUND");

            if (app.Status == ApplicationStatus.Accepted)
                return ApplicationActionResult.ErrorResult("Angenommene Bewerbung kann nicht abgelehnt werden — zuerst \"Wieder öffnen\"", "ALREADY_ACCEPTED");

            if (app.Status == ApplicationStatus.Rejected)
                return ApplicationActionResult.ErrorResult("Bewerbung ist bereits abgelehnt", "ALREADY_REJECTED");

            try
            {
                app.Status = ApplicationStatus.Rejected;
                app.RejectedAt = DateTime.UtcNow;
                // Eine abgelehnte Bewerbung braucht keine Review-Markierung mehr.
                app.IsFlagged = false;
                app.FlaggedAt = null;
                AppendNote(app, reason);

                // Audit VOR dem Save: Bewerbungs-Update und Audit-Eintrag landen atomar
                // in EINEM SaveChanges (sonst geht das Audit bei einem Fehler verloren).
                await _audit.LogAsync("RejectApplication", "ApplicationForm", id.ToString(),
                    $"Reason={reason}, Actor={actorId}");
                await _db.SaveChangesAsync();

                await _webhookAuto.FireAsync(WebhookEvents.ApplicationRejected, new()
                {
                    ["DiscordName"] = app.DiscordName,
                    ["DiscordId"]   = app.DiscordId ?? "",
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
                return ApplicationActionResult.ErrorResult("Konflikt beim Ablehnen. Bitte Seite neu laden und erneut versuchen.", "CONCURRENCY_CONFLICT");
            }
            catch (Exception ex)
            {
                return ApplicationActionResult.ErrorResult($"Fehler beim Ablehnen: {ex.Message}", "REJECT_ERROR");
            }
        }

        // ── Reopen (vereinheitlicht Unaccept + Unreject) ─────────────────────────

        public async Task<ApplicationActionResult> ReopenAsync(int id, string? note, string actorId)
        {
            var app = await _db.ApplicationForms.FindAsync(id);
            if (app is null)
                return ApplicationActionResult.ErrorResult("Bewerbung nicht gefunden", "NOT_FOUND");

            return app.Status switch
            {
                ApplicationStatus.Accepted => await ReopenFromAcceptedAsync(app, note, actorId),
                ApplicationStatus.Rejected => await ReopenFromRejectedAsync(app, note, actorId),
                _ => ApplicationActionResult.ErrorResult("Bewerbung ist bereits offen", "ALREADY_OPEN")
            };
        }

        private async Task<ApplicationActionResult> ReopenFromRejectedAsync(ApplicationForm app, string? note, string actorId)
        {
            // Dedup-Guard vorab prüfen: sobald die Bewerbung wieder "offen" ist, zählt sie
            // als aktiv — eine zweite aktive Bewerbung desselben Discord-Users würde sonst
            // erst beim Speichern am Unique-Index scheitern (kryptischer DB-Fehler).
            if (!string.IsNullOrWhiteSpace(app.DiscordId))
            {
                var hasOtherActive = await _db.ApplicationForms
                    .AnyAsync(a => a.Id != app.Id
                        && a.DiscordId == app.DiscordId
                        && a.Status != ApplicationStatus.Rejected);
                if (hasOtherActive)
                    return ApplicationActionResult.ErrorResult(
                        "Der Bewerber hat bereits eine andere aktive Bewerbung — Ablehnung kann nicht aufgehoben werden.",
                        "DUPLICATE_ACTIVE");
            }

            try
            {
                app.Status = ApplicationStatus.Open;
                app.RejectedAt = null;
                AppendNote(app, note);

                await _audit.LogAsync("ReopenApplication", "ApplicationForm", app.Id.ToString(),
                    $"Von=Abgelehnt, Note={note}, Actor={actorId}");
                await _db.SaveChangesAsync();

                return ApplicationActionResult.SuccessResult("Ablehnung aufgehoben — Bewerbung ist wieder offen", app);
            }
            catch (DbUpdateConcurrencyException dex)
            {
                await _audit.LogAsync("ReopenApplicationConcurrency", "ApplicationForm", app.Id.ToString(), dex.Message);
                return ApplicationActionResult.ErrorResult("Konflikt beim Wiederöffnen. Bitte Seite neu laden und erneut versuchen.", "CONCURRENCY_CONFLICT");
            }
            catch (Exception ex)
            {
                return ApplicationActionResult.ErrorResult($"Fehler beim Wiederöffnen: {ex.Message}", "REOPEN_ERROR");
            }
        }

        private async Task<ApplicationActionResult> ReopenFromAcceptedAsync(ApplicationForm app, string? note, string actorId)
        {
            try
            {
                await using var tx = await _db.Database.BeginTransactionAsync();

                var previousLeagueId = app.AssignedLeagueId;
                app.Status = ApplicationStatus.Open;
                app.AcceptedAt = null;
                app.AssignedRole = null;
                app.AssignedLeagueId = null;
                app.IsOnTrial = false;
                app.TrialEndsAt = null;
                AppendNote(app, note);

                // Leeren Auto-Standing entfernen, den die Annahme angelegt hat. Für
                // Alt-Daten ohne AssignedLeagueId fallen wir auf beworbene Liga und
                // Division-Namen zurück.
                var candidateLeagueIds = await ResolveStandingCandidateLeagueIdsAsync(app, previousLeagueId);
                await RemoveEmptyStandingsAsync(app, candidateLeagueIds, excludeLeagueId: null);

                await _audit.LogAsync("ReopenApplication", "ApplicationForm", app.Id.ToString(),
                    $"Von=Angenommen, Liga={previousLeagueId ?? "(keine)"}, Note={note}, Actor={actorId}");
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return ApplicationActionResult.SuccessResult("Annahme rückgängig gemacht — Bewerbung ist wieder offen", app);
            }
            catch (DbUpdateConcurrencyException dex)
            {
                await _audit.LogAsync("ReopenApplicationConcurrency", "ApplicationForm", app.Id.ToString(), dex.Message);
                return ApplicationActionResult.ErrorResult("Konflikt beim Wiederöffnen. Bitte Seite neu laden und erneut versuchen.", "CONCURRENCY_CONFLICT");
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("ReopenApplicationError", "ApplicationForm", app.Id.ToString(), ex.Message);
                return ApplicationActionResult.ErrorResult($"Fehler beim Wiederöffnen: {ex.Message}", "REOPEN_ERROR");
            }
        }

        // ── Liga-Umzug (nur für Angenommene) ─────────────────────────────────────

        public async Task<ApplicationActionResult> MoveToLeagueAsync(int id, string leagueId, string? assignedRole, string actorId)
        {
            var app = await _db.ApplicationForms.FindAsync(id);
            if (app is null)
                return ApplicationActionResult.ErrorResult("Bewerbung nicht gefunden", "NOT_FOUND");

            if (app.Status != ApplicationStatus.Accepted)
                return ApplicationActionResult.ErrorResult("Nur angenommene Bewerbungen können umgezogen werden — zuerst annehmen", "NOT_ACCEPTED");

            var league = await _db.Leagues.FirstOrDefaultAsync(l => l.Id == leagueId && !l.IsArchived);
            if (league is null)
                return ApplicationActionResult.ErrorResult("Liga nicht gefunden oder archiviert", "LEAGUE_NOT_FOUND");

            try
            {
                var driverName = await ResolveDriverNameAsync(app);
                var effectiveRole = assignedRole ?? app.AssignedRole ?? app.Role;
                var previousLeagueId = app.AssignedLeagueId;

                // Neuer Standing-Eintrag, Aufräumen alter Einträge und App-Update atomar —
                // sonst kann ein Teilzustand (Standing gesetzt, Liga nicht gespeichert) entstehen.
                await using var tx = await _db.Database.BeginTransactionAsync();

                await EnsureStandingAsync(league, driverName, IsReserveRole(effectiveRole), id);

                var candidateLeagueIds = await ResolveStandingCandidateLeagueIdsAsync(app, previousLeagueId);
                await RemoveEmptyStandingsAsync(app, candidateLeagueIds, excludeLeagueId: league.Id);

                if (!string.IsNullOrWhiteSpace(assignedRole))
                    app.AssignedRole = assignedRole;
                app.AssignedLeagueId = league.Id;
                app.Division = league.Name;

                await _audit.LogAsync("MoveApplicationToLeague", "ApplicationForm", id.ToString(),
                    $"League={league.Id} ({league.Name}), Von={previousLeagueId ?? "(keine)"}, Driver={driverName}, Actor={actorId}");
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return ApplicationActionResult.SuccessResult($"Fahrer in Liga \"{league.Name}\" umgezogen", app);
            }
            catch (DbUpdateConcurrencyException dex)
            {
                await _audit.LogAsync("MoveLeagueConcurrency", "ApplicationForm", id.ToString(), dex.Message);
                return ApplicationActionResult.ErrorResult("Konflikt beim Liga-Umzug. Bitte Seite neu laden und erneut versuchen.", "CONCURRENCY_CONFLICT");
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("MoveLeagueError", "ApplicationForm", id.ToString(), ex.Message);
                return ApplicationActionResult.ErrorResult($"Fehler beim Liga-Umzug: {ex.Message}", "MOVE_LEAGUE_ERROR");
            }
        }

        // ── Delete ───────────────────────────────────────────────────────────────

        public async Task<ApplicationActionResult> DeleteAsync(int id, string actorId)
        {
            var app = await _db.ApplicationForms.FindAsync(id);
            if (app is null)
                return ApplicationActionResult.ErrorResult("Bewerbung nicht gefunden", "NOT_FOUND");

            try
            {
                await using var tx = await _db.Database.BeginTransactionAsync();

                // Bei angenommenen Bewerbungen den leeren Auto-Standing mit abräumen,
                // damit kein verwaister 0-Punkte-Eintrag in der Liga zurückbleibt.
                if (app.Status == ApplicationStatus.Accepted)
                {
                    var candidateLeagueIds = await ResolveStandingCandidateLeagueIdsAsync(app, app.AssignedLeagueId);
                    await RemoveEmptyStandingsAsync(app, candidateLeagueIds, excludeLeagueId: null);
                }

                _db.ApplicationForms.Remove(app);
                await _audit.LogAsync("DeleteApplication", "ApplicationForm", id.ToString(),
                    $"Status={app.Status}, User={app.DiscordName}, Actor={actorId}");
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return ApplicationActionResult.SuccessResult("Bewerbung gelöscht", app);
            }
            catch (DbUpdateConcurrencyException dex)
            {
                await _audit.LogAsync("DeleteApplicationConcurrency", "ApplicationForm", id.ToString(), dex.Message);
                return ApplicationActionResult.ErrorResult("Konflikt beim Löschen. Bitte Seite neu laden und erneut versuchen.", "CONCURRENCY_CONFLICT");
            }
            catch (Exception ex)
            {
                return ApplicationActionResult.ErrorResult($"Fehler beim Löschen: {ex.Message}", "DELETE_ERROR");
            }
        }

        // ── Flag / Trial ─────────────────────────────────────────────────────────

        public async Task<ApplicationActionResult> FlagAsync(int id, string reason, string actorId)
        {
            var app = await _db.ApplicationForms.FindAsync(id);
            if (app is null)
                return ApplicationActionResult.ErrorResult("Bewerbung nicht gefunden", "NOT_FOUND");

            if (app.Status != ApplicationStatus.Open)
                return ApplicationActionResult.ErrorResult("Nur offene Bewerbungen können markiert werden", "INVALID_STATE");

            try
            {
                app.IsFlagged = true;
                app.FlaggedAt = DateTime.UtcNow;
                AppendNote(app, reason);

                await _audit.LogAsync("FlagApplicationForReview", "ApplicationForm", id.ToString(),
                    $"Reason={reason}, Actor={actorId}");
                await _db.SaveChangesAsync();

                return ApplicationActionResult.SuccessResult("Bewerbung zur Überprüfung markiert", app);
            }
            catch (DbUpdateConcurrencyException dex)
            {
                await _audit.LogAsync("FlagApplicationConcurrency", "ApplicationForm", id.ToString(), dex.Message);
                return ApplicationActionResult.ErrorResult("Konflikt beim Markieren. Bitte Seite neu laden und erneut versuchen.", "CONCURRENCY_CONFLICT");
            }
            catch (Exception ex)
            {
                return ApplicationActionResult.ErrorResult($"Fehler beim Markieren: {ex.Message}", "FLAG_ERROR");
            }
        }

        public async Task<ApplicationActionResult> UnflagAsync(int id, string actorId)
        {
            var app = await _db.ApplicationForms.FindAsync(id);
            if (app is null)
                return ApplicationActionResult.ErrorResult("Bewerbung nicht gefunden", "NOT_FOUND");

            if (!app.IsFlagged)
                return ApplicationActionResult.ErrorResult("Bewerbung ist nicht markiert", "NOT_FLAGGED");

            try
            {
                app.IsFlagged = false;
                app.FlaggedAt = null;

                await _audit.LogAsync("UnflagApplication", "ApplicationForm", id.ToString(), $"Actor={actorId}");
                await _db.SaveChangesAsync();

                return ApplicationActionResult.SuccessResult("Markierung aufgehoben", app);
            }
            catch (DbUpdateConcurrencyException dex)
            {
                await _audit.LogAsync("UnflagApplicationConcurrency", "ApplicationForm", id.ToString(), dex.Message);
                return ApplicationActionResult.ErrorResult("Konflikt beim Aufheben der Markierung. Bitte Seite neu laden und erneut versuchen.", "CONCURRENCY_CONFLICT");
            }
            catch (Exception ex)
            {
                return ApplicationActionResult.ErrorResult($"Fehler beim Aufheben der Markierung: {ex.Message}", "UNFLAG_ERROR");
            }
        }

        public async Task<ApplicationActionResult> SetTrialAsync(int id, bool onTrial, int? days, string actorId)
        {
            var app = await _db.ApplicationForms.FindAsync(id);
            if (app is null)
                return ApplicationActionResult.ErrorResult("Bewerbung nicht gefunden", "NOT_FOUND");

            try
            {
                app.IsOnTrial = onTrial;
                app.TrialEndsAt = onTrial
                    ? DateTime.UtcNow.AddDays(days.HasValue && days.Value > 0 ? Math.Min(days.Value, MaxTrialDays) : DefaultTrialDays)
                    : null;

                await _audit.LogAsync(onTrial ? "TrialStarted" : "TrialEnded", "ApplicationForm", id.ToString(),
                    onTrial ? $"Bis {app.TrialEndsAt:yyyy-MM-dd}, Actor={actorId}" : $"Probezeit aufgehoben, Actor={actorId}");
                await _db.SaveChangesAsync();

                return ApplicationActionResult.SuccessResult(
                    onTrial ? "Fahrer auf Probe gesetzt" : "Probezeit beendet", app);
            }
            catch (DbUpdateConcurrencyException dex)
            {
                await _audit.LogAsync("SetTrialConcurrency", "ApplicationForm", id.ToString(), dex.Message);
                return ApplicationActionResult.ErrorResult("Konflikt beim Setzen der Probezeit. Bitte Seite neu laden und erneut versuchen.", "CONCURRENCY_CONFLICT");
            }
            catch (Exception ex)
            {
                return ApplicationActionResult.ErrorResult($"Fehler beim Setzen der Probezeit: {ex.Message}", "TRIAL_ERROR");
            }
        }

        // ── Housekeeping ─────────────────────────────────────────────────────────

        public async Task<int> CleanupExpiredAsync(string actorId)
        {
            // Nur ALTE, bereits ABGELEHNTE Bewerbungen entfernen. Angenommene bleiben
            // dauerhaft als Audit-/Historie-Beleg erhalten.
            var cutoff = DateTime.UtcNow.AddDays(-CleanupAfterDays);
            var expired = await _db.ApplicationForms
                .Where(x => x.Status == ApplicationStatus.Rejected && x.RejectedAt.HasValue && x.RejectedAt.Value <= cutoff)
                .ToListAsync();

            if (expired.Count == 0)
                return 0;

            _db.ApplicationForms.RemoveRange(expired);
            await _audit.LogAsync("RemoveExpiredApplications", "ApplicationForm", "*",
                $"Removed {expired.Count} alte abgelehnte Bewerbungen (>{CleanupAfterDays} Tage), Actor={actorId}");
            await _db.SaveChangesAsync();

            return expired.Count;
        }

        // ── Standings-Pflege ─────────────────────────────────────────────────────

        /// <summary>Trägt den Fahrer in die Liga ein, falls er dort noch nicht steht.</summary>
        private async Task EnsureStandingAsync(League league, string driverName, bool isReserve, int applicationId,
            int? preferredNumber = null, string? preferredTeam = null)
        {
            var normalized = driverName.Trim();
            if (normalized.Length == 0)
                return;

            var normalizedLower = normalized.ToLowerInvariant();
            var exists = await _db.DriverStandings
                .AnyAsync(s => s.LeagueId == league.Id
                    && s.Driver != null
                    && s.Driver.Trim().ToLower() == normalizedLower);
            if (exists)
                return;

            // Wunsch-Nummer nur übernehmen, wenn sie in dieser Liga noch frei ist.
            int? assignedNumber = null;
            if (preferredNumber.HasValue)
            {
                var numberTaken = await _db.DriverStandings
                    .AnyAsync(s => s.LeagueId == league.Id && s.DriverNumber == preferredNumber.Value);
                if (!numberTaken)
                    assignedNumber = preferredNumber.Value;
            }

            var standing = new DriverStanding
            {
                LeagueId = league.Id,
                Driver = normalized,
                Team = string.IsNullOrWhiteSpace(preferredTeam) ? string.Empty : preferredTeam.Trim(),
                DriverNumber = assignedNumber,
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

            // RowId ist erst nach dem Insert bekannt → Audit separat speichern
            // (innerhalb der umschließenden Transaktion, kein eigener Commit).
            await _audit.LogAndSaveAsync("AdminAction", "DriverStanding", standing.RowId.ToString(),
                $"Added standing for driver={standing.Driver} in league={league.Id} via application***REMOVED***{applicationId}");
        }

        /// <summary>
        /// Entfernt leere Auto-Einträge (0 Punkte/Siege/Position) des Fahrers aus den
        /// Kandidaten-Ligen. Einträge mit echten Ergebnissen bleiben unangetastet.
        /// </summary>
        private async Task RemoveEmptyStandingsAsync(ApplicationForm app, List<string> candidateLeagueIds, string? excludeLeagueId)
        {
            var leagueIds = candidateLeagueIds
                .Where(x => excludeLeagueId is null || !string.Equals(x, excludeLeagueId, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (leagueIds.Count == 0)
                return;

            var normalizedLower = (await ResolveDriverNameAsync(app)).Trim().ToLowerInvariant();
            if (normalizedLower.Length == 0)
                return;

            var staleStandings = await _db.DriverStandings
                .Where(s => leagueIds.Contains(s.LeagueId)
                    && s.Driver != null
                    && s.Driver.Trim().ToLower() == normalizedLower
                    && s.Points == 0 && s.Wins == 0 && s.Position == 0)
                .ToListAsync();
            if (staleStandings.Count == 0)
                return;

            // Audit-Einträge vor dem Save (RowIds der geladenen Entities sind bekannt),
            // damit Löschung und Audit atomar in einem SaveChanges landen.
            foreach (var stale in staleStandings)
            {
                await _audit.LogAsync("AdminAction", "DriverStanding", stale.RowId.ToString(),
                    $"Removed empty standing for driver={stale.Driver} in league={stale.LeagueId} (application***REMOVED***{app.Id})");
            }

            _db.DriverStandings.RemoveRange(staleStandings);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Liefert die Liga-Ids, in denen ein Auto-Standing des Bewerbers liegen könnte:
        /// die zugewiesene Liga, die beworbene Liga sowie (für Alt-Daten ohne
        /// AssignedLeagueId) die über den Division-Namen aufgelöste Liga.
        /// </summary>
        private async Task<List<string>> ResolveStandingCandidateLeagueIdsAsync(ApplicationForm app, string? assignedLeagueId)
        {
            var ids = new List<string>();
            if (!string.IsNullOrWhiteSpace(assignedLeagueId))
                ids.Add(assignedLeagueId);
            if (!string.IsNullOrWhiteSpace(app.AppliedLeagueId))
                ids.Add(app.AppliedLeagueId);

            if (!string.IsNullOrWhiteSpace(app.Division))
            {
                var byDivision = await _db.Leagues
                    .Where(l => l.Name == app.Division)
                    .Select(l => l.Id)
                    .FirstOrDefaultAsync();
                if (byDivision is not null)
                    ids.Add(byDivision);
            }

            return ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>Warnung, wenn die Stammfahrer-Kapazität der Liga bereits erreicht ist.</summary>
        private async Task<string?> BuildCapacityWarningAsync(League league)
        {
            if (!league.Capacity.HasValue)
                return null;

            var filled = await _db.DriverStandings
                .CountAsync(s => s.LeagueId == league.Id && !s.IsReserveDriver && s.Driver != "");
            return filled >= league.Capacity.Value
                ? $"Achtung: Liga \"{league.Name}\" ist bereits voll ({filled}/{league.Capacity.Value} Stammplätze)"
                : null;
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
            => string.Equals(role, ErsatzfahrerRole, StringComparison.OrdinalIgnoreCase);

        /// <summary>Hängt eine Notiz an die Review-Notiz an (respektiert das 1000-Zeichen-Limit).</summary>
        private static void AppendNote(ApplicationForm app, string? note)
        {
            if (string.IsNullOrWhiteSpace(note))
                return;

            var combined = string.IsNullOrWhiteSpace(app.ReviewNote) ? note.Trim() : app.ReviewNote + "\n" + note.Trim();
            app.ReviewNote = combined.Length <= ReviewNoteMaxLength ? combined : combined[..ReviewNoteMaxLength];
        }
    }
}
