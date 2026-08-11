using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Options;
using <OWNER_HANDLE>_ERC.Services;
using <OWNER_HANDLE>_ERC.Tests.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace <OWNER_HANDLE>_ERC.Tests.Services;

/// <summary>
/// Tests für <see cref="DriverProfileService.ResolveAsync"/>: liefert ExactMatch,
/// ResolvedName (kanonisch) und Suggestions-Liste (fuzzy, sortiert nach Levenshtein).
/// Wird vom AdminLeagueController in TryValidateGuestAssignmentsAsync genutzt,
/// um Gastfahrer-Namen gegen Tippfehler und halluzinierte Eingaben abzusichern.
/// </summary>
public class DriverProfileServiceResolveTests
{
    private static DriverProfileService CreateService(AppDbContext db)
        => new(db, OptionsFactory.Create(new DriverMatchingOptions
        {
            MinQueryLength = 2,
            MaxLevenshteinDistance = 2,
            MaxSuggestions = 5,
        }));

    private static DriverProfile SeedProfile(
        AppDbContext db,
        string discordId,
        string displayName,
        params (string Platform, string Tag)[] tags)
    {
        var profile = new DriverProfile
        {
            DiscordId = discordId,
            DiscordName = $"{displayName}***REMOVED***0001",
            DisplayName = displayName,
            PreferredPlatform = "EA",
            GamerTags = tags.Select(t => new DriverGamerTag
            {
                DiscordId = discordId,
                Platform = t.Platform,
                GamerTag = t.Tag,
                IsPrimary = t.Tag == t.Platform
            }).ToList()
        };
        db.DriverProfiles.Add(profile);
        db.SaveChanges();
        return profile;
    }

    [Fact]
    public async Task ResolveAsync_knownExactMatch_returnsExactMatch()
    {
        using var ctx = new SqliteTestContext();
        SeedProfile(ctx.Db, "d1", "Mick Schumacher", ("EA", "Mick Schumacher"));

        var svc = CreateService(ctx.Db);
        var match = await svc.ResolveAsync("Mick Schumacher");

        Assert.True(match.ExactMatch);
        Assert.Equal("Mick Schumacher", match.ResolvedName);
        Assert.Equal("d1", match.DiscordId);
    }

    [Fact]
    public async Task ResolveAsync_caseInsensitiveExactMatch_returnsExactMatch()
    {
        using var ctx = new SqliteTestContext();
        SeedProfile(ctx.Db, "d1", "Mick Schumacher", ("EA", "Mick Schumacher"));

        var svc = CreateService(ctx.Db);
        var match = await svc.ResolveAsync("mick schumacher");

        Assert.True(match.ExactMatch);
        Assert.Equal("Mick Schumacher", match.ResolvedName);
    }

    [Fact]
    public async Task ResolveAsync_unknownName_returnsNoMatchWithSuggestions()
    {
        using var ctx = new SqliteTestContext();
        SeedProfile(ctx.Db, "d1", "Mick Schumacher", ("EA", "Mick Schumacher"));

        var svc = CreateService(ctx.Db);
        var match = await svc.ResolveAsync("Hans Müller");

        Assert.False(match.ExactMatch);
        Assert.Equal("Hans Müller", match.ResolvedName);
        Assert.NotNull(match.Suggestions);
        // "Hans Müller" hat Distanz > 2 zu "Mick Schumacher" -> keine Suggestion.
        Assert.Empty(match.Suggestions);
    }

    [Fact]
    public async Task ResolveAsync_typoWithLevenshteinOne_returnsMatchWithSuggestion()
    {
        using var ctx = new SqliteTestContext();
        SeedProfile(ctx.Db, "d1", "Mick Schumacher", ("EA", "Mick Schumacher"));

        var svc = CreateService(ctx.Db);
        var match = await svc.ResolveAsync("Mick Schumaher");

        Assert.False(match.ExactMatch); // nicht exakt -> Gate lehnt ab
        Assert.NotEmpty(match.Suggestions);
        Assert.Equal("Mick Schumacher", match.Suggestions[0].GamerTag);
    }

    [Fact]
    public async Task ResolveAsync_emptyInput_returnsEmptyMatch()
    {
        using var ctx = new SqliteTestContext();
        SeedProfile(ctx.Db, "d1", "Mick Schumacher", ("EA", "Mick Schumacher"));

        var svc = CreateService(ctx.Db);
        var match = await svc.ResolveAsync("");

        Assert.False(match.ExactMatch);
        Assert.Equal(string.Empty, match.ResolvedName);
        Assert.Null(match.DiscordId);
    }

    [Fact]
    public async Task ResolveAsync_whitespaceInput_returnsEmptyMatch()
    {
        using var ctx = new SqliteTestContext();
        SeedProfile(ctx.Db, "d1", "Mick Schumacher", ("EA", "Mick Schumacher"));

        var svc = CreateService(ctx.Db);
        var match = await svc.ResolveAsync("   ");

        Assert.False(match.ExactMatch);
        Assert.Equal(string.Empty, match.ResolvedName);
    }

    [Fact]
    public async Task ResolveAsync_inputShorterThanMinQueryLength_returnsEmptyMatch()
    {
        using var ctx = new SqliteTestContext();
        SeedProfile(ctx.Db, "d1", "Mick Schumacher", ("EA", "Mick Schumacher"));

        var svc = CreateService(ctx.Db);
        var match = await svc.ResolveAsync("M");

        Assert.False(match.ExactMatch);
        Assert.Equal("M", match.ResolvedName);
        Assert.Empty(match.Suggestions);
    }

    [Fact]
    public async Task ResolveAsync_resolvesViaAnyGamerTag_notOnlyPreferred()
    {
        // Ein Profil mit mehreren Tags (EA + Steam) muss ueber jeden Tag auffindbar sein.
        using var ctx = new SqliteTestContext();
        SeedProfile(ctx.Db, "d1", "DisplayName",
            ("EA", "EaHandle"),
            ("Steam", "SteamHandle"));

        var svc = CreateService(ctx.Db);
        var matchViaSteam = await svc.ResolveAsync("SteamHandle");

        Assert.True(matchViaSteam.ExactMatch);
        Assert.Equal("SteamHandle", matchViaSteam.ResolvedName);
        Assert.Equal("d1", matchViaSteam.DiscordId);
    }
}