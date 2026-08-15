namespace Erdi_ERC.Models
{
    public class EwigeListeViewModel
    {
        public List<EwigeListeSheetViewModel> Sheets { get; set; } = new();
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Nicht-blockierender Admin-Hinweis (z.B. "Legacy-XLSX nicht lesbar") — wird im
        /// Gegensatz zu <see cref="ErrorMessage"/> zusätzlich zu den Sheets angezeigt,
        /// nicht als Ganzseiten-Empty-State.
        /// </summary>
        public string? WarningMessage { get; set; }
    }

    public class EwigeListeSheetViewModel
    {
        public string Name { get; set; } = "";
        public List<List<string>> Rows { get; set; } = new();
    }
}
