using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;

namespace <OWNER_HANDLE>_ERC.Services
{
    public class AdminAuditService : IAdminAuditService
    {
        private readonly AppDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AdminAuditService(AppDbContext db, IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(string action, string entityType, string entityId, string details)
        {
            var actor = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(actor))
                actor = "system";

            _db.Set<AdminAuditLog>().Add(new AdminAuditLog
            {
                Actor = actor,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                CreatedAt = DateTime.UtcNow
            });

            // NOTE: Caller is responsible for SaveChangesAsync() to keep audit log
            // in same transaction as the operation being audited
        }
    }
}
