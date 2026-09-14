using Erdi_ERC.Services;
using Xunit;

namespace Erdi_ERC.Tests;

/// <summary>
/// Unit-Tests für den Telemetrie-Payload-Parser (statisch, DB-frei, analog RaceCsvParserTests).
/// Prüft das erwartete Schema: finishes mit Position (0 = DNF) / dnf-Flag, reserveAssignments,
/// guestAssignments und die deutschen Fehlermeldungen für den API-Client.
/// </summary>
public class TelemetryRaceParserTests
{
    [Fact]
    public void Parse_validPayloadWithLeague_returnsAllFields()
    {
        var result = TelemetryRaceParser.Parse("""
            {
              "track": "Silverstone",
              "date": "2026-09-09T20:15:00Z",
              "league": "ProLiga",
              "season": "2026",
              "fastestLap": "Max Mustermann",
              "finishes": [
                { "position": 1, "driver": "Max Mustermann", "raceTimeMs": 5400000, "qualifyingPosition": 2 },
                { "position": 2, "driver": "Anna Beispiel" }
              ]
            }
            """);

        Assert.Null(result.Error);
        Assert.NotNull(result.Result);
        Assert.Equal("Silverstone", result.Result!.Track);
        Assert.Equal("ProLiga", result.Result.League);
        Assert.Equal("2026", result.Result.Season);
        Assert.Equal("Max Mustermann", result.Result.FastestLap);
        Assert.Equal(new DateTime(2026, 9, 9, 20, 15, 0, DateTimeKind.Utc), result.Result.Date!.Value.ToUniversalTime());
        Assert.Equal(2, result.Result.Finishes.Count);

        var winner = result.Result.Finishes[0];
        Assert.Equal(1, winner.Position);
        Assert.False(winner.IsDnf);
        Assert.Equal(5400000, winner.RaceTimeMs);
        Assert.Equal(2, winner.QualifyingPosition);
    }

    [Fact]
    public void Parse_reserveAndGuestAssignments_areParsed()
    {
        var result = TelemetryRaceParser.Parse("""
            {
              "track": "Spa",
              "finishes": [ { "position": 1, "driver": "Max Mustermann" } ],
              "reserveAssignments": [ { "reserveDriver": "ErsatzMann", "mainDriver": "Stammfahrer" } ],
              "guestAssignments": [ { "guestDriver": "GastFahrer", "mainDriver": "HostFahrer" } ]
            }
            """);

        Assert.Null(result.Error);
        var reserve = Assert.Single(result.Result!.ReserveAssignments);
        Assert.Equal("ErsatzMann", reserve.Driver);
        Assert.Equal("Stammfahrer", reserve.MainDriver);
        var guest = Assert.Single(result.Result.GuestAssignments);
        Assert.Equal("GastFahrer", guest.Driver);
        Assert.Equal("HostFahrer", guest.MainDriver);
    }

    [Theory]
    [InlineData("positionZero")]   // position 0 → DNF
    [InlineData("dnfFlag")]        // explizites dnf-flag bei position > 0
    public void Parse_dnfVariants_areNormalizedToPositionZero(string variant)
    {
        var json = variant == "positionZero"
            ? """{ "track": "T", "finishes": [ { "position": 0, "driver": "Ausfall" }, { "position": 1, "driver": "Sieger" } ] }"""
            : """{ "track": "T", "finishes": [ { "position": 5, "driver": "Ausfall", "dnf": true }, { "position": 1, "driver": "Sieger" } ] }""";

        var result = TelemetryRaceParser.Parse(json);

        Assert.Null(result.Error);
        var dnf = result.Result!.Finishes.First(f => f.Driver == "Ausfall");
        Assert.True(dnf.IsDnf);
        Assert.Equal(0, dnf.Position);
        Assert.Equal(1, result.Result.Finishes.First(f => f.Driver == "Sieger").Position);
    }

    [Fact]
    public void Parse_emptyInput_returnsError() =>
        Assert.Equal("Kein JSON-Body empfangen.", TelemetryRaceParser.Parse(null).Error);

