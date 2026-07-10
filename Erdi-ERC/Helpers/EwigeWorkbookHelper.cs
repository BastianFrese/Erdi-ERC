namespace <OWNER_HANDLE>_ERC.Helpers
{
    /// <summary>
    /// Zentraler Pfad zur aktiven Ewige-Liste-Arbeitsmappe — genutzt von Stats (Anzeige),
    /// Admin (Upload) und AdminLeagueManagement (Archiv-Snapshot).
    /// </summary>
    public static class EwigeWorkbookHelper
    {
        public static string GetPath(IWebHostEnvironment env)
        {
            return Path.Combine(env.ContentRootPath, "data", "ewige", "active.xlsx");
        }
    }
}
