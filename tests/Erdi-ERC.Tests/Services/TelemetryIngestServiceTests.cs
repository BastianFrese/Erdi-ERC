using Erdi_ERC.Helpers;
using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erdi_ERC.Tests.Services;

/// <summary>
/// Ingest-Tests: Key-Auth (401), Schema-Validierung (400), Liga-Gate (403 — nur aktive
/// Stammfahrer der genannten Liga dürfen senden), SHA-256-Dedup (idempotentes Re-POST → 200
/// mit gleicher Id) und Persistenz als Pending inkl. Sender + Liga-Vorschlag + Fastest-Lap-Flag.
/// </summary>
public class TelemetryIngestServiceTests
{
    private sealed class RecordingWebhook : IWebhookAutomationService
    {
        public List<string> Events { get; } = new();
        public Task FireAsync(string eventType, Dictionary<string, string> vars)
        {
            Events.Add(eventType);
            return Task.CompletedTask;
        }
        public Task<(bool Success, string? Error)> TestRuleAsync(int ruleId, Dictionary<string, string> vars, CancellationToken ct = default)
            => Task.FromResult((true, (string?)null));
    }

    private static TelemetryIngestService Build(SqliteTestContext ctx, out RecordingWebhook webhook)
    {
        var audit = new AdminAuditService(ctx.Db, new HttpContextAccessor());
        webhook = new RecordingWebhook();
        return new TelemetryIngestService(ctx.Db, new TelemetryKeyService(ctx.Db, audit), audit, webhook);
    }

