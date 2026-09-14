using Erdi_ERC.Controllers;
using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Erdi_ERC.Options;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erdi_ERC.Tests.Controllers;

/// <summary>
/// Regression für den Prod-Crash auf /fahrerkarten und /Stats/DriverLevels
/// (NullReferenceException in <c>CacheExtensions.TryGetValue</c>): Der Konstruktor von
/// <see cref="StatsController"/> nahm <c>IMemoryCache cache</c> entgegen, wies aber nie
/// <c>_cache = cache;</c> zu (Commit 1c30a6f). Mit injiziertem Cache müssen beide Seiten
/// ein normales ViewResult liefern statt zu crashen.
/// </summary>
public class StatsControllerCacheTests
{
    [Fact]
    public async Task DriverLevels_WithInjectedCache_ReturnsView()
    {
        using var ctx = new SqliteTestContext();
        var controller = CreateController(ctx);

        var result = await controller.DriverLevels();

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<DriverLevelsPageViewModel>(view.Model);
        Assert.NotNull(vm);
        Assert.Empty(vm.Entries);
    }

    [Fact]
    public async Task DriverCards_WithInjectedCache_ReturnsView()
    {
        using var ctx = new SqliteTestContext();
        var controller = CreateController(ctx);

        var result = await controller.DriverCards();

        var view = Assert.IsType<ViewResult>(result);
        Assert.NotNull(view.Model);
    }

    private static StatsController CreateController(SqliteTestContext ctx)
        => new(
            ctx.Db,
            new StubWebHostEnvironment(),
            Microsoft.Extensions.Options.Options.Create(new ApplicationOptions()),
            Microsoft.Extensions.Options.Options.Create(new F1ScoringOptions()),
            NullLogger<StatsController>.Instance,
            new MemoryCache(new MemoryCacheOptions()));

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Erdi-ERC.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Testing";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
