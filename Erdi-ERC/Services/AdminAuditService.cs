using Erdi_ERC.Data;
using Erdi_ERC.Models;

namespace Erdi_ERC.Services
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
            // in same transaction as the operation being audited. Use LogAndSaveAsync
            // when the audited operation has already been committed.
        }

        public async Task LogAndSaveAsync(string action, string entityType, string entityId, string details)
        {
            await LogAsync(action, entityType, entityId, details);
            await _db.SaveChangesAsync();
        }
    }
}
