using Erdi_ERC.Controllers;
using Erdi_ERC.Data;
using Erdi_ERC.Helpers;
using Erdi_ERC.Models;
using Erdi_ERC.Options;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Erdi_ERC.Tests.Controllers;

/// <summary>
/// Regression für den Server-Crash auf /Stats/EwigeListe (ArgumentOutOfRangeException
/// beim Öffnen einer korrupten/leeren legacy active.xlsx). Eine kaputte Workbook-Datei
/// darf die Seite nicht mehr mit HTTP 500 abwürgen — Live-Daten reichen.
/// </summary>
public class StatsControllerEwigeListeTests
{
    [Fact]
    public async Task EwigeListe_RendersLiveSheets_WhenLegacyWorkbookIsCorrupt()
    {
        // Arrange: leeres 0-Byte-xlsx erzwingt denselben ClosedXML/OpenXML-Throw wie
        // eine teilweise hochgeladene/kaputte Datei (ArgumentOutOfRangeException in
        // GetPartById("id")). Wir replizieren den Prod-Crash minimal, indem wir den
        // Pfad aus EwigeWorkbookHelper.GetPath() nutzen und eine 0-Byte-Datei hinlegen.
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League
        {
            Id = "L1",
            Name = "TestLiga",
            SortOrder = 1,
            Standings = new List<DriverStanding>
            {
                new() { Driver = "Fahrer A", Team = "Team X", Points = 10, Wins = 1, Position = 1 }
            }
        });
        ctx.Db.SaveChanges();

