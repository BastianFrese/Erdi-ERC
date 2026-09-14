using Erdi_ERC.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Erdi_ERC.Tests;

/// <summary>
/// Sichert die Datei-Persistenz des <see cref="SiteSettingsService"/> ab:
/// Default (aus), Toggle-Persistenz und Neuladen aus einer frischen Service-Instanz
/// (simuliert App-Restart). Läuft gegen einen Temp-Ordner, keine echten Daten.
/// </summary>
public class SiteSettingsServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    public SiteSettingsServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "erc-sitetests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        _cache.Dispose();
        Directory.Delete(_tempRoot, recursive: true);
    }

    private SiteSettingsService Make() =>
        new(_tempRoot, _cache, NullLogger<SiteSettingsService>.Instance);

    [Fact]
    public void Default_WithoutFile_IsDisabled()
    {
        Assert.False(Make().IsAppDownloadEnabled());
    }

    [Fact]
    public void SetTrue_PersistsValue()
    {
        var svc = Make();
        svc.SetAppDownloadEnabled(true, "123456789012345678");
        Assert.True(svc.IsAppDownloadEnabled());
    }

    [Fact]
    public void FreshInstance_ReloadsFromDisk_SimulatingRestart()
    {
        var svc = Make();
        svc.SetAppDownloadEnabled(true, "123456789012345678");

        // Neue Instanz + neuer Cache = App-Restart: Wert muss von der Datei kommen.
        using var freshCache = new MemoryCache(new MemoryCacheOptions());
        var fresh = new SiteSettingsService(_tempRoot, freshCache, NullLogger<SiteSettingsService>.Instance);

        Assert.True(fresh.IsAppDownloadEnabled());
    }

    [Fact]
    public void ToggleOff_AfterToggleOn_IsDisabled()
    {
        var svc = Make();
        svc.SetAppDownloadEnabled(true, "123456789012345678");
        svc.SetAppDownloadEnabled(false, "123456789012345678");
        Assert.False(svc.IsAppDownloadEnabled());
    }

    [Fact]
    public void HasInstallerFile_WithoutExe_IsFalse()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, "downloads"));
        Assert.False(Make().HasInstallerFile());
    }

    [Fact]
    public void HasInstallerFile_WithExe_IsTrue()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, "downloads"));
        File.WriteAllBytes(Path.Combine(_tempRoot, "downloads", "any-name.exe"), [1, 2, 3]);
        Assert.True(Make().HasInstallerFile());
    }

    // ---- Wartungsmodus ----

    [Fact]
    public void Default_WithoutFile_MaintenanceIsOff()
    {
        Assert.False(Make().IsMaintenanceMode());
    }

    [Fact]
    public void SetMaintenanceModeTrue_PersistsValue()
    {
        var svc = Make();
        svc.SetMaintenanceMode(true, "123456789012345678");
        Assert.True(svc.IsMaintenanceMode());
    }

    [Fact]
    public void FreshInstance_ReloadsMaintenanceFromDisk_SimulatingRestart()
    {
        var svc = Make();
        svc.SetMaintenanceMode(true, "123456789012345678");

        using var freshCache = new MemoryCache(new MemoryCacheOptions());
        var fresh = new SiteSettingsService(_tempRoot, freshCache, NullLogger<SiteSettingsService>.Instance);

        Assert.True(fresh.IsMaintenanceMode());
    }

    [Fact]
    public void ToggleOff_AfterToggleOn_MaintenanceIsOff()
    {
        var svc = Make();
        svc.SetMaintenanceMode(true, "123456789012345678");
        svc.SetMaintenanceMode(false, "123456789012345678");
        Assert.False(svc.IsMaintenanceMode());
    }

    [Fact]
    public void SetMaintenanceMessage_PersistsAndReloads()
    {
        var svc = Make();
        svc.SetMaintenanceMessage("Wir sind am 15.09. um 20 Uhr zurück.", "123456789012345678");

        using var freshCache = new MemoryCache(new MemoryCacheOptions());
        var fresh = new SiteSettingsService(_tempRoot, freshCache, NullLogger<SiteSettingsService>.Instance);

        Assert.Equal("Wir sind am 15.09. um 20 Uhr zurück.", fresh.GetMaintenanceMessage());
    }

    [Fact]
    public void Default_WithoutFile_MaintenanceMessageIsEmpty()
    {
        Assert.Equal(string.Empty, Make().GetMaintenanceMessage());
    }

    // ---- Feld-Isolation: ein Toggle darf die anderen Flags nicht überschreiben ----

    [Fact]
    public void AppDownloadToggle_DoesNotWipeMaintenanceFlag()
    {
        var svc = Make();
        svc.SetMaintenanceMode(true, "123456789012345678");
        svc.SetMaintenanceMessage("Wartung!", "123456789012345678");

        svc.SetAppDownloadEnabled(true, "123456789012345678");

        Assert.True(svc.IsMaintenanceMode());
        Assert.Equal("Wartung!", svc.GetMaintenanceMessage());
    }

    [Fact]
    public void MaintenanceToggle_DoesNotWipeAppDownloadFlag()
    {
        var svc = Make();
        svc.SetAppDownloadEnabled(true, "123456789012345678");

        svc.SetMaintenanceMode(true, "123456789012345678");

        Assert.True(svc.IsAppDownloadEnabled());
    }

    [Fact]
    public void CorruptFile_FailsOpenToOff()
    {
        // Kaputte Datei darf niemanden aussperren: fail-open auf "Wartungsmodus aus".
        Directory.CreateDirectory(Path.Combine(_tempRoot, "data"));
        File.WriteAllText(Path.Combine(_tempRoot, "data", "site-settings.json"), "{ kaputt !!!");

        var svc = Make();
        Assert.False(svc.IsMaintenanceMode());
        Assert.False(svc.IsAppDownloadEnabled());
        Assert.Equal(string.Empty, svc.GetMaintenanceMessage());
    }
}