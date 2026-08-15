using System.ComponentModel.DataAnnotations;

namespace Erdi_ERC.Models;

/// <summary>
/// Eingehende Bewerbung eines Users auf eine Liga.
/// Wird durch den Admin geprüft und entweder angenommen (Accept) oder abgelehnt (Reject).
/// </summary>
public class Application
{
    [Key]
    [MaxLength(64)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string DiscordId { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string DiscordName { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string GamerTag { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string Platform { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string TargetLeagueId { get; set; } = string.Empty;

    /// <summary>Season-Kennung (z.B. "2026", "2026-H2", "Saison 5"). Wird serverseitig via
    /// <see cref="Erdi_ERC.Services.IApplicationTargetingService"/> aus Liga.NextSeason bzw.
    /// Liga.CurrentSeason abgeleitet — Client kann sie nicht manipulieren.</summary>
    [Required]
    [MaxLength(16)]
    public string Season { get; set; } = string.Empty;

    /// <summary>"Stammfahrer" oder "Reservefahrer".</summary>
    [Required]
    [MaxLength(32)]
    public string Role { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Motivation { get; set; }

    /// <summary>Siehe <see cref="ApplicationStatus"/>.</summary>
    public int Status { get; set; } = (int)ApplicationStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }

    [MaxLength(32)]
    public string? DecidedByDiscordId { get; set; }

    /// <summary>Admin-Notiz zum User (Rejektionsgrund o.ä.). Wird in MyApplication angezeigt.</summary>
    [MaxLength(1000)]
    public string? ReviewNote { get; set; }

    /// <summary>Snapshot des Discord-Join-Checks zum Submit-Zeitpunkt. Nur Admin-Info, kein harter Block.</summary>
    public bool DiscordJoinWarning { get; set; }

    [MaxLength(500)]
    public string? DiscordJoinWarningDetail { get; set; }

    // Navigation
    public League? TargetLeague { get; set; }
}

/// <summary>Lebenszyklus-Status einer Bewerbung.</summary>
public enum ApplicationStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
}
