using System.ComponentModel.DataAnnotations;

namespace Erdi_ERC.Models;

/// <summary>
/// Persönlicher Sende-Key eines Fahrers für den Telemetrie-Ingest
/// (<c>/api/telemetry/race</c>). Ein Key gehört zu genau einem Fahrer-Profil
/// (<see cref="DiscordId"/>); der Klartext wird beim Erzeugen EINMALIG angezeigt und
/// nur als SHA-256-Hash gespeichert. Gesperrte Keys (<see cref="RevokedAt"/> gesetzt)
/// werden von der Authentifizierung ignoriert.
/// </summary>
public class TelemetrySenderKey
{
    [Key]
    public int Id { get; set; }

    /// <summary>Fahrer-Profil (DriverProfile.DiscordId), dem der Key gehört.</summary>
    [Required]
    [MaxLength(32)]
    public string DiscordId { get; set; } = string.Empty;

    /// <summary>SHA-256 (hex, lowercase) des Klartext-Keys — nie der Klartext selbst.</summary>
    [Required]
    [MaxLength(64)]
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>Optionaler Anzeigename (z. B. „Laptop", „Streaming-PC").</summary>
    [MaxLength(64)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gesperrt am (= null = aktiv).</summary>
    public DateTime? RevokedAt { get; set; }
}
