using Erdi_ERC.Services;

namespace Erdi_ERC.Tests;

/// <summary>
/// Sichert <see cref="RaceCsvParser"/> ab — den CSV-Import des F1-Spiel-Exports beim
/// Renn-Ergebnis-Eintragen. Kernanforderungen:
/// — Positionen, Fahrer und Gesamtzeiten werden automatisch befüllt.
/// — Gesamtzeit = Siegerzeit + Gap zum Vordermann (Komma = Dezimaltrenner).
/// — "Spieler:in"-Platzhalter bleiben erhalten; echte Namen unangetastet.
/// — Überrundet ("+ 1 Runde") → kein berechenbarer Wert, LappedText wird geliefert.
/// — DNF wird erkannt (Position 0-Semantik bleibt in der View am Controller hängen).
/// </summary>
public class RaceCsvParserTests
{
    // Exakter deutscher Export des Users (22 Autos).
    private const string UserSample = """
        "Pos.","Fahrer","Team","Grid","Stopps","Beste","Zeit","Pkt.","Fahrertyp"
        "1","Spieler:in","Mercedes-AMG F1 Team","1","1","1:20,126","45:31,798","25","Spieler:in"
        "2","Spieler:in","Atlassian Williams F1 Team","11","1","1:20,251","+12,423","18","Spieler:in"
        "3","Spieler:in","Scuderia Ferrari HP","3","1","1:20,199","+13,740","15","Spieler:in"
        "4","Spieler:in","Aston Martin Aramco","6","1","1:20,428","+16,636","12","Spieler:in"
        "5","Spieler:in","Audi Revolut F1 Team","12","1","1:20,691","+22,676","10","Spieler:in"
        "6","Spieler:in","Mercedes-AMG F1 Team","15","1","1:20,375","+23,748","8","Spieler:in"
        "7","Spieler:in","Visa Cash App Racing Bulls","8","1","1:19,712","+27,805","6","Spieler:in"
        "8","Spieler:in","Alpine","2","2","1:17,732","+29,763","4","Spieler:in"
        "9","Spieler:in","Cadillac Formula 1® Team","5","2","1:20,125","+33,527","2","Spieler:in"
        "10","Spieler:in","Atlassian Williams F1 Team","16","1","1:21,486","+43,996","1","Spieler:in"
        "11","Spieler:in","McLaren","14","2","1:20,440","+50,301","0","Spieler:in"
        "12","Spieler:in","Haas","7","1","1:18,890","+51,341","0","Spieler:in"
        "13","Spieler:in","Audi Revolut F1 Team","9","1","1:19,236","+51,773","0","Spieler:in"
        "14","Spieler:in","Scuderia Ferrari HP","10","1","1:19,649","+1:02,309","0","Spieler:in"
        "15","Spieler:in","Haas","18","3","1:20,456","+1:02,531","0","Spieler:in"
        "16","Spieler:in","Visa Cash App Racing Bulls","4","3","1:19,140","+1:04,139","0","Spieler:in"
        "17","Spieler:in","McLaren","22","2","1:22,158","+1:17,620","0","Spieler:in"
        "18","Spieler:in","Alpine","21","2","1:21,681","+1:22,884","0","Spieler:in"
        "19","Spieler:in","Aston Martin Aramco","17","3","1:20,183","+ 1 Runde","0","Spieler:in"
        "20","ERC_X_Sani23_X","Oracle Red Bull Racing","20","1","1:20,699","+ 1 Runde","0","Spieler:in"
        "21","Spieler:in","Oracle Red Bull Racing","13","5","1:20,639","DNF","0","Spieler:in"
        "22","Spieler:in","Cadillac Formula 1® Team","19","0","1:20,528","DNF","0","Spieler:in"
        """;