        var tempRoot = Path.Combine(Path.GetTempPath(), "erdi-ewige-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "data", "ewige"));
        var corruptFile = Path.Combine(tempRoot, "data", "ewige", "active.xlsx");
        await File.WriteAllBytesAsync(corruptFile, Array.Empty<byte>()); // 0-Byte-Datei -> Parse-Crash

        try
        {
            var env = new StubWebHostEnvironment(tempRoot);
            var appOptions = Microsoft.Extensions.Options.Options.Create(new ApplicationOptions());
            var f1Scoring = Microsoft.Extensions.Options.Options.Create(new F1ScoringOptions());
            var controller = new StatsController(ctx.Db, env, appOptions, f1Scoring, NullLogger<StatsController>.Instance);

            // Act + Assert: kein Throw, Live-Sheet ist vorhanden.
            var result = await controller.EwigeListe();
            var view = Assert.IsType<ViewResult>(result);
            var vm = Assert.IsType<EwigeListeViewModel>(view.Model);
            Assert.NotNull(vm);
            // Live-Pfad liefert mindestens das Liga-Fahrer-Sheet (1 Header + 1 Driver-Row).
            Assert.Contains(vm.Sheets, s => s.Name.Contains("TestLiga", StringComparison.OrdinalIgnoreCase));
            Assert.True(vm.Sheets.Count > 0, "Live-Sheets müssen gerendert werden, auch wenn die XLSX kaputt ist.");
            Assert.True(string.IsNullOrWhiteSpace(vm.ErrorMessage),
                $"ErrorMessage darf NICHT gesetzt sein, wenn Live-Daten vorhanden sind: '{vm.ErrorMessage}'");
            // Sichtbarer Admin-Hinweis: Legacy-XLSX kaputt → Re-Upload nötig.
            Assert.False(string.IsNullOrWhiteSpace(vm.WarningMessage),
                "WarningMessage MUSS gesetzt sein, damit Admins wissen, dass die XLSX ersetzt werden muss.");
            Assert.Contains("Excel", vm.WarningMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SafeDeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task EwigeListe_RendersLiveSheets_WhenLegacyWorkbookIsMissing()
    {
        // Arrange: leeres Verzeichnis, keine active.xlsx — wirft keine Exception,
        // muss einfach nur Live-Sheets liefern.
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League
        {
            Id = "L1",
            Name = "NoWorkbookLiga",
            SortOrder = 1,
            Standings = new List<DriverStanding>
            {
                new() { Driver = "Solo", Team = "Solo Team", Points = 5, Wins = 0, Position = 1 }
            }
        });
        ctx.Db.SaveChanges();

        var tempRoot = Path.Combine(Path.GetTempPath(), "erdi-ewige-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var env = new StubWebHostEnvironment(tempRoot);
            var appOptions = Microsoft.Extensions.Options.Options.Create(new ApplicationOptions());
            var f1Scoring = Microsoft.Extensions.Options.Options.Create(new F1ScoringOptions());
            var controller = new StatsController(ctx.Db, env, appOptions, f1Scoring, NullLogger<StatsController>.Instance);

            var result = await controller.EwigeListe();
            var view = Assert.IsType<ViewResult>(result);
            var vm = Assert.IsType<EwigeListeViewModel>(view.Model);
            Assert.NotEmpty(vm.Sheets);
        }
        finally
        {
            SafeDeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task EwigeListe_RendersLiveAndLegacySheet_WhenLegacyWorkbookIsValid()
    {
        // Happy-Path-Regression: sichert ab, dass die try/catch-Umhüllung den Erfolgsfall
        // nicht sabotiert. Eine echte Mini-XLSX (1 Sheet "Archiv", 1 Header + 2 Rows)
        // wird gerendert — zusätzlich zu den Live-Sheets.
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League
        {
            Id = "L1",
            Name = "HappyLiga",
            SortOrder = 1,
            Standings = new List<DriverStanding>
            {
                new() { Driver = "Live Fahrer", Team = "Live Team", Points = 7, Wins = 0, Position = 1 }
            }
        });
        ctx.Db.SaveChanges();

        var tempRoot = Path.Combine(Path.GetTempPath(), "erdi-ewige-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "data", "ewige"));
        var validFile = Path.Combine(tempRoot, "data", "ewige", "active.xlsx");

        using (var wb = new ClosedXML.Excel.XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Archiv");
            ws.Cell(1, 1).Value = "Driver";
            ws.Cell(1, 2).Value = "Points";
            ws.Cell(2, 1).Value = "Archiv Fahrer";
            ws.Cell(2, 2).Value = 42;
            ws.Cell(3, 1).Value = "Archiv Fahrer 2";
            ws.Cell(3, 2).Value = 17;
            wb.SaveAs(validFile);
        }

        try
        {
            var env = new StubWebHostEnvironment(tempRoot);
            var appOptions = Microsoft.Extensions.Options.Options.Create(new ApplicationOptions());
            var f1Scoring = Microsoft.Extensions.Options.Options.Create(new F1ScoringOptions());
            var controller = new StatsController(ctx.Db, env, appOptions, f1Scoring, NullLogger<StatsController>.Instance);

            var result = await controller.EwigeListe();
            var view = Assert.IsType<ViewResult>(result);
            var vm = Assert.IsType<EwigeListeViewModel>(view.Model);
            Assert.NotEmpty(vm.Sheets);
            // Legacy-Sheet wurde tatsächlich eingelesen.
            Assert.Contains(vm.Sheets, s => string.Equals(s.Name, "Archiv", StringComparison.OrdinalIgnoreCase));
            // Kein Warnungs-Banner, kein leerer Empty-State.
            Assert.True(string.IsNullOrWhiteSpace(vm.ErrorMessage));
            Assert.True(string.IsNullOrWhiteSpace(vm.WarningMessage));
        }
        finally
        {
            SafeDeleteTempRoot(tempRoot);
        }
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public StubWebHostEnvironment(string contentRoot)
        {
            ContentRootPath = contentRoot;
            ContentRootFileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(contentRoot);
            WebRootPath = contentRoot;
            WebRootFileProvider = ContentRootFileProvider;
            ApplicationName = "Erdi-ERC.Tests";
            EnvironmentName = "Testing";
        }
        public string ApplicationName { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; }
        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; }
    }

    // ClosedXML/OpenXML halten das File-Handle auf Windows gelegentlich noch nach
    // dem Dispose — daher warten wir kurz, bevor wir die 0-Byte-Datei löschen.
    private static void SafeDeleteTempRoot(string tempRoot)
    {
        if (!Directory.Exists(tempRoot)) return;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
        }
        // Best-Effort: einzelne Files löschen, Verzeichnis notfalls stehen lassen.
        try { Directory.Delete(tempRoot, recursive: true); } catch { /* ignore */ }
    }
}
