using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Erdi_ERC.Services
{
    /// <summary>
    /// Dateibasierte Laufzeit-Einstellungen (<c>&lt;ContentRoot&gt;/data/site-settings.json</c>).
    /// Bewusst KEINE DB-Tabelle: ein Flag ohne Migration, deploy-light, funktioniert auf
    /// erditest und prod identisch. 30s-IMemoryCache, damit das Layout (render pro Request)
    /// nicht bei jedem Aufruf die Datei liest — ein Toggle greift daher binnen ~30s.
    /// </summary>
    public class SiteSettingsService : ISiteSettingsService
    {
        private const string SettingsFileName = "site-settings.json";
        private const string DownloadsFolderName = "downloads";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

        private readonly string _settingsPath;
        private readonly string _downloadsPath;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SiteSettingsService> _logger;

        public SiteSettingsService(string contentRootPath, IMemoryCache cache, ILogger<SiteSettingsService> logger)
        {
            _settingsPath = Path.Combine(contentRootPath, "data", SettingsFileName);
            _downloadsPath = Path.Combine(contentRootPath, DownloadsFolderName);
            _cache = cache;
            _logger = logger;
        }

        private sealed class SettingsFile
        {
            public bool AppDownloadEnabled { get; set; }
            public bool MaintenanceMode { get; set; }
            public string? MaintenanceMessage { get; set; }
            public string? UpdatedAt { get; set; }
            public string? UpdatedBy { get; set; }
        }

        public bool IsAppDownloadEnabled() => Load().AppDownloadEnabled;

        public void SetAppDownloadEnabled(bool enabled, string changedBy)
            => Update(s => s.AppDownloadEnabled = enabled, changedBy, enabled ? "App-Download AKTIVIERT" : "App-Download DEAKTIVIERT");

        public bool HasInstallerFile()
        {
            if (!Directory.Exists(_downloadsPath)) return false;
            return Directory.EnumerateFiles(_downloadsPath, "*.exe").Any();
        }

        public bool IsMaintenanceMode() => Load().MaintenanceMode;

        public void SetMaintenanceMode(bool enabled, string changedBy)
            => Update(s => s.MaintenanceMode = enabled, changedBy, enabled ? "Wartungsmodus AKTIVIERT" : "Wartungsmodus DEAKTIVIERT");

        public string GetMaintenanceMessage() => Load().MaintenanceMessage ?? string.Empty;

        public void SetMaintenanceMessage(string message, string changedBy)
            => Update(s => s.MaintenanceMessage = message, changedBy, "Wartungsmeldung aktualisiert");

        private const string CacheKey = "site-settings-file";

        // Serialisiert Read-Modify-Write: zwei gleichzeitige Setter (z.B. zwei Superadmin-Sessions)
        // dürfen sich nicht gegenseitig überschreiben. Singleton → ein Lock reicht.
        private readonly object _writeLock = new();

        /// <summary>Read-Modify-Write unter Lock. Baut IMMER ein frisches <see cref="SettingsFile"/>
        /// aus dem Disk-Stand — nie das gecachte Objekt mutieren.</summary>
        private void Update(Action<SettingsFile> change, string changedBy, string logMessage)
        {
            lock (_writeLock)
            {
                var current = LoadFromDisk();
                var settings = new SettingsFile
                {
                    AppDownloadEnabled = current.AppDownloadEnabled,
                    MaintenanceMode = current.MaintenanceMode,
                    MaintenanceMessage = current.MaintenanceMessage
                };
                change(settings);
                Persist(settings, changedBy, logMessage);
            }
        }

        private void Persist(SettingsFile settings, string changedBy, string logMessage)
        {
            settings.UpdatedAt = DateTime.UtcNow.ToString("o");
            settings.UpdatedBy = changedBy;

            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);

            _cache.Remove(CacheKey);
            _logger.LogInformation("{LogMessage} (von {ChangedBy})", logMessage, changedBy);
        }

        private SettingsFile LoadFromDisk()
        {
            try
            {
                if (!File.Exists(_settingsPath)) return new SettingsFile();
                return JsonSerializer.Deserialize<SettingsFile>(File.ReadAllText(_settingsPath)) ?? new SettingsFile();
            }
            catch (Exception ex)
            {
                // Kaputte/gesperrte Datei darf die Seite nicht crashen — fail-open auf "aus".
                _logger.LogError(ex, "site-settings.json konnte nicht gelesen werden ({Path})", _settingsPath);
                return new SettingsFile();
            }
        }

        private SettingsFile Load() =>
            _cache.GetOrCreate(CacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                entry.Size = 1; // Pflicht: MemoryCache hat globales SizeLimit=1024 (Program.cs), ohne Size wirft GetOrCreate.
                return LoadFromDisk();
            })!;
    }
}