using System.ComponentModel.DataAnnotations;

namespace Erdi_ERC.Models;

/// <summary>
/// Wartelisteneintrag für User, deren Wunsch-Liga voll ist. Stammfahrer-Bewerbungen auf
/// voller Liga werden vom Service automatisch auf die Warteliste umgeleitet. Admin kann
/// einzelne Einträge später zu regulären Bewerbungen promovieren.
/// </summary>
public class WaitlistEntry
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
    public string LeagueId { get; set; } = string.Empty;

    /// <summary>Season-Kennung (z.B. "2026"). Position wird pro (Liga, Season) vergeben.</summary>
    [Required]
    [MaxLength(16)]
    public string Season { get; set; } = string.Empty;

    /// <summary>1-indexed Reihenfolge pro (Liga, Season).</summary>
    public int Position { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Wenn der Admin den Eintrag zu einer Application promoviert hat.</summary>
    [MaxLength(64)]
    public string? PromotedToApplicationId { get; set; }

    // Navigation
    public League? League { get; set; }
}