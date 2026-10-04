namespace Erdi_ERC.Models.UiComponents;

/// <summary>
/// Eine Zeile des Turms: Position, Fahrer, Team, Punkte.
/// <paramref name="TeamKey"/> ist der CssKey aus F1TeamsHelper (z. B. "ferrari") und
/// steuert den Farbbalken links an der Zeile. Ist er unbekannt, faellt die Anzeige
/// auf eine neutrale Farbe zurueck.
/// </summary>
public sealed record TowerRowVm(
    int Pos,
    string DriverName,
    string TeamKey,
    string TeamName,
    decimal Points);

/// <summary>
/// Der Turm — die Top N der Meisterschaft, feste linke Spalte auf jeder
/// oeffentlichen Seite.
///
/// Bewusst KEIN positionaler Record: die Zeilen werden beim Erzeugen sortiert und
/// gekuerzt, und ein positionaler Parameter mit gleichnamiger Eigenschaft ist in C#
/// nicht moeglich.
/// </summary>
public sealed record TowerVm
{
    /// <summary>Obergrenze, wenn maxRows unsinnig ist (0 oder negativ).</summary>
    private const int Sicherheitsgrenze = 20;

    public TowerVm(string title, IReadOnlyList<TowerRowVm>? rows, int maxRows = 10)
    {
        Title = title;
        MaxRows = maxRows <= 0 ? Sicherheitsgrenze : maxRows;
        // Die Wertung kommt aus der Datenbank; auf ihre Reihenfolge darf sich
        // die Anzeige nicht verlassen.
        Rows = (rows ?? Array.Empty<TowerRowVm>())
            .OrderBy(r => r.Pos)
            .Take(MaxRows)
            .ToArray();
    }

    public string Title { get; }
    public IReadOnlyList<TowerRowVm> Rows { get; }
    public int MaxRows { get; }
}
