using System.ComponentModel.DataAnnotations;

namespace Erdi_ERC.Models;

/// <summary>
/// Eingehendes Rennergebnis der Telemetrie-App (telemetrie.erdi-erc.de).
/// Wird als vorläufiger Entwurf abgelegt, bis ein Admin es im Dashboard prüft und
/// als finales Ergebnis übernimmt (<see cref="Erdi_ERC.Services.PendingRacePromotionService.PromoteAsync"/>
/// bzw. RejectAsync). Bewusst KEINE Flag auf <see cref="RaceResult"/>: <see cref="RaceResult.LeagueId"/>
/// ist Pflicht, vorläufige Ergebnisse haben aber noch keine Liga — und alle bestehenden
/// Renn-Abfragen (StatsService, öffentliche Seiten) bleiben unantastbar.
/// </summary>
public class PendingRaceResult
{
    [Key]
    public int Id { get; set; }

    /// <summary>Roh-JSON des Telemetrie-Payloads (Audit + Reprozessierung).</summary>
    public string SourcePayload { get; set; } = string.Empty;

    /// <summary>SHA-256-Hash des normalisierten Payloads — Dedup-Schutz gegen Re-Send/Retry der Telemetrie-App.</summary>
    [MaxLength(64)]
    public string PayloadHash { get; set; } = string.Empty;

    /// <summary>Strecken-Name aus dem Payload (Vorschlag fürs Review, Admin kann korrigieren).</summary>
    [MaxLength(128)]
    public string? SourceTrack { get; set; }

    /// <summary>Renndatum/Startzeit aus dem Payload (Vorschlag, ISO-8601 UTC).</summary>
    public DateTime? SourceDate { get; set; }

    [MaxLength(32)]
    public string? SourceSeason { get; set; }

    /// <summary>Liga-Vorschlag aus der App-Auswahl (Name, frei). Admin kann beim Review abweichen.</summary>
    [MaxLength(128)]
    public string? SourceLeague { get; set; }

    /// <summary>Fahrer-Profil (DiscordId) des Key-Inhabers, der das Ergebnis gesendet hat.</summary>
    [MaxLength(32)]
    public string? SenderDiscordId { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Siehe <see cref="PendingRaceStatus"/>.</summary>
    public int Status { get; set; } = (int)PendingRaceStatus.Pending;

    public DateTime? DecidedAt { get; set; }

    [MaxLength(32)]
    public string? DecidedByDiscordId { get; set; }

    [MaxLength(1024)]
    public string? ReviewNote { get; set; }

    /// <summary>Link zum finalen <see cref="RaceResult"/> nach der Übernahme.</summary>
    public int? PromotedRaceResultId { get; set; }

    // Navigation
    public List<PendingRaceFinish> Finishes { get; set; } = new();
}

/// <summary>Eine einzelne Zielposition aus dem Telemetrie-Payload.</summary>
public class PendingRaceFinish
{
    [Key]
    public int Id { get; set; }

    public int PendingRaceResultId { get; set; }

    /// <summary>1-basiert; 0 = DNF (gleiche Position-Semantik wie <see cref="RaceFinish.Position"/>).</summary>
    public int Position { get; set; }

    [Required]
    [MaxLength(128)]
    public string Driver { get; set; } = string.Empty;

    public bool IsDnf { get; set; }

    public long? RaceTimeMs { get; set; }

    public int? QualifyingPosition { get; set; }

    public bool FastestLap { get; set; }

    public PendingRaceResult? PendingRaceResult { get; set; }
}

/// <summary>Lebenszyklus-Status eines eingehenden Telemetrie-Ergebnisses.</summary>
public enum PendingRaceStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
}
