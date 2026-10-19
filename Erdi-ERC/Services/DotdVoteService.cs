using System.Text.Json;
using Erdi_ERC.Data;
using Erdi_ERC.Helpers;
using Erdi_ERC.Models;
using Microsoft.EntityFrameworkCore;

namespace Erdi_ERC.Services;

/// <summary>
/// Fachlogik der Driver-of-the-Day-Wahl. Der Service ist scoped; der gemeinsame
/// Zustand liegt in <see cref="DotdRuntimeState"/>. Alle schreibenden Operationen
/// laufen durch dessen <c>Gate</c>, damit eine Stimme nicht mit dem Schließen kollidiert.
/// </summary>
public sealed class DotdVoteService : IDotdVoteService
{
    private readonly AppDbContext _db;
    private readonly DotdRuntimeState _state;
    private readonly ILogger<DotdVoteService> _logger;

    public DotdVoteService(AppDbContext db, DotdRuntimeState state, ILogger<DotdVoteService> logger)
    {
        _db = db;
        _state = state;
        _logger = logger;
    }

    public IReadOnlyList<DotdRejectedAttempt> RecentRejections => _state.RecentRejections;

    public async Task<bool> HasOpenVoteAsync(CancellationToken ct = default)
    {
        await _state.Gate.WaitAsync(ct);
        try
        {
            await EnsureLoadedAsync(ct);
            return _state.ActiveVoteId is not null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DOTD: offene Wahl konnte nicht geprüft werden.");
            return false;
        }
        finally
        {
            _state.Gate.Release();
        }
    }

