using Erdi_ERC.Models;

namespace Erdi_ERC.Services
{
    public interface ITrackSetupAccessPolicy
    {
        bool CanView(TrackSetup setup, int currentTier, string? currentRole);
    }
}
