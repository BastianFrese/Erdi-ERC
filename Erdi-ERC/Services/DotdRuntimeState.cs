using System.Collections.Concurrent;
using Erdi_ERC.Models;

namespace Erdi_ERC.Services;

/// <summary>
/// Prozessweiter Zustand der Driver-of-the-Day-Wahl. Der Vote-Service ist scoped
/// (ein <c>AppDbContext</c> pro Request); dieser Singleton hält die offene Wahl,
/// den eingefrorenen Kader und den letzten Snapshot für den DB-Ausfall-Fallback.
/// </summary>
public sealed class DotdRuntimeState
{
    /// <summary>Serialisiert Stimmen, Öffnen, Schließen und Verwerfen.</summary>
    public SemaphoreSlim Gate { get; } = new(1, 1);

    /// <summary>Wurde der Zustand einmal aus der DB geladen?</summary>
    public bool Loaded { get; set; }

    public int? ActiveVoteId { get; set; }
    public string? ActiveLeagueName { get; set; }

    /// <summary>Beim Öffnen eingefrorene Kandidaten (volle Standings-Namen).</summary>
    public IReadOnlyList<string> Candidates { get; set; } = Array.Empty<string>();

    /// <summary>Letzter erfolgreich gebauter Snapshot; wird bei DB-Ausfall mit <c>Stale</c> ausgeliefert.</summary>
    public DotdSnapshot? LastSnapshot { get; set; }

    private readonly ConcurrentQueue<DotdRejectedAttempt> _rejections = new();

    public IReadOnlyList<DotdRejectedAttempt> RecentRejections
        => _rejections.Reverse().Take(20).ToArray();

    public void AddRejection(string text)
    {
        _rejections.Enqueue(new DotdRejectedAttempt(text, DateTime.UtcNow));
        while (_rejections.Count > 20 && _rejections.TryDequeue(out _))
        {
        }
    }

    public void ClearActive()
    {
        ActiveVoteId = null;
        ActiveLeagueName = null;
        Candidates = Array.Empty<string>();
    }
}