    public async Task<DotdActionResult> OpenAsync(
        string leagueId, string? startedByDiscordId, CancellationToken ct = default)
    {
        await _state.Gate.WaitAsync(ct);
        try
        {
            await EnsureLoadedAsync(ct);

            if (_state.ActiveVoteId is not null)
            {
                return new DotdActionResult(
                    false,
                    $"Es läuft bereits eine Wahl für {_state.ActiveLeagueName ?? "eine Liga"}.",
                    null,
                    null);
            }

            var league = await _db.Leagues.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == leagueId, ct);
            if (league is null || league.IsArchived)
            {
                return new DotdActionResult(
                    false, "Liga nicht gefunden oder archiviert.", null, null);
            }

            var candidates = await LoadCandidatesAsync(league.Id, ct);
            if (candidates.Count == 0)
            {
                return new DotdActionResult(
                    false, "Diese Liga hat keine Fahrer in den Standings.", null, null);
            }

            var vote = new DotdVote
            {
                LeagueId = league.Id,
                LeagueName = league.Name,
                StartedAt = DateTime.UtcNow,
                StartedByDiscordId = startedByDiscordId
            };
            _db.DotdVotes.Add(vote);
            await _db.SaveChangesAsync(ct);

            _state.ActiveVoteId = vote.Id;
            _state.ActiveLeagueName = league.Name;
            _state.Candidates = candidates.ToArray();
            _state.LastSnapshot = new DotdSnapshot(
                "open", false, league.Name, Array.Empty<DotdBar>(), null);

            return new DotdActionResult(
                true, null, vote.Id, $"League={league.Name}");
        }
        finally
        {
            _state.Gate.Release();
        }
    }

    public async Task<bool> RegisterVoteAsync(
        string voterKey, string chatText, CancellationToken ct = default)
    {
        await _state.Gate.WaitAsync(ct);
        try
        {
            await EnsureLoadedAsync(ct);
            if (_state.ActiveVoteId is not int voteId)
            {
                return false;
            }

            var match = DriverNameMatcher.Match(chatText, _state.Candidates);
            if (match.Driver is null)
            {
                _state.AddRejection(chatText);
                return false;
            }

            var ballot = await _db.DotdBallots.AsTracking()
                .FirstOrDefaultAsync(
                    b => b.VoteId == voteId && b.VoterKey == voterKey, ct);

            if (ballot is null)
            {
                _db.DotdBallots.Add(new DotdBallot
                {
                    VoteId = voteId,
                    VoterKey = voterKey,
                    Driver = match.Driver,
                    VotedAt = DateTime.UtcNow
                });
            }
            else
            {
                ballot.Driver = match.Driver;
                ballot.VotedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
            return true;
        }
        finally
        {
            _state.Gate.Release();
        }
    }

    public async Task<DotdActionResult> CloseAsync(CancellationToken ct = default)
    {
        await _state.Gate.WaitAsync(ct);
        try
        {
            await EnsureLoadedAsync(ct);
            if (_state.ActiveVoteId is not int voteId)
            {
                return new DotdActionResult(false, "Keine offene Wahl.", null, null);
            }

            var vote = await _db.DotdVotes.AsTracking()
                .FirstOrDefaultAsync(v => v.Id == voteId, ct);
            if (vote is null)
            {
                _state.ClearActive();
                return new DotdActionResult(false, "Wahl nicht gefunden.", voteId, null);
            }

            var counts = await LoadCountsAsync(voteId, ct);
            var total = counts.Values.Sum();
            var finalBars = DotdShareBuilder.BuildFinalBars(counts);
            var ordered = OrderByVotes(counts).ToArray();
            var winnerDriver = ordered.Length > 0 ? ordered[0].Key : null;
            var winnerVotes = ordered.Length > 0 ? ordered[0].Value : 0;

            vote.ClosedAt = DateTime.UtcNow;
            vote.TotalVotes = total;
            vote.WinnerDriver = winnerDriver;
            vote.WinnerVotes = winnerDriver is null ? null : winnerVotes;
            vote.FinalSharesJson = JsonSerializer.Serialize(finalBars);

            await _db.DotdBallots
                .Where(b => b.VoteId == voteId)
                .ExecuteDeleteAsync(ct);
            await _db.SaveChangesAsync(ct);

            var percent = total > 0
                ? (int)Math.Round(winnerVotes * 100.0 / total, MidpointRounding.AwayFromZero)
                : 0;
            var snapshot = new DotdSnapshot(
                "closed",
                false,
                vote.LeagueName,
                finalBars,
                winnerDriver is null
                    ? null
                    : new DotdWinner(DriverNameHelper.Display(winnerDriver), percent));

            _state.ClearActive();
            _state.LastSnapshot = snapshot;

            return new DotdActionResult(
                true,
                null,
                vote.Id,
                winnerDriver is null
                    ? "Keine gültigen Stimmen"
                    : $"Winner={winnerDriver}, Votes={winnerVotes}/{total}");
        }
        finally
        {
            _state.Gate.Release();
        }
    }

    public async Task<DotdActionResult> DiscardAsync(CancellationToken ct = default)
    {
        await _state.Gate.WaitAsync(ct);
        try
        {
            await EnsureLoadedAsync(ct);
            if (_state.ActiveVoteId is not int voteId)
            {
                return new DotdActionResult(false, "Keine offene Wahl.", null, null);
            }

            await _db.DotdBallots
                .Where(b => b.VoteId == voteId)
                .ExecuteDeleteAsync(ct);

            var vote = await _db.DotdVotes.AsTracking()
                .FirstOrDefaultAsync(v => v.Id == voteId, ct);
            if (vote is not null)
            {
                _db.DotdVotes.Remove(vote);
                await _db.SaveChangesAsync(ct);
            }

            _state.ClearActive();
            _state.LastSnapshot = null;

            return new DotdActionResult(true, null, voteId, "Verworfen");
        }
        finally
        {
            _state.Gate.Release();
        }
    }

    public async Task<DotdSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        await _state.Gate.WaitAsync(ct);
        try
        {
            try
            {
                await EnsureLoadedAsync(ct);

                if (_state.ActiveVoteId is int voteId)
                {
                    var counts = await LoadCountsAsync(voteId, ct);
                    var snapshot = new DotdSnapshot(
                        "open",
                        false,
                        _state.ActiveLeagueName ?? string.Empty,
                        DotdShareBuilder.BuildOpenBars(counts),
                        null);
                    _state.LastSnapshot = snapshot;
                    return snapshot;
                }

                var latest = await _db.DotdVotes.AsNoTracking()
                    .Where(v => v.ClosedAt != null)
                    .OrderByDescending(v => v.ClosedAt)
                    .ThenByDescending(v => v.Id)
                    .FirstOrDefaultAsync(ct);

                if (latest is null)
                {
                    var none = new DotdSnapshot(
                        "none", false, string.Empty, Array.Empty<DotdBar>(), null);
                    _state.LastSnapshot = none;
                    return none;
                }

                var finalBars = DeserializeFinalBars(latest.FinalSharesJson);
                var percent = latest is { WinnerVotes: int winnerVotes, TotalVotes: int total }
                              && total > 0
                    ? (int)Math.Round(
                        winnerVotes * 100.0 / total, MidpointRounding.AwayFromZero)
                    : 0;

                var closed = new DotdSnapshot(
                    "closed",
                    false,
                    latest.LeagueName,
                    finalBars,
                    latest.WinnerDriver is null
                        ? null
                        : new DotdWinner(
                            DriverNameHelper.Display(latest.WinnerDriver), percent));
                _state.LastSnapshot = closed;
                return closed;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DOTD: Snapshot konnte nicht aus der DB gelesen werden.");
                return _state.LastSnapshot is { } cached
                    ? cached with { Stale = true }
                    : new DotdSnapshot(
                        "none", true, string.Empty, Array.Empty<DotdBar>(), null);
            }
        }
        finally
        {
            _state.Gate.Release();
        }
    }

    public async Task<DotdActiveVote?> GetActiveAsync(CancellationToken ct = default)
    {
        await _state.Gate.WaitAsync(ct);
        try
        {
            await EnsureLoadedAsync(ct);
            if (_state.ActiveVoteId is not int voteId)
            {
                return null;
            }

            var vote = await _db.DotdVotes.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == voteId, ct);
            if (vote is null)
            {
                _state.ClearActive();
                return null;
            }

            var counts = await LoadCountsAsync(voteId, ct);
            return new DotdActiveVote
            {
                Id = vote.Id,
                LeagueName = vote.LeagueName,
                StartedAt = vote.StartedAt,
                Counts = OrderByVotes(counts)
                    .Select(entry => new DotdCountRow(entry.Key, entry.Value))
                    .ToArray(),
                TotalVotes = counts.Values.Sum()
            };
        }
        finally
        {
            _state.Gate.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (_state.Loaded)
        {
            return;
        }

        var open = await _db.DotdVotes.AsNoTracking()
            .Where(v => v.ClosedAt == null)
            .OrderByDescending(v => v.StartedAt)
            .ThenByDescending(v => v.Id)
            .FirstOrDefaultAsync(ct);

        if (open is not null)
        {
            _state.ActiveVoteId = open.Id;
            _state.ActiveLeagueName = open.LeagueName;
            _state.Candidates = await LoadCandidatesAsync(open.LeagueId, ct);
        }

        _state.Loaded = true;
    }

    private Task<List<string>> LoadCandidatesAsync(string leagueId, CancellationToken ct)
        => _db.DriverStandings.AsNoTracking()
            .Where(s => s.LeagueId == leagueId)
            .Select(s => s.Driver)
            .Distinct()
            .ToListAsync(ct);

    private async Task<Dictionary<string, int>> LoadCountsAsync(
        int voteId, CancellationToken ct)
    {
        var rows = await _db.DotdBallots.AsNoTracking()
            .Where(b => b.VoteId == voteId)
            .GroupBy(b => b.Driver)
            .Select(group => new { Driver = group.Key, Count = group.Count() })
            .ToListAsync(ct);

        return rows.ToDictionary(row => row.Driver, row => row.Count);
    }

    private static IEnumerable<KeyValuePair<string, int>> OrderByVotes(
        IReadOnlyDictionary<string, int> counts)
        => counts
            .OrderByDescending(entry => entry.Value)
            .ThenBy(
                entry => DriverNameHelper.Display(entry.Key),
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<DotdBar> DeserializeFinalBars(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<DotdBar>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<DotdBar>>(json)
                   ?? (IReadOnlyList<DotdBar>)Array.Empty<DotdBar>();
        }
        catch (JsonException)
        {
            return Array.Empty<DotdBar>();
        }
    }
}
