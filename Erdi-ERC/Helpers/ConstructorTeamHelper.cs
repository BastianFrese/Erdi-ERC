namespace Erdi_ERC.Helpers;

/// <summary>
/// Regeln für den „Ohne Team"-Bucket der Constructors-Wertung.
///
/// Hintergrund: Fahrer ohne auflösbares Team landen in der Teamwertung unter dem
/// Pseudo-Konstrukteur „Ohne Team". Dieser Bucket entsteht aus <c>DriverStanding</c>-Zeilen
/// mit leerem <c>Team</c> — und zwar für JEDE, auch für Fahrer mit 0 Punkten. Dadurch stand
/// eine „Ohne Team"-Zeile in der Tabelle, die zu keiner einzigen Zahl beitrug (in Prod
/// betraf das den Großteil der teamlosen Fahrer).
///
/// Regel: ein Bucket ohne Punkte wird nicht geführt. Ein ECHTES Team bleibt auch mit
/// 0 Punkten sichtbar. Sobald im Bucket Punkte liegen, muss er sichtbar bleiben — sonst
/// verschwinden diese Punkte stillschweigend aus der Teamwertung, statt aufzufallen.
/// </summary>
public static class ConstructorTeamHelper
{
    public const string NoTeamLabel = "Ohne Team";

    /// <summary>Bucket-Label einer Standings-Zeile: leeres Team → „Ohne Team", sonst getrimmt.</summary>
    public static string LabelFor(string? team) =>
        string.IsNullOrWhiteSpace(team) ? NoTeamLabel : team.Trim();

    /// <summary>Ist der Name der Pseudo-Konstrukteur für Fahrer ohne auflösbares Team?</summary>
    public static bool IsNoTeamBucket(string? teamName) =>
        string.Equals((teamName ?? string.Empty).Trim(), NoTeamLabel, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Darf dieser Konstrukteur in der Tabelle stehen? Echte Teams immer, der
    /// „Ohne Team"-Bucket nur, wenn er Punkte trägt.
    /// </summary>
    public static bool IsVisibleConstructor(string? teamName, decimal points) =>
        points > 0 || !IsNoTeamBucket(teamName);
}
