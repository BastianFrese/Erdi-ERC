using System.ComponentModel.DataAnnotations;

namespace Erdi_ERC.Models;

/// <summary>
/// Eine Driver-of-the-Day-Abstimmung. Pro Liga kann genau eine Wahl offen sein.
/// Zeiten sind UTC (<c>DateTime.UtcNow</c>); Anzeige mit <c>ToLocalTime()</c>.
/// </summary>
public class DotdVote
{
    public int Id { get; set; }

    [MaxLength(64)]
    public string LeagueId { get; set; } = string.Empty;

    /// <summary>Snapshot des Liga-Namens, damit die Historie auch nach einer Umbenennung lesbar bleibt.</summary>
    [MaxLength(128)]
    public string LeagueName { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    /// <summary>Voller gespeicherter Fahrername. Null = geschlossen ohne gültige Stimme.</summary>
    [MaxLength(160)]
    public string? WinnerDriver { get; set; }

    public int? WinnerVotes { get; set; }

    public int? TotalVotes { get; set; }

    /// <summary>Endverteilung (max. 8 Balken) als JSON-Snapshot. Wird beim Schließen
    /// geschrieben, damit die Ballots danach gelöscht werden können.</summary>
    [MaxLength(2000)]
    public string? FinalSharesJson { get; set; }

    [MaxLength(32)]
    public string? StartedByDiscordId { get; set; }
}

/// <summary>
/// Eine Stimme. Eine Zeile pro (Wahl, Twitch-Nutzer); Umentscheiden überschreibt sie.
/// Die Ballots werden beim Schließen gelöscht.
/// </summary>
public class DotdBallot
{
    public int Id { get; set; }

    public int VoteId { get; set; }

    /// <summary>Twitch-User-Id (pseudonym); nur für die Ein-Stimme-Regel.</summary>
    [MaxLength(32)]
    public string VoterKey { get; set; } = string.Empty;

    /// <summary>Fahrername zum Zeitpunkt der Stimme (eingefrorener Kader).</summary>
    [MaxLength(160)]
    public string Driver { get; set; } = string.Empty;

    public DateTime VotedAt { get; set; }
}

/// <summary>Ein Balken im Overlay. Während der Wahl enthält <see cref="Share"/> einen
/// gerundeten, verrauschten Anteil — nie eine Stimmenzahl.</summary>
public sealed record DotdBar(string Name, int Share);

public sealed record DotdWinner(string Name, int Percent);

/// <summary>Zuletzt gezählte Stimme — für die kurze Einblendung im Overlay.</summary>
public sealed record DotdVoteEvent(string Driver, DateTime At);

/// <summary>Antwort des Overlay-Endpunkts. <c>State</c>: <c>open</c>, <c>closed</c>, <c>none</c>.</summary>
public sealed record DotdSnapshot(
    string State,
    bool Stale,
    string League,
    IReadOnlyList<DotdBar> Bars,
    DotdWinner? Winner,
    DotdVoteEvent? LastVote = null);

public sealed record DotdCountRow(string Driver, int Votes);

public sealed record DotdRejectedAttempt(string ChatText, DateTime At);

public sealed class DotdActiveVote
{
    public int Id { get; init; }
    public string LeagueName { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
    public IReadOnlyList<DotdCountRow> Counts { get; init; } = Array.Empty<DotdCountRow>();
    public int TotalVotes { get; init; }
}

public sealed class DotdAdminViewModel
{
    public const string OverlayUrl = "https://erdi-erc.de/overlay/dotd";

    public IReadOnlyList<League> Leagues { get; set; } = Array.Empty<League>();
    public DotdActiveVote? Active { get; set; }
    public IReadOnlyList<DotdVote> History { get; set; } = Array.Empty<DotdVote>();
    public IReadOnlyList<DotdRejectedAttempt> Rejections { get; set; } = Array.Empty<DotdRejectedAttempt>();
}
