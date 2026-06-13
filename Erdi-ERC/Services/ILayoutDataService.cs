using <OWNER_HANDLE>_ERC.Models;

namespace <OWNER_HANDLE>_ERC.Services
{
    public interface ILayoutDataService
    {
        Task<LayoutData> GetLayoutDataAsync(CancellationToken cancellationToken = default);
    }

    public sealed record LayoutData(
        string? WinnerTeamKey,
        string WinnerCarPrimary,
        string WinnerCarPrimaryLight,
        string WinnerCarPrimaryDark,
        string WinnerCarSecondary,
        StreamSchedule? ActiveStream,
        DateTime? LatestSetupActivityUtc);
}