    // Exakter Semicolon-Export der ERDi-Telemetrie-App (ResultsExporter.WriteCsv) — 21 Autos.
    private const string TelemetrySample = """
        position;name;team;raceNumber;numLaps;gridPosition;points;resultStatus;bestLapMs;totalRaceSeconds;penaltiesTime;numPenalties
        1;ERC|MarvinSCP10;RacingBulls;8;36;1;25;Finished;72119;2670.938232421875;0;0
        2;leanderw041490;RacingBulls;37;36;4;18;Finished;72594;2675.861572265625;0;0
        3;ERC|Max.;Audi;98;36;3;15;Finished;72683;2685.425537109375;0;0
        4;ShadowxB0rn;RedBullRacing;76;36;2;12;Finished;72723;2686.2265625;0;0
        5;Car 19;Mercedes;0;36;7;10;Finished;72108;2687.197021484375;0;0
        6;ERC|LUKS;Williams;99;36;8;8;Finished;72178;2680.616455078125;10;1
        7;Car 21;Mercedes;0;36;6;6;Finished;72719;2693.137939453125;0;0
        8;Nilos23;AstonMartin;93;36;9;4;Finished;72523;2700.711669921875;0;0
        9;ERCIOlli;Mercedes;64;36;12;2;Finished;72583;2700.798828125;0;0
        10;Car 20;Mercedes;0;36;17;1;Finished;72996;2691.684326171875;10;1
        11;ERC|Erdi/10/*MOZA;Audi;94;36;15;0;Finished;72813;2704.017578125;0;0
        12;ERC|2High2Fly2U;Williams;2;36;10;0;Finished;72457;2705.25048828125;0;0
        13;ERC|Sebeck;Mercedes;9;36;5;0;Finished;72316;2708.974609375;0;0
        14;TheRealLionsfan;Haas;21;36;16;0;Finished;72878;2714.43994140625;0;0
        15;[NXS]MaiksF1Channel;Ferrari;25;36;11;0;Finished;73225;2717.812255859375;0;0
        16;Spieler:in;Haas;25;36;14;0;Finished;72783;2726.424072265625;0;0
        17;ERC|holymoly;Cadillac;9;35;20;0;Finished;72332;2695.289306640625;0;0
        18;ERC|BluePeda;Cadillac;70;35;18;0;Finished;72061;2688.421142578125;10;1
        19;ERC|XIMagic;Alpine;42;22;13;0;DidNotFinish;72769;1726.538330078125;0;0
        20;ERC|Trigger;AstonMartin;66;17;21;0;DidNotFinish;75305;1349.9530029296875;3;1
        21;ERC|DennisLN1;McLaren;39;6;19;0;DidNotFinish;73348;490.17242431640625;0;0
        """;

    [Fact]
    public void Parse_UserSample_ReturnsAllTwentyTwoEntries_InCsvOrder()
    {
        var result = RaceCsvParser.Parse(UserSample);

        Assert.Null(result.Error);
        Assert.Equal(1, result.SkippedLines); // die Kopfzeile wird gezählt
        Assert.Equal(22, result.Entries.Count);
        Assert.Equal(1, result.Entries[0].Position);
        Assert.Equal(22, result.Entries[21].Position);
        Assert.Equal("ERC_X_Sani23_X", result.Entries[19].Driver); // echter Name bleibt erhalten
        Assert.Equal("Oracle Red Bull Racing", result.Entries[19].Team);
    }

    [Theory]
    [InlineData(1, 2731798)]   // 45:31,798  = Siegerzeit (Basis)
    [InlineData(2, 2744221)]   // +12,423    → 45:44,221
    [InlineData(3, 2745538)]   // +13,740    → 45:45,538
    [InlineData(8, 2761561)]   // +29,763    → 46:01,561
    [InlineData(10, 2775794)]  // +43,996    → 46:15,794
    [InlineData(14, 2794107)]  // +1:02,309  → 46:34,107
    [InlineData(18, 2814682)]  // +1:22,884  → 46:54,682
    public void Parse_UserSample_GapResultsInTotalTime(int position, int expectedMs)
    {
        var result = RaceCsvParser.Parse(UserSample);
        var entry = result.Entries.Single(e => e.Position == position);

        Assert.NotNull(entry.TotalTimeMs);
        Assert.InRange(entry.TotalTimeMs!.Value, expectedMs - 1, expectedMs + 1);
    }

