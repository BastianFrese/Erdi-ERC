namespace <OWNER_HANDLE>_ERC.Services
{
    public interface IAdminAuditService
    {
        Task LogAsync(string action, string entityType, string entityId, string details);
    }
}
