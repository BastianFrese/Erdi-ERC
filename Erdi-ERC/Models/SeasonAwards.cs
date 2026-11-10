using System.ComponentModel.DataAnnotations;

namespace Erdi_ERC.Models;

/// <summary>
/// Eine Season-Award-Abstimmung (z. B. „Überraschung des Jahres"). Ein Award gilt für
/// alle Ligen: jede Liga bekommt automatisch ihr eigenes Voting. Nominieren ist
/// Abstimmen — jeder eingeloggte User hat pro Award und Liga genau eine Stimme und
/// darf sie bis zum Schließen umändern. Die Stimmenzahlen sind öffentlich erst nach
/// dem Schließen sichtbar (die Gewinner werden „bekannt gegeben").
/// </summary>
public class SeasonAward
{
    public int Id { get; set; }

    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(480)]
    public string? Description { get; set; }

    /// <summary>Saison-Label (z. B. „2026"), nur Anzeige/Gruppierung. Leer = keine Angabe.</summary>
    [MaxLength(16)]
    public string Season { get; set; } = string.Empty;

    public bool IsClosed { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ClosedAt { get; set; }

    [MaxLength(32)]
    public string? ClosedByDiscordId { get; set; }

    public List<SeasonAwardNomination> Nominations { get; set; } = new();
}

/// <summary>
/// Eine Stimme: ein User nominiert einen Fahrer aus dem Kader einer Liga für einen Award.
/// Es gibt genau eine Zeile pro (Award, Liga, User); Umentscheiden überschreibt sie.
/// </summary>
public class SeasonAwardNomination
{
    public int Id { get; set; }

    public int AwardId { get; set; }

    [MaxLength(64)]
    public string LeagueId { get; set; } = string.Empty;

    /// <summary>Fahrername aus dem Kader der Liga (Standings, aktuelle Saison).</summary>
    [MaxLength(128)]
    public string Driver { get; set; } = string.Empty;

    /// <summary>Discord-User-Id (pseudonym); nur für die Ein-Stimme-Regel.</summary>
    [MaxLength(32)]
    public string DiscordId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string DiscordName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Ein Gewinner (oder ein Gleichstand-Kandidat) eines Awards in einer Liga.</summary>
public sealed record SeasonAwardWinnerRow(string LeagueId, string Driver, int Votes, bool Tied);

/// <summary>Ein Fahrer im Kader einer Liga — Option des Nominierungs-Selects.</summary>
public sealed record SeasonAwardRosterEntry(string Driver, string Team, bool IsReserve);

/// <summary>
/// Auswertung der Stimmen. Reine Funktion, damit sie ohne Datenbank prüfbar ist.
/// </summary>
public static class SeasonAwardTally
{
    /// <summary>
    /// Gewinner je Liga innerhalb eines Awards: alle Fahrer mit der höchsten
    /// Stimmenzahl. Gleichstand ist kein Fehler — er wird als solcher ausgewiesen
    /// (<see cref="SeasonAwardWinnerRow.Tied"/>), statt ihn stillschweigend
    /// alphabetisch zu entscheiden.
    /// </summary>
    public static IReadOnlyList<SeasonAwardWinnerRow> Winners(IEnumerable<SeasonAwardNomination> nominations)
    {
        return nominations
            .GroupBy(n => n.LeagueId, StringComparer.Ordinal)
            .SelectMany(league =>
            {
                var counts = league
                    .GroupBy(n => n.Driver, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new { Driver = g.First().Driver, Votes = g.Count() })
                    .ToList();

                var max = counts.Max(c => c.Votes);
                var tied = counts.Count(c => c.Votes == max) > 1;

                return counts
                    .Where(c => c.Votes == max)
                    .OrderBy(c => c.Driver, StringComparer.OrdinalIgnoreCase)
                    .Select(c => new SeasonAwardWinnerRow(league.Key, c.Driver, c.Votes, tied));
            })
            .ToList();
    }
}

// ── ViewModels öffentliche Seite ───────────────────────────────────────────────

public class SeasonAwardsPageViewModel
{
    /// <summary>Offene Awards, gruppiert nach Liga (nur Ligen mit Kader).</summary>
    public List<SeasonAwardLeagueViewModel> OpenLeagues { get; set; } = new();

    /// <summary>Geschlossene Awards mit Gewinnern, neueste zuerst.</summary>
    public List<SeasonAwardResultViewModel> Results { get; set; } = new();

    public Dictionary<string, string> LeagueNames { get; set; } = new(StringComparer.Ordinal);

    public bool IsAuthenticated { get; set; }
}

public class SeasonAwardLeagueViewModel
{
    public League League { get; set; } = new();
    public List<SeasonAwardVoteCardViewModel> Awards { get; set; } = new();
}

public class SeasonAwardVoteCardViewModel
{
    public SeasonAward Award { get; set; } = new();
    public List<SeasonAwardRosterEntry> Roster { get; set; } = new();

    /// <summary>Fahrer, den der aktuelle User hier gewählt hat (null = noch keine Stimme).</summary>
    public string? MyDriver { get; set; }
}

public class SeasonAwardResultViewModel
{
    public SeasonAward Award { get; set; } = new();
    public IReadOnlyList<SeasonAwardWinnerRow> Winners { get; set; } = Array.Empty<SeasonAwardWinnerRow>();
    public int TotalVotes { get; set; }
}

// ── ViewModel Admin ────────────────────────────────────────────────────────────

public class AdminSeasonAwardsViewModel
{
    public List<SeasonAward> Awards { get; set; } = new();

    /// <summary>Stimmenverteilung je Award: (Liga, Fahrer, Anzahl), absteigend.</summary>
    public Dictionary<int, List<AdminSeasonAwardTallyRow>> Tallies { get; set; } = new();

    public Dictionary<string, string> LeagueNames { get; set; } = new(StringComparer.Ordinal);

    public int TotalVotes => Tallies.Values.Sum(rows => rows.Sum(r => r.Votes));
}

public sealed record AdminSeasonAwardTallyRow(string LeagueId, string Driver, int Votes);
