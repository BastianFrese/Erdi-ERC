using <OWNER_HANDLE>_ERC.Models;

namespace <OWNER_HANDLE>_ERC.Services
{
    public interface ITrackSetupAccessPolicy
    {
        bool CanView(TrackSetup setup, int currentTier, string? currentRole);
    }
}