    /// <summary>Seedet Profil + Liga + Standing und gibt den Klartext-Key des Senders zurück.</summary>
    private static async Task<string> SeedSenderAsync(
        SqliteTestContext ctx, string discordId = "d1", string driverName = "Max Mustermann",
        bool reserve = false, bool archivedLeague = false)
    {
        ctx.Db.DriverProfiles.Add(new DriverProfile { DiscordId = discordId, DiscordName = driverName });
        ctx.Db.Leagues.Add(new League { Id = "l1", Name = "ProLiga", IsArchived = archivedLeague });
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "l1", Driver = driverName, IsReserveDriver = reserve });
        await ctx.Db.SaveChangesAsync();

        var keys = new TelemetryKeyService(ctx.Db, new AdminAuditService(ctx.Db, new HttpContextAccessor()));
        return (await keys.GenerateAsync(discordId, null)).PlainKey!;
    }

    private static string Payload(string? league, params (int Pos, string Driver)[] finishes)
    {
        var finishesJson = string.Join(",", finishes.Select(f => $@"{{ ""position"": {f.Pos}, ""driver"": ""{f.Driver}"" }}"));
        var leagueJson = string.IsNullOrWhiteSpace(league) ? "" : $@", ""league"": ""{league}""";
        return $@"{{ ""track"": ""Spa""{leagueJson}, ""finishes"": [ {finishesJson} ] }}";
    }

    private static (int Pos, string Driver)[] TwoDrivers() =>
        new[] { (1, "Max Mustermann"), (2, "Anna Beispiel") };

    [Fact]
    public async Task Ingest_validPayloadWithLeague_returns201_andPersistsPending()
    {
        using var ctx = new SqliteTestContext();
        var key = await SeedSenderAsync(ctx);

        var svc = Build(ctx, out var webhook);
        var result = await svc.IngestAsync(Payload("ProLiga", TwoDrivers()), key);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);

        await using var verify = ctx.NewContext();
        var pending = await verify.PendingRaceResults
            .Include(p => p.Finishes)
            .SingleAsync();
        Assert.Equal(result.PendingId, pending.Id);
        Assert.Equal("ProLiga", pending.SourceLeague);
        Assert.Equal("d1", pending.SenderDiscordId);
        Assert.Equal("Spa", pending.SourceTrack);
        Assert.Equal((int)PendingRaceStatus.Pending, pending.Status);
        Assert.Equal(2, pending.Finishes.Count);
        Assert.Equal(64, pending.PayloadHash.Length);

        Assert.Contains(WebhookEvents.TelemetryResultReceived, webhook.Events);
        var audit = await verify.AdminAuditLogs.SingleOrDefaultAsync(a => a.Action == "TelemetryResultReceived");
        Assert.NotNull(audit);
        Assert.Contains("ProLiga", audit!.Details);
    }

    [Fact]
    public async Task Ingest_payloadWithDistance_persistsLapsForFactorDerivation()
    {
        // Die App meldet Soll-Distanz (totalLaps) und gefahrene Runden (numLaps), damit der
        // Review-Dialog den Rennabbruch-Faktor vorbelegen kann — die Rohdaten werden am
        // Pending gespeichert, nicht der fertige Faktor (Regel bleibt nachträglich änderbar).
        using var ctx = new SqliteTestContext();
        var key = await SeedSenderAsync(ctx);

        var json = """
            {
              "track": "Spa",
              "league": "ProLiga",
              "totalLaps": 44,
              "finishes": [
                { "position": 1, "driver": "Max Mustermann", "numLaps": 31 },
                { "position": 2, "driver": "Anna Beispiel", "numLaps": 30 },
                { "position": 0, "driver": "Ausfall", "numLaps": 12, "dnf": true }
              ]
            }
            """;

        var svc = Build(ctx, out _);
        var result = await svc.IngestAsync(json, key);
        Assert.True(result.IsSuccess);

        await using var verify = ctx.NewContext();
        var pending = await verify.PendingRaceResults.SingleAsync();
        Assert.Equal(44, pending.TotalLaps);
        Assert.Equal(31, pending.CompletedLaps); // DNF-Zeile (12 Runden) zählt nicht
        Assert.Equal(RacePointsFactor.Half,
            RacePointsFactor.DeriveFromDistance(pending.TotalLaps, pending.CompletedLaps)); // 70 %
    }

    [Fact]
    public async Task Ingest_payloadWithoutDistance_leavesLapsNull()
    {
        // Alte App-Versionen senden die Distanz nicht → keine Ableitung, Rennen gilt als voll.
        using var ctx = new SqliteTestContext();
        var key = await SeedSenderAsync(ctx);

        var svc = Build(ctx, out _);
        var result = await svc.IngestAsync(Payload("ProLiga", TwoDrivers()), key);
        Assert.True(result.IsSuccess);

        await using var verify = ctx.NewContext();
        var pending = await verify.PendingRaceResults.SingleAsync();
        Assert.Null(pending.TotalLaps);
        Assert.Null(pending.CompletedLaps);
        Assert.Equal(RacePointsFactor.Full,
            RacePointsFactor.DeriveFromDistance(pending.TotalLaps, pending.CompletedLaps));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("falscher-key")]
    public async Task Ingest_missingOrWrongKey_returns401(string? apiKey)
    {
        using var ctx = new SqliteTestContext();
        await SeedSenderAsync(ctx);
        var svc = Build(ctx, out _);

        var result = await svc.IngestAsync(Payload("ProLiga", TwoDrivers()), apiKey);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Null(result.PendingId);
        Assert.Equal("Ungültiger oder fehlender API-Key.", result.Error);
    }

    [Fact]
    public async Task Ingest_malformedJson_returns400()
    {
        using var ctx = new SqliteTestContext();
        var key = await SeedSenderAsync(ctx);
        var svc = Build(ctx, out _);

        var result = await svc.IngestAsync("""{ "kaputt": """, key);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("Ungültiges JSON.", result.Error);
    }

    [Fact]
    public async Task Ingest_foreignLeague_returns403()
    {
        using var ctx = new SqliteTestContext();
        var key = await SeedSenderAsync(ctx); // Sender ist Mitglied in ProLiga
        var svc = Build(ctx, out _);

        var result = await svc.IngestAsync(Payload("AndereLiga", TwoDrivers()), key);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Contains("kein aktiver Stammfahrer", result.Error);
        Assert.Empty(await ctx.NewContext().PendingRaceResults.ToListAsync());
    }

    [Fact]
    public async Task Ingest_reserveDriverCannotSendForLeague_returns403()
    {
        using var ctx = new SqliteTestContext();
        var key = await SeedSenderAsync(ctx, reserve: true);
        var svc = Build(ctx, out _);

        var result = await svc.IngestAsync(Payload("ProLiga", TwoDrivers()), key);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public async Task Ingest_withoutLeague_doesNotApplyGate_returns201()
    {
        using var ctx = new SqliteTestContext();
        var key = await SeedSenderAsync(ctx);
        var svc = Build(ctx, out var webhook);

        var result = await svc.IngestAsync(Payload(null, TwoDrivers()), key);
        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        var pending = await ctx.NewContext().PendingRaceResults.SingleAsync();
        Assert.Null(pending.SourceLeague);
    }

    [Fact]
    public async Task Ingest_duplicateRePost_returnsSameId200()
    {
        using var ctx = new SqliteTestContext();
        var key = await SeedSenderAsync(ctx);
        var svc = Build(ctx, out var webhook);
        var body = Payload("ProLiga", TwoDrivers());

        var first = await svc.IngestAsync(body, key);
        var second = await svc.IngestAsync(body, key);

        Assert.Equal(StatusCodes.Status201Created, first.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, second.StatusCode);
        Assert.Equal(first.PendingId, second.PendingId);
        Assert.Single(await ctx.NewContext().PendingRaceResults.ToListAsync());
        Assert.Single(webhook.Events); // nur ein Webhook für das eine (erste) Ingest
    }

    [Fact]
    public async Task Ingest_rootFastestLap_marksMatchingFinishRow()
    {
        using var ctx = new SqliteTestContext();
        var key = await SeedSenderAsync(ctx);
        var svc = Build(ctx, out _);

        var json = """
            { "track": "Spa", "league": "ProLiga", "fastestLap": "Anna Beispiel",
              "finishes": [ { "position": 1, "driver": "Max Mustermann" }, { "position": 2, "driver": "Anna Beispiel" } ] }
            """;
        var result = await svc.IngestAsync(json, key);
        Assert.True(result.IsSuccess);

        var pending = await ctx.NewContext().PendingRaceResults.Include(p => p.Finishes).SingleAsync();
        Assert.True(pending.Finishes.Single(f => f.Driver == "Anna Beispiel").FastestLap);
        Assert.False(pending.Finishes.Single(f => f.Driver == "Max Mustermann").FastestLap);
    }

    [Fact]
    public async Task MemberLeagues_returnsOnlyActiveMembership_inNonArchivedLeagues()
    {
        using var ctx = new SqliteTestContext();
        ctx.Db.DriverProfiles.Add(new DriverProfile { DiscordId = "d1", DiscordName = "Max Mustermann" });
        ctx.Db.Leagues.Add(new League { Id = "aktiveLiga", Name = "Aktive Liga" });
        ctx.Db.Leagues.Add(new League { Id = "reserveLiga", Name = "Reserve Liga" });
        ctx.Db.Leagues.Add(new League { Id = "archivLiga", Name = "Archiv Liga", IsArchived = true });
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "aktiveLiga", Driver = "Max Mustermann", IsReserveDriver = false });
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "archivLiga", Driver = "Max Mustermann", IsReserveDriver = false });
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = "reserveLiga", Driver = "Max Mustermann", IsReserveDriver = true });
        await ctx.Db.SaveChangesAsync();

        var svc = Build(ctx, out _);
        var leagues = await svc.MemberLeaguesAsync("d1");

        var league = Assert.Single(leagues);
        Assert.Equal("aktiveLiga", league.LeagueId);
        Assert.Equal("Aktive Liga", league.Name);
    }

    [Fact]
    public async Task MemberLeagues_unknownProfile_returnsEmpty()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx, out _);
        var leagues = await svc.MemberLeaguesAsync("unbekannt");
        Assert.Empty(leagues);
    }
}
