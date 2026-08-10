using <OWNER_HANDLE>_ERC.Models;

namespace <OWNER_HANDLE>_ERC.Services
{
    /// <summary>
    /// Liest den nächsten (oder aktuell laufenden) Stream-Termin aus der DB.
    /// Wird von Home, Admin-Index und anderen Stellen genutzt, damit die
    /// Logik nur an einer Stelle lebt und alle Aufrufer denselben UTC-Bezug verwenden.
    /// </summary>
    public interface IStreamScheduleQueryService
    {
        Task<StreamSchedule?> GetNextStreamScheduleAsync(CancellationToken cancellationToken = default);
    }
}
