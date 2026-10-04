using Erdi_ERC.Models;
using Erdi_ERC.Models.UiComponents;

namespace Erdi_ERC.Services
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
        DateTime? LatestSetupActivityUtc,
        /// <summary>Top-N der ersten aktiven Liga. Nie null; bei leerer Wertung ohne Zeilen.</summary>
        TowerVm Tower);
}
