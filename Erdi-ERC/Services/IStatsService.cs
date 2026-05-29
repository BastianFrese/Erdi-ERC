namespace <OWNER_HANDLE>_ERC.Services
{
    public interface IStatsService
    {
        Task RebuildLeagueStandingsAsync(string leagueId, CancellationToken cancellationToken = default);
        Task RebuildAllLeagueStandingsAsync(CancellationToken cancellationToken = default);
    }
}
