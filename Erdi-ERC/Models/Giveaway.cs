using System.ComponentModel.DataAnnotations;

namespace Erdi_ERC.Models;

/// <summary>
/// Giveaway (Gewinnspiel) für die Infotafel auf der Startseite.
/// Die Sichtbarkeit ist rein zeitraum-basiert: Das Giveaway wird auf der Startseite
/// angezeigt, solange <c>StartAt &lt;= jetzt &lt;= EndAt</c> gilt, und verschwindet
/// danach automatisch. Verwaltung: Admin (Community → Giveaways).
/// </summary>
public class Giveaway
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(480)]
    public string? Description { get; set; }

    /// <summary>Was verlost wird (z. B. „Fanatec-Pedalset") — Kopfzeile der Karte.</summary>
    [MaxLength(160)]
    public string? Prize { get; set; }

    /// <summary>Beginn des Bewerbungszeitraums (Zeitraum-basiert sichtbar ab hier).</summary>
    public DateTime StartAt { get; set; }

    /// <summary>Deadline — ab diesem Zeitpunkt ist das Giveaway automatisch beendet.</summary>
    public DateTime EndAt { get; set; }

    /// <summary>Optionaler Teilnahme-/Detail-Link (http/https).</summary>
    [MaxLength(512)]
    public string? Link { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
