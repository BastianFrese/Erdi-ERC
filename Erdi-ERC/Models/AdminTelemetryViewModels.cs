namespace Erdi_ERC.Models;

/// <summary>Zeilenprojektion für die Telemetrie-Übersicht (keine Finish-Rohdaten laden).</summary>
public sealed class TelemetryListItem
{
    public int Id { get; set; }
    public string? SourceTrack { get; set; }
    public DateTime? SourceDate { get; set; }
    public string? SourceSeason { get; set; }
    public string? SourceLeague { get; set; }
    public string? SenderDiscordId { get; set; }
    public DateTime ReceivedAt { get; set; }
    public int Status { get; set; }
    public int FinishCount { get; set; }
    public string? ReviewNote { get; set; }
}

/// <summary>Eine Finish-Zeile aus der Review-UI (admin-editiert). Wird beim POST
/// direkt vom Formular gebunden (<c>finishes[N].Driver</c> usw.).</summary>
public sealed class PendingRaceFinishInput
{
    public int Position { get; set; }
    public string Driver { get; set; } = string.Empty;
    public bool IsDnf { get; set; }
    public long? RaceTimeMs { get; set; }
    public int? QualifyingPosition { get; set; }
    public bool FastestLap { get; set; }
}

/// <summary>Gesamt-POST des Accept-Formulars auf der Telemetrie-Detail-Seite.</summary>
public sealed class TelemetryAcceptInput
{
    public int PendingId { get; set; }
    public string LeagueId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Track { get; set; } = string.Empty;
    public string? Season { get; set; }
    public string? FastestLapDriver { get; set; }

    /// <summary>Punkte-Faktor bei Rennabbruch (100/75/50). Vorbelegt aus der Distanz,
    /// im Formular übersteuerbar.</summary>
    public int PointsPercent { get; set; } = Erdi_ERC.Helpers.RacePointsFactor.Full;

    public List<PendingRaceFinishInput> Finishes { get; set; } = new();
}

/// <summary>Detail-Seite eines Pending-Entwurfs (Review).</summary>
public sealed class TelemetryDetailViewModel
{
    public PendingRaceResult Pending { get; set; } = null!;

    /// <summary>Anzeigename des Key-Inhabers, der das Ergebnis gesendet hat.</summary>
    public string? SenderName { get; set; }

    public string? DeciderName { get; set; }

    /// <summary>Ligadropdown für das Accept-Formular (nicht-archivierte Ligen).</summary>
    public List<League> Leagues { get; set; } = new();

    /// <summary>
    /// Reprozessierter Payload — liefert Reserve-/Gast-Zuordnungen für die Anzeige
    /// (der Pending hält diese bewusst nicht als eigene Tabellen).
    /// </summary>
    public Erdi_ERC.Services.ParsedTelemetryResult? Parsed { get; set; }

    /// <summary>Aus der Distanz abgeleiteter Faktor-Vorschlag (100/75/50) für die
    /// Vorbelegung des Punkte-Selects. 100, wenn keine Distanz vorliegt.</summary>
    public int SuggestedPointsPercent { get; set; } = Erdi_ERC.Helpers.RacePointsFactor.Full;

    /// <summary>Gefahrene Quote in Prozent für die Anzeige („31 / 44 Runden (70 %)"),
    /// oder null ohne Distanzangabe.</summary>
    public int? CompletedPercent { get; set; }
}
