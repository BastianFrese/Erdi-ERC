using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Microsoft.EntityFrameworkCore;

namespace Erdi_ERC.Services;

/// <summary>Von der Review-UI übermittelte, evtl. korrigierte Finish-Zeile.</summary>
public sealed record PendingRaceFinishEdit(
    int Position,
    string Driver,
    bool IsDnf,
    long? RaceTimeMs,
    int? QualifyingPosition,
    bool FastestLap);

public sealed record PromotePendingRaceResult(bool Ok, string? Error, int? RaceResultId);

public interface IPendingRacePromotionService
{
    /// <summary>
    /// Übernimmt einen Pending-Entwurf als finales <see cref="RaceResult"/> (inkl. Finishes,
    /// Reserve- und Gast-Zuordnungen), rebuildet die Liga-Tabelle und markiert den Entwurf
    /// als Accept. Die Finishes kommen aus der Review-UI (admin-editiert); Reserve-/Gast-
    /// Zuordnungen werden aus dem gespeicherten SourcePayload reprozessiert (der Pending
    /// hält sie bewusst nicht als eigene Tabellen). Idempotent: ein bereits entschiedener
    /// Entwurf wird NICHT erneut promoted.
    /// </summary>
    Task<PromotePendingRaceResult> PromoteAsync(
        int pendingId,
        string leagueId,
        string track,
        DateTime date,
        string? season,
        IReadOnlyList<PendingRaceFinishEdit> editedFinishes,
        string? fastestLapDriver,
        string? actorDiscordId,
        CancellationToken ct = default);

    /// <summary>Lehnt einen Pending-Entwurf ab (Status=Rejected, optionale Note). Idempotent.</summary>
    Task<(bool Ok, string? Error)> RejectAsync(int pendingId, string? note, string? actorDiscordId, CancellationToken ct = default);
}

public class PendingRacePromotionService : IPendingRacePromotionService
{
    private readonly AppDbContext _db;
    private readonly IStatsService _stats;
    private readonly IStaticDataCache _staticCache;
    private readonly IDriverProfileService _driverProfiles;
    private readonly IAdminAuditService _audit;
    private readonly IWebhookAutomationService _webhookAuto;

    public PendingRacePromotionService(
        AppDbContext db,
        IStatsService stats,
        IStaticDataCache staticCache,
        IDriverProfileService driverProfiles,
        IAdminAuditService audit,
        IWebhookAutomationService webhookAuto)
    {
        _db = db;
        _stats = stats;
        _staticCache = staticCache;
        _driverProfiles = driverProfiles;
        _audit = audit;
        _webhookAuto = webhookAuto;
    }

    public async Task<PromotePendingRaceResult> PromoteAsync(
        int pendingId,
        string leagueId,
        string track,
        DateTime date,
        string? season,
        IReadOnlyList<PendingRaceFinishEdit> editedFinishes,
        string? fastestLapDriver,
        string? actorDiscordId,
        CancellationToken ct = default)
    {
        var pending = await _db.PendingRaceResults
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id == pendingId, ct);

        if (pending is null)
        {
            return new PromotePendingRaceResult(false, "Entwurf nicht gefunden.", null);
        }

        if (pending.Status != (int)PendingRaceStatus.Pending)
        {
            return new PromotePendingRaceResult(false, "Der Entwurf wurde bereits bearbeitet.", null);
        }

        var league = await _db.Leagues.FirstOrDefaultAsync(l => l.Id == leagueId, ct);
        if (league is null)
        {
            return new PromotePendingRaceResult(false, "Liga nicht gefunden.", null);
        }

        // Basis-Validierung der editierbaren Finishes (Parser-Garantien dürfen durch
        // Admin-Edition nicht verletzt werden).
        if (editedFinishes is null || editedFinishes.Count == 0)
        {
            return new PromotePendingRaceResult(false, "Mindestens ein Fahrer ist Pflicht.", null);
        }
        if (editedFinishes.All(f => f.IsDnf || f.Position == 0))
        {
            return new PromotePendingRaceResult(false, "Mindestens ein Fahrer muss das Ziel erreicht haben.", null);
        }
        if (editedFinishes.Where(f => f.Position > 0).Select(f => f.Position).Distinct().Count()
            != editedFinishes.Count(f => f.Position > 0))
        {
            return new PromotePendingRaceResult(false, "Doppelte Zielpositionen sind nicht erlaubt.", null);
        }

        // Reserve-/Gast-Zuordnungen stammen aus dem Roh-Payload (Reprozessierung, keine
        // Extra-Tabellen am Pending). Parse-Fehler hier ignorieren: der Ingest hat den
        // Payload bereits validiert, die Stammdaten (Finishes) kommen aus der UI.
        var parsed = TelemetryRaceParser.Parse(pending.SourcePayload).Result;

        var fastestLap = !string.IsNullOrWhiteSpace(fastestLapDriver)
            ? fastestLapDriver!.Trim()
            : parsed?.FastestLap?.Trim();