    [Theory]
    [InlineData(19, 18)]  // überrundet
    [InlineData(20, 19)]  // überrundet (echter Name)
    public void Parse_UserSample_LappedDrivers_HaveNoComputableTime(int position, int index)
    {
        var result = RaceCsvParser.Parse(UserSample);
        var entry = result.Entries[index];

        Assert.Equal(position, entry.Position);
        Assert.Null(entry.TotalTimeMs);
        Assert.False(entry.IsDnf);
    }

    [Fact]
    public void Parse_LappedRows_ProduceNormalizedGermanLapText()
    {
        var result = RaceCsvParser.Parse("""
            "Pos.","Fahrer","Team","Grid","Stopps","Beste","Zeit","Pkt.","Fahrertyp"
            "1","A","T1","1","1","1:20,126","45:31,798","25","Spieler:in"
            "2","B","T2","2","1","1:20,251","+ 1 Runde","18","Spieler:in"
            "3","C","T3","3","1","1:21,486","+ 2 Runden","15","Spieler:in"
            "4","D","T4","4","1","1:20,440","+3 Runden","12","Spieler:in"
            """);

        Assert.Equal("+1 Runde", result.Entries[1].LappedText);
        Assert.Equal("+2 Runden", result.Entries[2].LappedText);
        Assert.Equal("+3 Runden", result.Entries[3].LappedText);
        Assert.Null(result.Entries[2].TotalTimeMs);
    }

    [Fact]
    public void Parse_DnfRows_AreFlaggedAndHaveNoTime()
    {
        var result = RaceCsvParser.Parse("""
            "Pos.","Fahrer","Team","Grid","Stopps","Beste","Zeit","Pkt.","Fahrertyp"
            "1","Gewinner","Aston Martin Aramco","1","1","1:20,126","45:31,798","25","Spieler:in"
            "2","Ausfall","Oracle Red Bull Racing","3","5","1:20,639","DNF","0","Spieler:in"
            """);

        Assert.Equal(2, result.Entries.Count);
        Assert.True(result.Entries[1].IsDnf);
        Assert.Null(result.Entries[1].TotalTimeMs);
        Assert.Null(result.Entries[1].LappedText);
        Assert.False(result.Entries[0].IsDnf);
    }

    [Fact]
    public void Parse_GapAsBareSeconds_IsAddedToLeaderTime()
    {
        var result = RaceCsvParser.Parse("""
            Pos.,Fahrer,Team,Grid,Stopps,Beste,Zeit,Pkt.,Fahrertyp
            1,A,T1,1,1,1:20.126,45:31.798,25,Spieler:in
            2,B,T2,2,1,1:20.251,+12.423,18,Spieler:in
            """);

        Assert.Equal(2731798, result.Entries[0].TotalTimeMs);
        // 45:31,798 + 12,423 s = 45:44,221 → 2744221 ms
        Assert.InRange(result.Entries[1].TotalTimeMs!.Value, 2744220, 2744222);
    }

    [Fact]
    public void Parse_TotalTimeOverOneHour_UsesHmsFormat()
    {
        var result = RaceCsvParser.Parse("""
            "Pos.","Fahrer","Team","Grid","Stopps","Beste","Zeit","Pkt.","Fahrertyp"
            "1","Lang","T1","1","1","1:20,126","1:24:35,100","25","Spieler:in"
            "2","Zweit","T2","2","1","1:20,251","+13,740","18","Spieler:in"
            """);

        // 1:24:35,100 = 5075,1 s → 5075100 ms; +13,740 → 5088840 ms
        Assert.Equal(5075100, result.Entries[0].TotalTimeMs);
        Assert.InRange(result.Entries[1].TotalTimeMs!.Value, 5088839, 5088841);
    }

