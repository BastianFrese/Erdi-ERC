using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Erdi_ERC.Tests.Services;

/// <summary>
/// Auswahl des nächsten/aktuellen Streams. Beide Dienste verglichen die Wanduhrzeit aus
/// <c>StreamSchedule.StartAt</c> mit <c>DateTime.UtcNow</c> — im Sommer lag das Ergebnis
/// dadurch 2 h daneben (siehe Docs/Features/Zeitzonen-Konvention.md).
///
/// Die Tests arbeiten mit Offsets zu <c>DateTime.Now</c>, damit sie unabhängig von der
/// Serverzeitzone korrekt sind. Ob sie den alten Fehler wirklich fangen, hängt an der
/// Zeitzone des Testrechners: nur bei einem Offset ≠ 0 fallen Alt- und Neuverhalten
/// auseinander (auf einem UTC-Host wäre der Bug unsichtbar).
/// </summary>
public class StreamScheduleQueryServiceTests
{
    private static StreamScheduleQueryService BuildService(SqliteTestContext ctx) =>
        new(ctx.Db, new MemoryCache(new MemoryCacheOptions()));

    private static void SeedStream(SqliteTestContext ctx, string title, DateTime startAt, int durationMinutes = 180, bool recurring = false, int? dayOfWeek = null, TimeSpan? timeOfDay = null)
    {
        var seed = ctx.NewContext();
        seed.StreamSchedules.Add(new StreamSchedule
        {
            Title = title,
            StartAt = startAt,
            DurationMinutes = durationMinutes,
            IsRecurring = recurring,
            DayOfWeek = dayOfWeek,
            TimeOfDay = timeOfDay,
        });
        seed.SaveChanges();
    }

    [Fact]
    public async Task GetNextStreamScheduleAsync_ignoresStreamThatAlreadyStarted()
    {
        using var ctx = new SqliteTestContext();
        SeedStream(ctx, "Läuft schon", DateTime.Now.AddMinutes(-30));
        SeedStream(ctx, "Später", DateTime.Now.AddMinutes(30));

        var next = await BuildService(ctx).GetNextStreamScheduleAsync();

        // Mit dem alten Vergleich gegen UtcNow galt "Läuft schon" im Sommer noch als
        // kommend und wurde als nächster Stream angezeigt.
        Assert.NotNull(next);
        Assert.Equal("Später", next!.Title);
    }

    [Fact]
    public async Task GetNextStreamScheduleAsync_returnsNullWhenOnlyPastStreamsExist()
    {
        using var ctx = new SqliteTestContext();
        SeedStream(ctx, "Gestern", DateTime.Now.AddDays(-1));

        var next = await BuildService(ctx).GetNextStreamScheduleAsync();

        Assert.Null(next);
    }

    [Fact]
    public async Task GetNextStreamScheduleAsync_recurringStream_returnsUpcomingLocalOccurrence()
    {
        using var ctx = new SqliteTestContext();
        // Wochentag in 3 Tagen um 21:00 — liegt garantiert in der Zukunft.
        var dayOfWeek = ((int)DateTime.Now.DayOfWeek + 3) % 7;
        SeedStream(ctx, "Wöchentlich", DateTime.Now.AddDays(-7), recurring: true, dayOfWeek: dayOfWeek, timeOfDay: new TimeSpan(21, 0, 0));

        var next = await BuildService(ctx).GetNextStreamScheduleAsync();

        Assert.NotNull(next);
        Assert.Equal(21, next!.StartAt.Hour);
        Assert.Equal(dayOfWeek, (int)next.StartAt.DayOfWeek);
        Assert.True(next.StartAt > DateTime.Now);
    }

    [Fact]
    public async Task GetNextStreamScheduleAsync_picksEarliestOfSeveralCandidates()
    {
        using var ctx = new SqliteTestContext();
        SeedStream(ctx, "In 3 Tagen", DateTime.Now.AddDays(3));
        SeedStream(ctx, "In 2 Stunden", DateTime.Now.AddHours(2));

        var next = await BuildService(ctx).GetNextStreamScheduleAsync();

        Assert.Equal("In 2 Stunden", next!.Title);
    }

    [Fact]
    public async Task GetNextStreamScheduleAsync_returnsNullWithoutSchedules()
    {
        using var ctx = new SqliteTestContext();

        var next = await BuildService(ctx).GetNextStreamScheduleAsync();

        Assert.Null(next);
    }
}

/// <summary>
/// "Live jetzt"-Banner im Layout. Es verglich <c>StartAt</c> (Wanduhrzeit) mit
/// <c>DateTime.UtcNow</c> und erschien dadurch im Sommer erst 2 h nach Streamstart.
/// </summary>
public class LayoutDataServiceActiveStreamTests
{
    [Fact]
    public async Task GetLayoutDataAsync_streamStartedThirtyMinutesAgo_isActive()
    {
        using var ctx = new SqliteTestContext();
        var seed = ctx.NewContext();
        seed.StreamSchedules.Add(new StreamSchedule
        {
            Title = "Live-Rennen",
            StartAt = DateTime.Now.AddMinutes(-30),
            DurationMinutes = 180,
        });
        seed.SaveChanges();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var data = await new LayoutDataService(ctx.Db, cache).GetLayoutDataAsync();

        Assert.NotNull(data.ActiveStream);
        Assert.Equal("Live-Rennen", data.ActiveStream!.Title);
    }

    [Fact]
    public async Task GetLayoutDataAsync_streamEndedHoursAgo_isNotActive()
    {
        using var ctx = new SqliteTestContext();
        var seed = ctx.NewContext();
        seed.StreamSchedules.Add(new StreamSchedule
        {
            Title = "Vorbei",
            StartAt = DateTime.Now.AddHours(-5),
            DurationMinutes = 60,
        });
        seed.SaveChanges();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var data = await new LayoutDataService(ctx.Db, cache).GetLayoutDataAsync();

        Assert.Null(data.ActiveStream);
    }
}
