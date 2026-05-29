namespace <OWNER_HANDLE>_ERC.Options
{
    /// <summary>
    /// Punkteverteilung pro Endposition (Index = Position - 1).
    /// Sektion: <c>F1Scoring</c>
    /// </summary>
    public sealed class F1ScoringOptions
    {
        public const string SectionName = "F1Scoring";

        public int[] PointMap { get; set; } =
        {
            25, 21, 18, 16, 14, 12, 10, 8, 7, 6,
             5,  4,  3,  2,  1, 0, 0, 0, 0, 0, 0, 0
        };
    }
}