    [Fact]
    public void Parse_EnglishHeader_IsDetectedAndMapped()
    {
        var result = RaceCsvParser.Parse("""
            "Pos","Driver","Team","Grid","Stops","Best","Time","Pts","Type"
            "1","Alice","Ferrari","1","1","1:20.126","45:31.798","25","Player"
            "2","Bob","Haas","2","1","1:20.251","+12.423","18","Player"
            """);

        Assert.Equal(2, result.Entries.Count);
        Assert.Equal("Alice", result.Entries[0].Driver);
        Assert.Equal("Haas", result.Entries[1].Team);
        Assert.InRange(result.Entries[1].TotalTimeMs!.Value, 2744220, 2744222);
    }

    [Fact]
    public void Parse_NoHeaderLine_FallsBackToFixedIndices()
    {
        var result = RaceCsvParser.Parse("""
            1,Alice,TeamA,1,1,1:20.126,45:31.798,25,Player
            2,Bob,TeamC,2,1,1:20.251,+12.423,18,Player
            """);

        Assert.Equal(2, result.Entries.Count);
        Assert.Equal("Alice", result.Entries[0].Driver);
        Assert.Equal("TeamC", result.Entries[1].Team);
    }

    [Fact]
    public void Parse_SkipsHeaderAndGarbageLines_WithoutError()
    {
        var result = RaceCsvParser.Parse("""
            Das ist ein Kopftitel
            Pos.,Fahrer,Team,Grid,Stopps,Beste,Zeit,Pkt.,Fahrertyp

            1,A,T1,1,1,1:20.126,45:31.798,25,Player
            kaputte Zeile ohne Kommastruktur
            2,B,T2,2,1,1:20.251,+12.423,18,Player
            """);

        Assert.Equal(2, result.Entries.Count);
        Assert.Null(result.Error);
        // Kopfzeile + leere Zeile + Müllzeile werden übersprungen (nicht als Entry).
        Assert.True(result.SkippedLines >= 3);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsError()
    {
        var result = RaceCsvParser.Parse("   \r\n \n  ");

        Assert.NotNull(result.Error);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public void Parse_OnlyHeader_ReturnsNoEntriesButNoError()
    {
        var result = RaceCsvParser.Parse("\"Pos.\",\"Fahrer\",\"Team\",\"Grid\",\"Stopps\",\"Beste\",\"Zeit\",\"Pkt.\",\"Fahrertyp\"");

        Assert.Null(result.Error);
        Assert.Empty(result.Entries);
        Assert.Equal(1, result.SkippedLines);
    }

    [Fact]
    public void Parse_DriverNames_AreTrimmedNotAltered()
    {
        var result = RaceCsvParser.Parse("""
            "Pos.","Fahrer","Team","Grid","Stopps","Beste","Zeit","Pkt.","Fahrertyp"
            "1","  Max Mustermann  ","T1","1","1","1:20,126","45:31,798","25","Spieler:in"
            """);

        Assert.Equal("Max Mustermann", result.Entries[0].Driver);
    }

    [Fact]
    public void Parse_PreambleWithSevenOrMoreFields_DoesNotConsumeRealHeader()
    {
        // Preambel-Zeile mit ≥7 Feldern ohne bekannte Header-Tokens darf NICHT als
        // Kopfzeile verbraucht werden — sonst fallen die Indizes auf Defaults und die
        // Fahrer-Spalte würde falsch gemappt (Fahrer stünde in Spalte 1 statt 2).
        var result = RaceCsvParser.Parse("""
            Das ist ein Kopftitel ohne Header-Felder: aaaa bbbb cccc dddd eeee ffff gggg
            "Pos.","Fahrer","Team","Grid","Stopps","Beste","Zeit","Pkt.","Fahrertyp"
            "1","A","T1","1","1","1:20,126","45:31,798","25","Spieler:in"
            "2","Bob","T2","2","1","1:20,251","+12,423","18","Spieler:in"
            """);

        Assert.Equal(2, result.Entries.Count);
        Assert.Equal("A", result.Entries[0].Driver);
        Assert.Equal("Bob", result.Entries[1].Driver);
        Assert.Equal("T2", result.Entries[1].Team);
        Assert.InRange(result.Entries[1].TotalTimeMs!.Value, 2744220, 2744222);
        Assert.True(result.SkippedLines >= 2); // Titelzeile + echte Kopfzeile
    }

    // ── ERDi-Telemetrie-Export (semicolon-getrennt) ─────────────────────────────

    [Fact]
    public void Parse_TelemetrySample_ReturnsAllEntriesInOrder()
    {
        var result = RaceCsvParser.Parse(TelemetrySample);

        Assert.Null(result.Error);
        Assert.Equal(1, result.SkippedLines); // nur die Kopfzeile
        Assert.Equal(21, result.Entries.Count);
        Assert.Equal(1, result.Entries[0].Position);
        Assert.Equal(21, result.Entries[20].Position);
        Assert.Equal("ERC|MarvinSCP10", result.Entries[0].Driver);
        Assert.Equal("RacingBulls", result.Entries[0].Team);
        Assert.Equal("ERC|Erdi/10/*MOZA", result.Entries[10].Driver); // Name mit Sonderzeichen bleibt erhalten
        Assert.Equal("Audi", result.Entries[10].Team);
    }

    [Theory]
    [InlineData(0, 2670937, 2670939)]              // Sieger raw 2670.938 s → 2 670 938 ms
    [InlineData(5, 2690615, 2690617)]              // LUKS raw 2680.616 s + 10 s Strafe → 2 690 616 ms
    [InlineData(4, 2687196, 2687198)]              // Car 19 ohne Strafe → 2 687 197 ms
    public void Parse_Telemetry_TotalTimeIncludesPenaltySeconds(int index, int expectedMin, int expectedMax)
    {
        var result = RaceCsvParser.Parse(TelemetrySample);
        var entry = result.Entries[index];

        Assert.False(entry.IsDnf);
        Assert.NotNull(entry.TotalTimeMs);
        Assert.InRange(entry.TotalTimeMs!.Value, expectedMin, expectedMax);
    }

    [Fact]
    public void Parse_Telemetry_DnfIsDetectedFromResultStatus()
    {
        var result = RaceCsvParser.Parse(TelemetrySample);

        Assert.False(result.Entries[0].IsDnf);
        // XIMagic (pos 19), Trigger (pos 20), DennisLN1 (pos 21) — alle DidNotFinish.
        for (var i = 18; i <= 20; i++)
        {
            Assert.True(result.Entries[i].IsDnf);
            Assert.Null(result.Entries[i].TotalTimeMs);
        }
    }

    [Theory]
    [InlineData(1, 1)]   // Marvin von der Pole
    [InlineData(4, 2)]   // ShadowxB0rn grid 2
    [InlineData(18, 18)] // BluePeda grid 18
    public void Parse_Telemetry_GridPositionMapsToQualifyingPosition(int position, int expectedGrid)
    {
        var result = RaceCsvParser.Parse(TelemetrySample);
        var entry = result.Entries.Single(e => e.Position == position);

        Assert.Equal(expectedGrid, entry.QualifyingPosition);
    }

    [Fact]
    public void Parse_Telemetry_FastestLapComesFromBestLapMsExcludingDnf()
    {
        var result = RaceCsvParser.Parse(TelemetrySample);

        // Minimum bestLapMs der klassifizierten Fahrer = 72061 → ERC|BluePeda (pos 18).
        Assert.Equal("ERC|BluePeda", result.FastestLapDriver);
    }

    [Fact]
    public void Parse_Telemetry_DisqualifiedCountsAsDnf()
    {
        var result = RaceCsvParser.Parse("""
            position;name;team;raceNumber;numLaps;gridPosition;points;resultStatus;bestLapMs;totalRaceSeconds;penaltiesTime;numPenalties
            1;Alice;McLaren;81;44;1;25;Finished;90123;3600.5;0;0
            2;Bob;Mercedes;16;44;3;18;Disqualified;0;3580.2;5;1
            """);

        Assert.True(result.Entries[1].IsDnf);
        Assert.False(result.Entries[0].IsDnf);
        Assert.Equal("Alice", result.FastestLapDriver);
    }

    [Fact]
    public void Parse_Telemetry_NameWithSemicolon_IsParsedAsSingleField()
    {
        var result = RaceCsvParser.Parse("""
            position;name;team;raceNumber;numLaps;gridPosition;points;resultStatus;bestLapMs;totalRaceSeconds;penaltiesTime;numPenalties
            1;"Player;One";McLaren;81;44;2;25;Finished;90123;3600.5;0;0
            """);

        var entry = Assert.Single(result.Entries);
        Assert.Equal("Player;One", entry.Driver); // Semikolon im Namen ist KEIN Trennzeichen
        Assert.Equal("McLaren", entry.Team);
        Assert.Equal(2, entry.QualifyingPosition);
        Assert.InRange(entry.TotalTimeMs!.Value, 3600499, 3600501);
    }

    [Fact]
    public void Parse_Telemetry_WithoutHeader_FallsBackToFixedIndexOrder()
    {
        var result = RaceCsvParser.Parse("""
            1;Alice;McLaren;81;44;2;25;Finished;90123;3600.5;0;0
            2;Bob;Mercedes;1;44;3;18;Finished;91234;3611.0;0;0
            3;Carol;Ferrari;55;43;1;15;DidNotFinish;93000;2000.0;0;0
            """);

        Assert.Equal(3, result.Entries.Count);
        Assert.Equal("Alice", result.Entries[0].Driver);
        Assert.Equal("Mercedes", result.Entries[1].Team);
        Assert.Equal(2, result.Entries[0].QualifyingPosition);
        Assert.True(result.Entries[2].IsDnf);
        Assert.Equal("Alice", result.FastestLapDriver);
    }

    [Fact]
    public void Parse_Telemetry_WithoutHeader_PreambleLineIsSkipped()
    {
        // Eine Titel-/Preambel-Zeile vor headerlosen Telemetrie-Daten darf die
        // Format-Erkennung nicht abbrechen (Fallback: Zeilen <9 Felder werden übersprungen).
        var result = RaceCsvParser.Parse("""
            Meisterschaft Runde 1 - Export
            1;Alice;McLaren;81;44;2;25;Finished;90123;3600.5;0;0
            2;Bob;Mercedes;1;44;3;18;Finished;91234;3611.0;0;0
            """);

        Assert.Equal(2, result.Entries.Count);
        Assert.Equal("Alice", result.Entries[0].Driver);
        Assert.Equal(2, result.Entries[0].QualifyingPosition);
        Assert.False(result.Entries[1].IsDnf);
    }

    // ── Rennabbruch: Distanz für die Faktor-Ableitung ───────────────────────────

    [Fact]
    public void Parse_TelemetrySample_ReportsDrivenLapsButNoTargetDistance()
    {
        // Der heutige Exporter führt numLaps, aber (noch) keine Soll-Distanz mit.
        // Es darf deshalb KEINE Verkürzung abgeleitet werden — das Rennen bleibt voll.
        var result = RaceCsvParser.Parse(TelemetrySample);

        Assert.Equal(36, result.CompletedLaps); // Maximum der gewerteten Fahrer
        Assert.Null(result.TotalLaps);          // Spalte fehlt in diesem Export
        Assert.Equal(Erdi_ERC.Helpers.RacePointsFactor.Full,
            Erdi_ERC.Helpers.RacePointsFactor.DeriveFromDistance(result.TotalLaps, result.CompletedLaps));
    }

    [Fact]
    public void Parse_TelemetryWithTotalLapsColumn_ReportsBothDistances()
    {
        // Abgebrochenes Rennen: 31 von 44 Runden → 50 % (F1-analoge Schwelle).
        var result = RaceCsvParser.Parse("""
            position;name;team;raceNumber;numLaps;gridPosition;points;resultStatus;bestLapMs;totalRaceSeconds;penaltiesTime;numPenalties;totalLaps
            1;Alice;McLaren;81;31;1;25;Finished;90123;1800.5;0;0;44
            2;Bob;Mercedes;1;31;3;18;Finished;91234;1811.0;0;0;44
            3;Carol;Ferrari;55;28;2;15;DidNotFinish;93000;1500.0;0;0;44
            """);

        Assert.Equal(44, result.TotalLaps);
        Assert.Equal(31, result.CompletedLaps); // Carols 28 Runden zählen nicht (DNF)
        Assert.Equal(Erdi_ERC.Helpers.RacePointsFactor.Half,
            Erdi_ERC.Helpers.RacePointsFactor.DeriveFromDistance(result.TotalLaps, result.CompletedLaps));
    }

    [Fact]
    public void Parse_TelemetryWithTotalLapsColumn_HeaderOrderDoesNotMatter()
    {
        // Die Spalten werden über die Kopfzeile gemappt — totalLaps darf überall stehen.
        var result = RaceCsvParser.Parse("""
            totalLaps;position;name;team;raceNumber;numLaps;gridPosition;points;resultStatus;bestLapMs;totalRaceSeconds;penaltiesTime;numPenalties
            44;1;Alice;McLaren;81;44;1;25;Finished;90123;3600.5;0;0
            44;2;Bob;Mercedes;1;33;3;18;Finished;91234;3700.0;0;0
            """);

        Assert.Equal(44, result.TotalLaps);
        Assert.Equal(44, result.CompletedLaps);
        Assert.Equal(Erdi_ERC.Helpers.RacePointsFactor.Full,
            Erdi_ERC.Helpers.RacePointsFactor.DeriveFromDistance(result.TotalLaps, result.CompletedLaps));
    }

    [Fact]
    public void Parse_TelemetryWithZeroTotalLaps_ReportsNoTargetDistance()
    {
        // Zeitrennen/Replays schreiben 0 als Soll-Distanz → keine Ableitung (keine 0-Runden-Quote).
        var result = RaceCsvParser.Parse("""
            position;name;team;raceNumber;numLaps;gridPosition;points;resultStatus;bestLapMs;totalRaceSeconds;penaltiesTime;numPenalties;totalLaps
            1;Alice;McLaren;81;20;1;25;Finished;90123;1800.5;0;0;0
            2;Bob;Mercedes;1;19;3;18;Finished;91234;1811.0;0;0;0
            """);

        Assert.Null(result.TotalLaps);
        Assert.Equal(20, result.CompletedLaps);
        Assert.Equal(Erdi_ERC.Helpers.RacePointsFactor.Full,
            Erdi_ERC.Helpers.RacePointsFactor.DeriveFromDistance(result.TotalLaps, result.CompletedLaps));
    }

    [Fact]
    public void Parse_GameExport_HasNoLapDistance()
    {
        // Der F1-Spiel-Export kennt keine Runden-Spalte → manuelle Faktor-Wahl bleibt.
        var result = RaceCsvParser.Parse(UserSample);

        Assert.Null(result.TotalLaps);
        Assert.Null(result.CompletedLaps);
    }
}
