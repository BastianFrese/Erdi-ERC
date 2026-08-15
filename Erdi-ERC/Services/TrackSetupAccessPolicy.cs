using Erdi_ERC.Models;

namespace Erdi_ERC.Services
{
    public sealed class TrackSetupAccessPolicy : ITrackSetupAccessPolicy
    {
        public bool CanView(TrackSetup setup, int currentTier, string? currentRole)
        {
            if (setup.RequiredAccessTier <= 0) return true;

            // Legacy: setups die früher Tier 2 ("Discord Rolle") erforderten werden
            // jetzt wie Tier 1 (Login + Community-Guild) behandelt.
            var required = setup.RequiredAccessTier == 2 ? 1 : setup.RequiredAccessTier;

            return currentTier >= required;
        }
    }
}
