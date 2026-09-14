using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Microsoft.EntityFrameworkCore;

namespace Erdi_ERC.Services;

/// <summary>Liga-Eintrag für die App-Auswahl im „Ergebnis senden"-Dialog (GET /api/telemetry/leagues).</summary>
public sealed record TelemetryLeagueInfo(string LeagueId, string Name);

/// <summary>
/// Antwort des Ingest-Endpunkts: bei Erfolg <see cref="PendingId"/> + StatusCode
/// 201 (neu) bzw. 200 (idempotentes Re-POST), sonst <see cref="StatusCode"/> 400/401/403
/// mit deutscher <see cref="Error"/>-Meldung für den API-Client.
/// </summary>
public sealed record TelemetryIngestResult(int? PendingId, int StatusCode, string? Error)
{
    public bool IsSuccess => PendingId is not null;
}

public interface ITelemetryIngestService
{
    /// <summary>
    /// Nimmt einen Telemetrie-Rennergebnis-Payload entgegen und legt ihn als
    /// Pending-Entwurf in der Review-Inbox ab. Reihenfolge:
    /// Key validieren (401) → Parser (400) → Liga-Gate (403, nur wenn Payload eine
    /// Liga nennt) → SHA-256-Dedup (idempotent) → Persistieren + Audit + Webhook.
    /// </summary>
    Task<TelemetryIngestResult> IngestAsync(string? json, string? apiKey, CancellationToken ct = default);

    /// <summary>
    /// Alle Ligen, in denen der Key-Inhaber aktiver Stammfahrer ist (DriverStanding,
    /// !IsReserveDriver). Quelle für das Dropdown in der Telemetrie-App.
    /// </summary>
    Task<IReadOnlyList<TelemetryLeagueInfo>> MemberLeaguesAsync(string discordId, CancellationToken ct = default);
}

public class TelemetryIngestService : ITelemetryIngestService
{
    private readonly AppDbContext _db;
    private readonly ITelemetryKeyService _keyService;
    private readonly IAdminAuditService _audit;
    private readonly IWebhookAutomationService _webhookAuto;

    public TelemetryIngestService(
        AppDbContext db,
        ITelemetryKeyService keyService,
        IAdminAuditService audit,
        IWebhookAutomationService webhookAuto)
    {
        _db = db;
        _keyService = keyService;
        _audit = audit;
        _webhookAuto = webhookAuto;
    }

    public async Task<TelemetryIngestResult> IngestAsync(string? json, string? apiKey, CancellationToken ct = default)
    {
        // 1) Sender-Authentifizierung: persönlicher Key → aktives TelemetrySenderKey.
        var senderKey = await _keyService.ValidateAsync(apiKey, ct);
        if (senderKey is null)
        {
            return new TelemetryIngestResult(null, StatusCodes.Status401Unauthorized,
                "Ungültiger oder fehlender API-Key.");
        }

        // 2) Schema-Validierung (statisch, DB-frei).
        var parseResult = TelemetryRaceParser.Parse(json);
        if (parseResult.Result is null || parseResult.Error is not null)
        {
            return new TelemetryIngestResult(null, StatusCodes.Status400BadRequest, parseResult.Error);
        }

        var result = parseResult.Result;

        // 3) Liga-Gate: nennt der Payload eine Liga, muss der Key-Inhaber dort aktiver
        //    Stammfahrer sein — es dürfen NUR Liga-Rennen von berechtigten Fahrern
        //    in die Review-Inbox kommen.
        if (!string.IsNullOrWhiteSpace(result.League))
        {
            var isMember = await IsActiveMemberAsync(senderKey!.DiscordId, result.League, ct);
            if (!isMember)
            {
                return new TelemetryIngestResult(null, StatusCodes.Status403Forbidden,
                    $"Du darfst keine Ergebnisse für die Liga '{result.League}' senden (kein aktiver Stammfahrer dort).");
            }
        }

        // 4) Dedup: SHA-256 über den (getrimmten) Roh-Payload — identisches Re-POST
        //    (Retry der App) liefert die bestehende Pending-Id statt eines Doppels.
        var payloadHash = TelemetryKeyService.ComputeHash(json!.Trim());
        var existing = await _db.PendingRaceResults
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PayloadHash == payloadHash, ct);
        if (existing is not null)
        {
            return new TelemetryIngestResult(existing.Id, StatusCodes.Status200OK, null);
        }

        // 5) Persistieren als Pending (Review-Inbox), inkl. Sender + Liga-Vorschlag.
        var pending = new PendingRaceResult
        {
            SourcePayload = json.Trim(),
            PayloadHash = payloadHash,
            SourceTrack = result.Track,
            SourceDate = result.Date,
            SourceSeason = result.Season,
            SourceLeague = result.League,
            SenderDiscordId = senderKey!.DiscordId,
            Status = (int)PendingRaceStatus.Pending,
            ReceivedAt = DateTime.UtcNow,
        };

