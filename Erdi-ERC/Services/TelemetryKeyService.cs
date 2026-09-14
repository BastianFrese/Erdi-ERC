using System.Security.Cryptography;
using System.Text;
using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Microsoft.EntityFrameworkCore;

namespace Erdi_ERC.Services;

/// <summary>Ergebnis der Key-Generierung. <see cref="PlainKey"/> ist der EINZIGE Klartext,
/// wird danach nie wieder angezeigt und nie gespeichert.</summary>
public sealed record TelemetryKeyGenerateResult(
    bool Ok,
    string? Error,
    string? PlainKey,
    TelemetrySenderKey? SenderKey);

public interface ITelemetryKeyService
{
    /// <summary>
    /// Löst einen präsentierten API-Key zu seinem (aktiven) <see cref="TelemetrySenderKey"/>
    /// auf — Hash-Match gegen die DB, gesperrte Keys werden ignoriert. Null = ungültig.
    /// </summary>
    Task<TelemetrySenderKey?> ValidateAsync(string? apiKey, CancellationToken ct = default);

    /// <summary>
    /// Erzeugt einen neuen Sende-Key für ein Fahrer-Profil. Der Klartext wird EINMALIG
    /// zurückgegeben (nur im Speicher); DB speichert nur den SHA-256-Hash.
    /// Audit: <c>TelemetrySenderKeyGenerated</c>.
    /// </summary>
    Task<TelemetryKeyGenerateResult> GenerateAsync(string discordId, string? description, CancellationToken ct = default);

    /// <summary>Gesperrte Keys können nicht mehr senden. Audit: <c>TelemetrySenderKeyRevoked</c>. Idempotent.</summary>
    Task<bool> RevokeAsync(int id, string actorDiscordId, CancellationToken ct = default);

    /// <summary>Alle Keys (auch gesperrt), für die Admin-Übersicht.</summary>
    Task<IReadOnlyList<TelemetrySenderKey>> ListAsync(CancellationToken ct = default);

    Task<TelemetrySenderKey?> GetByIdAsync(int id, CancellationToken ct = default);
}

public class TelemetryKeyService : ITelemetryKeyService
{
    private readonly AppDbContext _db;
    private readonly IAdminAuditService _audit;

    public TelemetryKeyService(AppDbContext db, IAdminAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <summary>SHA-256 als lowercase-Hex — auch für den Payload-Dedup-Hash wiederverwendet.</summary>
    public static string ComputeHash(string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

    public async Task<TelemetrySenderKey?> ValidateAsync(string? apiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var hash = ComputeHash(apiKey);
        return await _db.TelemetrySenderKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.KeyHash == hash && k.RevokedAt == null, ct);
    }

    public async Task<TelemetryKeyGenerateResult> GenerateAsync(string discordId, string? description, CancellationToken ct = default)
    {
        var profile = await _db.DriverProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.DiscordId == discordId, ct);

        if (profile is null)
        {
            return new TelemetryKeyGenerateResult(false, "Fahrer-Profil nicht gefunden.", null, null);
        }

        var plainKey = "erct_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(20)).ToLowerInvariant();
        var senderKey = new TelemetrySenderKey
        {
            DiscordId = discordId,
            KeyHash = ComputeHash(plainKey),
            Description = string.IsNullOrWhiteSpace(description) ? null : description!.Trim(),
        };

        _db.TelemetrySenderKeys.Add(senderKey);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("TelemetrySenderKeyGenerated", "TelemetrySenderKey", senderKey.Id.ToString(),
            $"DiscordId={discordId}, Description={senderKey.Description ?? "-"}");
        await _db.SaveChangesAsync(ct);

        return new TelemetryKeyGenerateResult(true, null, plainKey, senderKey);
    }

    public async Task<bool> RevokeAsync(int id, string actorDiscordId, CancellationToken ct = default)
    {
        var senderKey = await _db.TelemetrySenderKeys
            .AsTracking()
            .FirstOrDefaultAsync(k => k.Id == id, ct);
        if (senderKey is null || senderKey.RevokedAt != null)
        {
            // Idempotent: bereits gesperrt/unbekannt → kein zweiter Audit-Eintrag, aber "Erfolg" für den Aufrufer.
            return senderKey is not null;
        }

        senderKey.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("TelemetrySenderKeyRevoked", "TelemetrySenderKey", id.ToString(),
            $"DiscordId={senderKey.DiscordId}, Actor={actorDiscordId}");
        await _db.SaveChangesAsync(ct);

        return true;
    }

    public async Task<IReadOnlyList<TelemetrySenderKey>> ListAsync(CancellationToken ct = default)
    {
        return await _db.TelemetrySenderKeys
            .AsNoTracking()
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);
    }

    public Task<TelemetrySenderKey?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return _db.TelemetrySenderKeys.AsNoTracking().FirstOrDefaultAsync(k => k.Id == id, ct);
    }
}
