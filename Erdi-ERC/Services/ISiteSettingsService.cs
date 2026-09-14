namespace Erdi_ERC.Services
{
    /// <summary>
    /// Seitenweite Laufzeit-Flags, die Admins zur Laufzeit umschalten können
    /// (ohne Deployment/Restart). Persistiert in <c>data/site-settings.json</c>.
    /// </summary>
    public interface ISiteSettingsService
    {
        /// <summary>Ist der öffentliche App-Download-Button sichtbar?</summary>
        bool IsAppDownloadEnabled();

        /// <summary>Schaltet den App-Download um (Datei wird sofort geschrieben).</summary>
        void SetAppDownloadEnabled(bool enabled, string changedBy);

        /// <summary>Liegt aktuell eine Installer-EXE im downloads-Ordner?</summary>
        bool HasInstallerFile();

        /// <summary>Ist der Wartungsmodus aktiv (alle Nicht-Admins sehen die Wartungsseite)?</summary>
        bool IsMaintenanceMode();

        /// <summary>Schaltet den Wartungsmodus um (Datei wird sofort geschrieben).</summary>
        void SetMaintenanceMode(bool enabled, string changedBy);

        /// <summary>Optionale Meldung, die auf der Wartungsseite angezeigt wird (leer = Standardtext).</summary>
        string GetMaintenanceMessage();

        /// <summary>Setzt die Meldung der Wartungsseite (Datei wird sofort geschrieben).</summary>
        void SetMaintenanceMessage(string message, string changedBy);
    }
}