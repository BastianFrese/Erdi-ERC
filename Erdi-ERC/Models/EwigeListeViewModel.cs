namespace <OWNER_HANDLE>_ERC.Models
{
    public class EwigeListeViewModel
    {
        public List<EwigeListeSheetViewModel> Sheets { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    public class EwigeListeSheetViewModel
    {
        public string Name { get; set; } = "";
        public List<List<string>> Rows { get; set; } = new();
    }
}
