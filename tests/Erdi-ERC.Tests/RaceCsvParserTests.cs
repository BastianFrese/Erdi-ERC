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
}
