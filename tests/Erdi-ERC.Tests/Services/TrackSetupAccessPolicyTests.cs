using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Xunit;

namespace Erdi_ERC.Tests.Services;

/// <summary>
/// Zugriffsregeln der Setups-API: RequiredAccessTier 0 = öffentlich, 1 = Login +
/// Community-Guild, 3–5 = höhere Tiers. Der deprecated Wert 2 wird zur Laufzeit wie 1
/// behandelt. Ein Setup ist sichtbar, wenn der aktuelle Tier >= dem geforderten ist.
/// </summary>
public class TrackSetupAccessPolicyTests
{
    private static readonly TrackSetupAccessPolicy Policy = new();

    private static TrackSetup Setup(int requiredTier) => new() { RequiredAccessTier = requiredTier };

    [Fact]
    public void CanView_publicSetup_visibleForEveryone()
    {
        Assert.True(Policy.CanView(Setup(0), 0, null));
        Assert.True(Policy.CanView(Setup(0), 1, null));
        Assert.True(Policy.CanView(Setup(0), 5, "T3"));
    }

    [Fact]
    public void CanView_tier1Setup_requiresLoginAndGuild()
    {
        Assert.False(Policy.CanView(Setup(1), 0, null));
        Assert.True(Policy.CanView(Setup(1), 1, null));
        Assert.True(Policy.CanView(Setup(1), 3, "T1"));
    }

    [Fact]
    public void CanView_deprecatedTier2_treatedAsTier1()
    {
        // Legacy-Wert 2 ("Discord Rolle") ist deprecated und wird wie 1 behandelt.
        Assert.False(Policy.CanView(Setup(2), 0, null));
        Assert.True(Policy.CanView(Setup(2), 1, null));
    }

    [Fact]
    public void CanView_tier3Setup_requiresTier3OrHigher()
    {
        Assert.False(Policy.CanView(Setup(3), 1, null));
        Assert.False(Policy.CanView(Setup(3), 2, null));
        Assert.True(Policy.CanView(Setup(3), 3, "T1"));
        Assert.True(Policy.CanView(Setup(3), 5, "T3"));
    }

    [Fact]
    public void CanView_tier5Setup_onlyVisibleForTier5()
    {
        Assert.False(Policy.CanView(Setup(5), 3, "T1"));
        Assert.False(Policy.CanView(Setup(5), 4, "T2"));
        Assert.True(Policy.CanView(Setup(5), 5, "T3"));
    }
}
