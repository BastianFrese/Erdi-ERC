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
            public string? UpdatedAt { get; set; }
            public string? UpdatedBy { get; set; }
        }

        public bool IsAppDownloadEnabled() => Load().AppDownloadEnabled;

        public void SetAppDownloadEnabled(bool enabled, string changedBy)
        {
            var settings = new SettingsFile
            {
                AppDownloadEnabled = enabled,
                UpdatedAt = DateTime.UtcNow.ToString("o"),
                UpdatedBy = changedBy
            };

            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);

            _cache.Remove(CacheKey);
            _logger.LogInformation("App-Download {State} (von {ChangedBy})", enabled ? "AKTIVIERT" : "DEAKTIVIERT", changedBy);
        }

        public bool HasInstallerFile()
        {
            if (!Directory.Exists(_downloadsPath)) return false;
            return Directory.EnumerateFiles(_downloadsPath, "*.exe").Any();
        }

        private const string CacheKey = "site-settings-file";

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