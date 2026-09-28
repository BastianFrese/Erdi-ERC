using Erdi_ERC.Helpers;
using Xunit;

namespace Erdi_ERC.Tests.Helpers;

/// <summary>
/// Regel für den „Ohne Team"-Bucket der Constructors-Wertung. Vorher entstand die Zeile für
/// jede teamlose Standings-Zeile — auch für Fahrer mit 0 Punkten — und stand damit ohne
/// Beitrag in der Tabelle. Ein Bucket MIT Punkten muss dagegen sichtbar bleiben, sonst
/// verschwinden diese Punkte stillschweigend aus der Teamwertung.
/// </summary>
public class ConstructorTeamHelperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LabelFor_emptyTeam_returnsBucketLabel(string? team)
    {
        Assert.Equal(ConstructorTeamHelper.NoTeamLabel, ConstructorTeamHelper.LabelFor(team));
    }

    [Fact]
    public void LabelFor_realTeam_trims()
    {
        Assert.Equal("Ferrari", ConstructorTeamHelper.LabelFor("  Ferrari  "));
    }

    [Theory]
    [InlineData("Ohne Team")]
    [InlineData("ohne team")]
    [InlineData("  OHNE TEAM  ")]
    public void IsNoTeamBucket_isCaseInsensitiveAndTrimmed(string name)
    {
        Assert.True(ConstructorTeamHelper.IsNoTeamBucket(name));
    }

    [Fact]
    public void IsNoTeamBucket_realTeam_false()
    {
        Assert.False(ConstructorTeamHelper.IsNoTeamBucket("Ferrari"));
        Assert.False(ConstructorTeamHelper.IsNoTeamBucket(null));
    }

    [Fact]
    public void IsVisibleConstructor_bucketWithoutPoints_isHidden()
    {
        Assert.False(ConstructorTeamHelper.IsVisibleConstructor(ConstructorTeamHelper.NoTeamLabel, 0));
    }

    [Fact]
    public void IsVisibleConstructor_bucketWithPoints_staysVisible()
    {
        // Kein stiller Punktverlust: sobald im Bucket Punkte liegen, muss er auftauchen.
        Assert.True(ConstructorTeamHelper.IsVisibleConstructor(ConstructorTeamHelper.NoTeamLabel, 21));
    }

    [Fact]
    public void IsVisibleConstructor_realTeamWithoutPoints_staysVisible()
    {
        // Ein echtes Team mit 0 Punkten gehört weiterhin in die Tabelle — nur der
        // Pseudo-Konstrukteur wird unterdrückt.
        Assert.True(ConstructorTeamHelper.IsVisibleConstructor("Ferrari", 0));
    }
}
