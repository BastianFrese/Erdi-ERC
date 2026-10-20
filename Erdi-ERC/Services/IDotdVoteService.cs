using Erdi_ERC.Models;

namespace Erdi_ERC.Services;

/// <summary>Ergebnis einer Verwaltungsaktion; <see cref="Details"/> landet im Audit-Log.</summary>
public sealed record DotdActionResult(bool Ok, string? Error, int? VoteId, string? Details);

public interface IDotdVoteService
{
    Task<bool> HasOpenVoteAsync(CancellationToken ct = default);

    Task<DotdActionResult> OpenAsync(
        string leagueId, string? startedByDiscordId, CancellationToken ct = default);

    Task<DotdActionResult> CloseAsync(CancellationToken ct = default);

    Task<DotdActionResult> DiscardAsync(CancellationToken ct = default);

    /// <summary>Verarbeitet eine Chat-Nachricht. Gibt den vollen Fahrernamen zurück,
    /// wenn die Stimme gezählt wurde, sonst null (keine Wahl, unbekannt oder mehrdeutig).</summary>
    Task<string?> RegisterVoteAsync(string voterKey, string chatText, CancellationToken ct = default);

    Task<DotdSnapshot> GetSnapshotAsync(CancellationToken ct = default);

    /// <summary>Exakte Zahlen für den Admin (nie für das Overlay).</summary>
    Task<DotdActiveVote?> GetActiveAsync(CancellationToken ct = default);

    IReadOnlyList<DotdRejectedAttempt> RecentRejections { get; }
}