        // Fastest-Lap-Flag: Der Payload nennt optional den Namen des schnellsten Fahrers
        // (root "fastestLap"); die passende Finish-Zeile wird damit markiert.
        var fastestLapDriver = result.FastestLap?.Trim();
        foreach (var finish in result.Finishes)
        {
            pending.Finishes.Add(new PendingRaceFinish
            {
                Position = finish.Position,
                Driver = finish.Driver,
                IsDnf = finish.IsDnf,
                RaceTimeMs = finish.RaceTimeMs,
                QualifyingPosition = finish.QualifyingPosition,
                FastestLap = !string.IsNullOrWhiteSpace(fastestLapDriver)
                    && string.Equals(finish.Driver.Trim(), fastestLapDriver, StringComparison.OrdinalIgnoreCase),
            });
        }

        _db.PendingRaceResults.Add(pending);
        await _db.SaveChangesAsync(ct);

        // Audit-Persistenz-Konvention: LogAsync speichert NICHT selbst → eigener Save.
        await _audit.LogAsync("TelemetryResultReceived", "PendingRaceResult", pending.Id.ToString(),
            $"Sender={senderKey!.DiscordId}, Track={result.Track ?? "-"}, Date={result.Date:o}, League={result.League ?? "-"}, Finishes={pending.Finishes.Count}");
        await _db.SaveChangesAsync(ct);

        // Webhook fire-and-forget (wie Bewerbungs-Flow).
        await _webhookAuto.FireAsync(WebhookEvents.TelemetryResultReceived, new Dictionary<string, string>
        {
            ["Track"] = result.Track ?? "-",
            ["Date"] = result.Date?.ToString("yyyy-MM-dd HH:mm") ?? "-",
            ["League"] = result.League ?? "-",
            ["DriverCount"] = pending.Finishes.Count.ToString(),
            ["PendingId"] = pending.Id.ToString(),
        });

        return new TelemetryIngestResult(pending.Id, StatusCodes.Status201Created, null);
    }

    public async Task<IReadOnlyList<TelemetryLeagueInfo>> MemberLeaguesAsync(string discordId, CancellationToken ct = default)
    {
        var profile = await _db.DriverProfiles
            .AsNoTracking()
            .Include(p => p.GamerTags)
            .FirstOrDefaultAsync(p => p.DiscordId == discordId, ct);

        if (profile is null)
        {
            return Array.Empty<TelemetryLeagueInfo>();
        }

        // Alle Alias-Namen des Profils (DiscordName, DisplayName, GamerTags) —
        // gleiche Muster wie GetUnlinkedDriversAsync.
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddAlias(aliases, profile.DiscordName);
        AddAlias(aliases, profile.DisplayName);
        foreach (var tag in profile.GamerTags)
        {
            AddAlias(aliases, tag.GamerTag);
        }

        if (aliases.Count == 0)
        {
            return Array.Empty<TelemetryLeagueInfo>();
        }

        // Aktive Mitgliedschaft (kein Reserve-Fahrer) über Name-Match gegen die Standings.
        // Bestandsdaten können Leerraum im Namen haben → Trimm-Vergleich in-memory.
        var allActive = await _db.DriverStandings
            .AsNoTracking()
            .Where(s => !s.IsReserveDriver && s.Driver != null && s.Driver != "")
            .Select(s => new { s.LeagueId, s.Driver })
            .Distinct()
            .ToListAsync(ct);

        var leagueIds = allActive
            .Where(s => aliases.Contains(s.Driver!.Trim()))
            .Select(s => s.LeagueId)
            .Distinct()
            .ToList();

        if (leagueIds.Count == 0)
        {
            return Array.Empty<TelemetryLeagueInfo>();
        }

        return await _db.Leagues
            .AsNoTracking()
            .Where(l => leagueIds.Contains(l.Id) && !l.IsArchived)
            .OrderBy(l => l.Name)
            .Select(l => new TelemetryLeagueInfo(l.Id, l.Name))
            .ToListAsync(ct);
    }

    private async Task<bool> IsActiveMemberAsync(string discordId, string claimedLeague, CancellationToken ct)
    {
        var leagues = await MemberLeaguesAsync(discordId, ct);
        if (leagues.Count == 0)
        {
            return false;
        }

        // Der Payload nennt den Liga-NAMEN (aus der App-Auswahl) — Case-insensitive
        // gegen die realen Liga-Namen matchen.
        return leagues.Any(l => string.Equals(l.Name.Trim(), claimedLeague.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static void AddAlias(HashSet<string> aliases, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            aliases.Add(value.Trim());
        }
    }
}