    [Fact]
    public void Parse_invalidJson_returnsError()
    {
        var result = TelemetryRaceParser.Parse("""{ "track": "Spa", """);
        Assert.Null(result.Result);
        Assert.Equal("Ungültiges JSON.", result.Error);
    }

    [Fact]
    public void Parse_tooLargePayload_returnsError()
    {
        var oversized = new string('x', TelemetryRaceParser.MaxInputLength + 1);
        var result = TelemetryRaceParser.Parse(oversized);
        Assert.Equal($"Payload zu groß ({oversized.Length} Zeichen, max. {TelemetryRaceParser.MaxInputLength}).", result.Error);
    }

    [Fact]
    public void Parse_missingFinishes_returnsError()
    {
        var result = TelemetryRaceParser.Parse("""{ "track": "T" }""");
        Assert.Equal("Feld 'finishes' fehlt — mindestens ein Fahrer ist Pflicht.", result.Error);
    }

    [Fact]
    public void Parse_emptyFinishes_returnsError()
    {
        var result = TelemetryRaceParser.Parse("""{ "track": "T", "finishes": [] }""");
        Assert.Equal("Feld 'finishes' muss ein nicht-leeres Array sein.", result.Error);
    }

    [Fact]
    public void Parse_allDnf_returnsError()
    {
        var result = TelemetryRaceParser.Parse("""
            { "track": "T", "finishes": [ { "position": 0, "driver": "A" }, { "position": 1, "driver": "B", "dnf": true } ] }
            """);
        Assert.Equal("Mindestens ein Fahrer muss das Ziel erreicht haben (nicht alle DNF).", result.Error);
    }

    [Fact]
    public void Parse_missingDriver_returnsError()
    {
        var result = TelemetryRaceParser.Parse("""{ "track": "T", "finishes": [ { "position": 1 } ] }""");
        Assert.Equal("Fahrer-Eintrag fehlt 'driver'.", result.Error);
    }

    [Fact]
    public void Parse_blankDriver_returnsError()
    {
        var result = TelemetryRaceParser.Parse("""{ "track": "T", "finishes": [ { "position": 1, "driver": "   " } ] }""");
        Assert.Equal("Fahrer-Eintrag hat leeren 'driver'.", result.Error);
    }

    [Fact]
    public void Parse_duplicatePosition_returnsError()
    {
        var result = TelemetryRaceParser.Parse("""
            { "track": "T", "finishes": [ { "position": 1, "driver": "A" }, { "position": 1, "driver": "B" } ] }
            """);
        Assert.Equal("Doppelte Zielposition 1 (Fahrer 'B').", result.Error);
    }

    [Fact]
    public void Parse_invalidDate_returnsError()
    {
        var result = TelemetryRaceParser.Parse("""
            { "track": "T", "date": "gestern", "finishes": [ { "position": 1, "driver": "A" } ] }
            """);
        Assert.Equal("Ungültiges Datumsformat in 'date' (erwartet ISO-8601, z. B. 2026-09-09T20:15:00Z).", result.Error);
    }

    [Fact]
    public void Parse_assignmentMissingMainDriver_returnsError()
    {
        var result = TelemetryRaceParser.Parse("""
            { "track": "T", "finishes": [ { "position": 1, "driver": "A" } ], "reserveAssignments": [ { "reserveDriver": "X" } ] }
            """);
        Assert.Equal("Eintrag in 'reserveAssignments' fehlt 'mainDriver'.", result.Error);
    }

    [Fact]
    public void Parse_moreThanMaxFinishes_returnsError()
    {
        var finishes = string.Join(",", Enumerable.Range(1, TelemetryRaceParser.MaxFinishes + 1)
            .Select(p => $@"{{ ""position"": {p}, ""driver"": ""D{p}"" }}"));
        var result = TelemetryRaceParser.Parse($@"{{ ""track"": ""T"", ""finishes"": [ {finishes} ] }}");
        Assert.Equal($"Zu viele Fahrer ({TelemetryRaceParser.MaxFinishes + 1}, max. {TelemetryRaceParser.MaxFinishes}).", result.Error);
    }
}
