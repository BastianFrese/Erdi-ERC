using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Erdi_ERC.Tests.Services;

/// <summary>
/// Regression für die Prod-Fehler "A second operation was started on this context
/// instance before a previous operation completed" auf jeder Page-Load: Der Layout-Build
/// startete die 3 unabhängigen Reads (letztes Rennen / aktiver Stream / Setup-Aktivität)
/// per <c>Task.WhenAll</c> auf DEMSELBEN scoped <see cref="AppDbContext"/> — EF-Core
/// erlaubt kein Concurrent-Use. Seit dem Fix laufen die Reads sequentiell, und alle drei
/// Datenfelder müssen gefüllt sein.
/// </summary>
public class LayoutDataServiceTests
{
    [Fact]
    public async Task GetLayoutDataAsync_SeededDb_PopulatesAllFields()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.Leagues.Add(new League
        {
            Id = "L1",
            Name = "LayoutTestLiga",
            SortOrder = 1,
            Standings = new List<DriverStanding>
            {
                // Unbekannter Fahrername → Winner-Lookup geht über die Standings
                // (Team "Ferrari" → F1TeamsHelper.CssKey "ferrari").
                new() { Driver = "ZZZ Layout Fahrer", Team = "Ferrari", Points = 20, Wins = 1, Position = 1 }
            }
        });
        ctx.Db.RaceResults.Add(new RaceResult
        {
            LeagueId = "L1",
            Date = DateTime.UtcNow.AddDays(-1),
            Track = "Imola",
            Winner = "ZZZ Layout Fahrer"
        });
        ctx.Db.StreamSchedules.Add(new StreamSchedule
        {
            Title = "Test Stream",
            // StartAt ist Wanduhrzeit (Server-Lokalzeit) — der Stream läuft seit 10 Minuten
            // und ist damit der aktive Stream. Mit UtcNow geseedet lag der Wert im Sommer
            // 2 h in der Vergangenheit und galt nicht mehr als laufend.
            StartAt = DateTime.Now.AddMinutes(-10),
            DurationMinutes = 120
        });
        ctx.Db.TrackSetups.Add(new TrackSetup
        {
            Track = "Imola",
            Title = "Test Setup",
            SetupText = "Setup",
            UpdatedAt = DateTime.UtcNow.AddHours(-2)
        });
        ctx.Db.SaveChanges();

        var service = new LayoutDataService(ctx.Db, new MemoryCache(new MemoryCacheOptions()));

        var data = await service.GetLayoutDataAsync();

        Assert.NotNull(data);
        Assert.False(string.IsNullOrWhiteSpace(data.WinnerTeamKey),
            "Winner-Team muss über die Standings aufgelöst werden.");
        Assert.NotNull(data.ActiveStream);
        Assert.NotNull(data.LatestSetupActivityUtc);
    }

    [Fact]
    public async Task GetLayoutDataAsync_SecondCall_ServesFromCache()
    {
        using var ctx = new SqliteTestContext();
        var service = new LayoutDataService(ctx.Db, new MemoryCache(new MemoryCacheOptions()));

        var first = await service.GetLayoutDataAsync();
        var second = await service.GetLayoutDataAsync();

        Assert.Same(first, second);
    }
}
