namespace Erdi_ERC.Options
{
    /// <summary>
    /// Reihenfolge der Divisionen, in der Ligen sortiert werden.
    /// Sektion: <c>LeagueDivisions</c>
    /// </summary>
    public sealed class LeagueDivisionOptions
    {
        public const string SectionName = "LeagueDivisions";

        public string[] Order { get; set; } =
        {
            "Main Division 1",
            "Second Crossplay Division 2",
            "Rookie Crossplay Division 3",
            "Community Crossplay Event"
        };
    }
}