        var race = new RaceResult
        {
            LeagueId = leagueId,
            Date = date,
            Track = track.Trim(),
            Season = string.IsNullOrWhiteSpace(season) ? league.CurrentSeason : season!.Trim(),
            Winner = string.Empty,
            FastestLap = fastestLap ?? string.Empty,
        };

        // Transaktion: Rennen + Standings-Rebuild + Pending-Status atomar — eine
        // Unterbrechung darf keinen Halb-Zustand (Rennen final, Pending offen) hinterlassen.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            _db.RaceResults.Add(race);
            await _db.SaveChangesAsync(ct);

            foreach (var finish in editedFinishes)
            {
                var timeMs = finish.RaceTimeMs is > 0 and <= int.MaxValue
                    ? (int)finish.RaceTimeMs.Value
                    : (int?)null;

                _db.RaceFinishes.Add(new RaceFinish
                {
                    RaceResultId = race.RowId,
                    Driver = finish.Driver.Trim(),
                    Position = finish.IsDnf ? 0 : finish.Position,
                    FastestLap = finish.FastestLap,
                    RaceTimeMs = timeMs,
                    QualifyingPosition = finish.QualifyingPosition is > 0 ? finish.QualifyingPosition : null,
                });

                if (!finish.IsDnf && finish.Position == 1 && string.IsNullOrEmpty(race.Winner))
                {
                    race.Winner = finish.Driver.Trim();
                }
            }

            if (parsed is not null)
            {
                foreach (var reserve in parsed.ReserveAssignments)
                {
                    _db.RaceReserveAssignments.Add(new RaceReserveAssignment
                    {
                        RaceResultId = race.RowId,
                        ReserveDriver = reserve.Driver.Trim(),
                        MainDriver = reserve.MainDriver.Trim(),
                    });
                }

                foreach (var guest in parsed.GuestAssignments)
                {
                    // Gast-Namen kanonisch auflösen (Drift-Schutz), nicht-exakte Matches
                    // überspringen — exakt wie der EnterRace-Pfad.
                    var match = await _driverProfiles.ResolveAsync(guest.Driver, ct);
                    if (!match.ExactMatch) continue;

                    var mainName = string.IsNullOrWhiteSpace(guest.MainDriver)
                        ? StatsService.GuestSentinelNoMain
                        : guest.MainDriver.Trim();

                    _db.RaceGuestAssignments.Add(new RaceGuestAssignment
                    {
                        RaceResultId = race.RowId,
                        GuestDriver = match.ResolvedName,
                        MainDriver = mainName,
                    });
                }
            }

            await _db.SaveChangesAsync(ct);

            await _stats.RebuildLeagueStandingsAsync(leagueId, ct);
            _staticCache.InvalidateLeagues();

            pending.Status = (int)PendingRaceStatus.Accepted;
            pending.DecidedAt = DateTime.UtcNow;
            pending.DecidedByDiscordId = actorDiscordId;
            pending.PromotedRaceResultId = race.RowId;
            await _db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        // Post-commit (Audit-Persistenz-Konvention): LogAsync + eigener Save.
        await _audit.LogAsync("TelemetryResultPromoted", "RaceResult", race.RowId.ToString(),
            $"PendingId={pendingId}, League={leagueId}, Track={race.Track}, Date={date:yyyy-MM-dd}, Winner={race.Winner}, Finishes={editedFinishes.Count}, Actor={actorDiscordId ?? "-"}");
        await _db.SaveChangesAsync(ct);

        await _webhookAuto.FireAsync(WebhookEvents.RaceResultSaved, new Dictionary<string, string>
        {
            ["League"] = leagueId,
            ["Track"] = race.Track,
            ["Date"] = date.ToString("dd.MM.yyyy"),
            ["Winner"] = race.Winner,
            ["FastestLap"] = race.FastestLap ?? "",
        });

        return new PromotePendingRaceResult(true, null, race.RowId);
    }

    public async Task<(bool Ok, string? Error)> RejectAsync(int pendingId, string? note, string? actorDiscordId, CancellationToken ct = default)
    {
        var pending = await _db.PendingRaceResults.AsTracking().FirstOrDefaultAsync(p => p.Id == pendingId, ct);
        if (pending is null)
        {
            return (false, "Entwurf nicht gefunden.");
        }

        if (pending.Status != (int)PendingRaceStatus.Pending)
        {
            return (false, "Der Entwurf wurde bereits bearbeitet.");
        }

        pending.Status = (int)PendingRaceStatus.Rejected;
        pending.DecidedAt = DateTime.UtcNow;
        pending.DecidedByDiscordId = actorDiscordId;
        pending.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note!.Trim();
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("TelemetryResultRejected", "PendingRaceResult", pendingId.ToString(),
            $"Note={(string.IsNullOrWhiteSpace(pending.ReviewNote) ? "-" : pending.ReviewNote)}, Actor={actorDiscordId ?? "-"}");
        await _db.SaveChangesAsync(ct);

        return (true, null);
    }
}
