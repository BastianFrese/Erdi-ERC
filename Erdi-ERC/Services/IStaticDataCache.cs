using <OWNER_HANDLE>_ERC.Models;

namespace <OWNER_HANDLE>_ERC.Services
{
    /// <summary>
    /// Cache für selten ändernde DB-Lookups, die auf vielen Seiten gelesen werden.
    /// </summary>
    public interface IStaticDataCache
    {
        Task<IReadOnlyList<AchievementDefinition>> GetActiveAchievementDefinitionsAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<League>> GetAllLeaguesAsync(CancellationToken cancellationToken = default);

        void InvalidateAchievementDefinitions();
        void InvalidateLeagues();
    }
}
