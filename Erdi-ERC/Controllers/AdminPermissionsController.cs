using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace <OWNER_HANDLE>_ERC.Controllers
{
    /// <summary>Admins &amp; Berechtigungen (nur Superadmins).</summary>
    [Authorize(Policy = "Admin")]
    public class AdminPermissionsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IAdminAuditService _audit;

        public AdminPermissionsController(AppDbContext db, IAdminAuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        [HttpGet]
        public async Task<IActionResult> Admins()
        {
            var admins = await _db.AdminUsers
                .Include(x => x.Permissions)
                .OrderBy(x => x.DisplayName)
                .ToListAsync();
            return View("~/Views/Admin/Admins.cshtml", admins);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAdmin(string discordId, string? displayName, bool isSuperAdmin = false)
        {
            if (string.IsNullOrWhiteSpace(discordId))
            {
                TempData["AdminMessage"] = "Discord-Id ist erforderlich.";
                return RedirectToAction(nameof(Admins));
            }

            var trimmedId = discordId.Trim();
            if (!await _db.AdminUsers.AnyAsync(x => x.DiscordId == trimmedId))
            {
                _db.AdminUsers.Add(new AdminUser
                {
                    DiscordId = trimmedId,
                    DisplayName = displayName?.Trim(),
                    AddedAt = DateTime.UtcNow,
                    IsSuperAdmin = isSuperAdmin
                });
                await _db.SaveChangesAsync();
                await _audit.LogAsync("AddAdmin", "AdminUser", trimmedId,
                    $"DisplayName={displayName?.Trim()}, SuperAdmin={isSuperAdmin}");
                TempData["AdminMessage"] = "Admin hinzugefügt.";
            }
            else
            {
                TempData["AdminMessage"] = "Discord-Id ist bereits registriert.";
            }

            return RedirectToAction(nameof(Admins));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAdmin(string discordId, string? displayName, bool isSuperAdmin = false)
        {
            var admin = await _db.AdminUsers.FindAsync(discordId);
            if (admin is null)
            {
                TempData["AdminMessage"] = "Admin nicht gefunden.";
                return RedirectToAction(nameof(Admins));
            }

            admin.DisplayName = displayName?.Trim();
            admin.IsSuperAdmin = isSuperAdmin;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("UpdateAdmin", "AdminUser", discordId,
                $"DisplayName={displayName?.Trim()}, SuperAdmin={isSuperAdmin}");
            TempData["AdminMessage"] = "Admin aktualisiert.";
            return RedirectToAction(nameof(Admins));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPermissions(string discordId, List<string> permissions)
        {
            var admin = await _db.AdminUsers
                .Include(x => x.Permissions)
                .FirstOrDefaultAsync(x => x.DiscordId == discordId);

            if (admin is null)
            {
                TempData["AdminMessage"] = "Admin nicht gefunden.";
                return RedirectToAction(nameof(Admins));
            }

            // Superadmins brauchen keine expliziten Einzelberechtigungen
            if (admin.IsSuperAdmin)
            {
                TempData["AdminMessage"] = "Superadmins haben automatisch alle Rechte. Einzelne Berechtigungen werden nicht gespeichert.";
                return RedirectToAction(nameof(Admins));
            }

            var validPerms = AdminPermissions.All.Select(p => p.Key).ToHashSet();
            var requested = permissions.Where(p => validPerms.Contains(p)).ToHashSet();

            // Entfernen
            var toRemove = admin.Permissions.Where(p => !requested.Contains(p.Permission)).ToList();
            _db.AdminUserPermissions.RemoveRange(toRemove);

            // Hinzufügen
            var existing = admin.Permissions.Select(p => p.Permission).ToHashSet();
            foreach (var perm in requested.Where(p => !existing.Contains(p)))
            {
                _db.AdminUserPermissions.Add(new AdminUserPermission
                {
                    DiscordId = discordId,
                    Permission = perm
                });
            }

            await _db.SaveChangesAsync();
            await _audit.LogAsync("SetPermissions", "AdminUser", discordId,
                $"Permissions={string.Join(",", requested)}");
            TempData["AdminMessage"] = "Berechtigungen gespeichert.";
            return RedirectToAction(nameof(Admins));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAdmin(string discordId)
        {
            var admin = await _db.AdminUsers.FindAsync(discordId);
            if (admin is not null)
            {
                _db.AdminUsers.Remove(admin);
                await _db.SaveChangesAsync();
                await _audit.LogAsync("RemoveAdmin", "AdminUser", discordId, $"DisplayName={admin.DisplayName}");
                TempData["AdminMessage"] = "Admin entfernt.";
            }

            return RedirectToAction(nameof(Admins));
        }
    }
}
