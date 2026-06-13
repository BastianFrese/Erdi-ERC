using <OWNER_HANDLE>_ERC.Models;

namespace <OWNER_HANDLE>_ERC.Tests;

/// <summary>
/// Sichert <see cref="SetupGameSpec.ReadCardData"/> ab. Diese Methode wurde eingeführt,
/// um die Setup-Karte mit EINEM Parse-Durchlauf zu rendern (statt vier separaten
/// JsonDocument.Parse-Aufrufen) und MUSS verhaltensgleich zu den ursprünglichen
/// Lese-Methoden bleiben — genau das prüfen die Äquivalenz-Tests unten.
/// </summary>
public class SetupGameSpecReadCardDataTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("[1,2,3]")]      // Array-Wurzel, kein Objekt
    [InlineData("\"just a string\"")]
    public void ReadCardData_InvalidOrEmpty_ReturnsDefaults(string? raw)
    {
        var data = SetupGameSpec.ReadCardData(raw);

        Assert.False(data.HasPayload);
        Assert.Equal(1, data.Version);
        Assert.Null(data.StrategyKey);
        Assert.Null(data.TrackKey);
        Assert.Null(data.LengthKey);
        Assert.Null(data.StrategyPlan);
    }

    [Fact]
    public void ReadCardData_CategoryWithAtLeastOneValue_HasPayloadTrue()
    {
        var raw = """{ "categories": { "aerodynamics": { "frontWing": 10 } } }""";

        Assert.True(SetupGameSpec.ReadCardData(raw).HasPayload);
    }

    [Theory]
    [InlineData("""{ "categories": {} }""")]                    // categories leer
    [InlineData("""{ "categories": { "aero": {} } }""")]        // Kategorie ohne Werte
    [InlineData("""{ "aero": { "frontWing": 10 } }""")]         // kein "categories"-Wrapper -> wie alte View-Logik: false
    [InlineData("""{ "version": 2 }""")]                         // gar keine categories
    public void ReadCardData_NoUsableCategories_HasPayloadFalse(string raw)
    {
        Assert.False(SetupGameSpec.ReadCardData(raw).HasPayload);
    }

    [Theory]
    [InlineData("""{ "version": 5, "categories": { "a": { "x": 1 } } }""", 5)]
    [InlineData("""{ "categories": { "a": { "x": 1 } } }""", 1)]            // fehlende Version -> 1
    [InlineData("""{ "version": 0, "categories": { "a": { "x": 1 } } }""", 1)] // nicht-positiv -> 1
    [InlineData("""{ "version": -3 }""", 1)]
    public void ReadCardData_Version_FollowsRules(string raw, int expectedVersion)
    {
        Assert.Equal(expectedVersion, SetupGameSpec.ReadCardData(raw).Version);
    }

    [Fact]
    public void ReadCardData_ReadsStrategyContextFields()
    {
        var raw = """
            { "trackKey": "monza", "lengthKey": "full", "strategyPlan": "Soft -> Hard, Box Lap 18",
              "categories": { "a": { "x": 1 } } }
            """;

        var data = SetupGameSpec.ReadCardData(raw);

        Assert.Equal("monza", data.TrackKey);
        Assert.Equal("full", data.LengthKey);
        Assert.Equal("Soft -> Hard, Box Lap 18", data.StrategyPlan);
    }

    [Fact]
    public void ReadCardData_KnownStrategyKey_IsPreserved()
    {
        var knownKey = SetupGameSpec.GetEditorConfig().RaceStrategies.FirstOrDefault()?.Key;
        if (knownKey is null) return; // keine Strategien konfiguriert -> nichts abzusichern

        var raw = $$"""{ "strategy": "{{knownKey}}", "categories": { "a": { "x": 1 } } }""";

        Assert.Equal(knownKey, SetupGameSpec.ReadCardData(raw).StrategyKey);
    }

    [Fact]
    public void ReadCardData_UnknownStrategyKey_IsNull()
    {
        var raw = """{ "strategy": "definitely-not-a-real-strategy-xyz", "categories": { "a": { "x": 1 } } }""";

        Assert.Null(SetupGameSpec.ReadCardData(raw).StrategyKey);
    }

    public static IEnumerable<object?[]> EquivalencePayloads()
    {
        yield return new object?[] { """{ "version": 3, "trackKey": "monza", "lengthKey": "full", "strategyPlan": "Box Lap 18", "categories": { "a": { "x": 1 } } }""" };
        yield return new object?[] { """{ "categories": { "a": { "x": 1 } } }""" };
        yield return new object?[] { """{ "version": 2 }""" };
        yield return new object?[] { """{ "strategy": "irgendwas", "trackKey": "spa" }""" };
        yield return new object?[] { "not json" };
        yield return new object?[] { "" };
    }

    /// <summary>
    /// Kernabsicherung des Refactors: ReadCardData muss exakt dieselben Werte liefern wie
    /// die drei öffentlichen Methoden, die es zusammenfasst.
    /// </summary>
    [Theory]
    [MemberData(nameof(EquivalencePayloads))]
    public void ReadCardData_IsEquivalentToLegacyReadMethods(string? raw)
    {
        var data = SetupGameSpec.ReadCardData(raw);
        var ctx = SetupGameSpec.ReadStrategyContext(raw);

        Assert.Equal(SetupGameSpec.ReadPayloadVersion(raw), data.Version);
        Assert.Equal(SetupGameSpec.ReadStrategyKey(raw), data.StrategyKey);
        Assert.Equal(ctx.TrackKey, data.TrackKey);
        Assert.Equal(ctx.LengthKey, data.LengthKey);
        Assert.Equal(ctx.StrategyPlan, data.StrategyPlan);
    }
}
