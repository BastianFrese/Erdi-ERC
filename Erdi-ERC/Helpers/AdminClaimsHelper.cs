using System.Security.Claims;
using <OWNER_HANDLE>_ERC.Models;

namespace <OWNER_HANDLE>_ERC.Helpers
{
    /// <summary>
    /// Setzt die Admin-Claims (erdi:admin, ggf. erdi:superadmin + erdi:perm) auf einer Identity.
    /// Wird beim Login (AccountController) UND beim Claims-Refresh (OnValidatePrincipal in
    /// Program.cs) gemeinsam verwendet, damit beide Stellen identische Claims erzeugen.
    /// Ohne den Aufruf beim Login hätte ein frisch eingeloggter Admin bis zum ersten
    /// Sync-Intervall (RefreshDiscordMembershipMinutes) keine Bereichs-Claims und sähe
    /// in der Admin-Sidebar keine Kategorie-Gruppen.
    /// </summary>
    public static class AdminClaimsHelper
    {
        public static void AddAdminClaims(ClaimsIdentity identity, AdminUser? adminUser)
        {
            if (adminUser is null) return;

            identity.AddClaim(new Claim("erdi:admin", "true"));

            if (adminUser.IsSuperAdmin)
            {
                identity.AddClaim(new Claim("erdi:superadmin", "true"));
                // Superadmin bekommt alle Berechtigungen implizit.
                foreach (var perm in AdminPermissions.All)
                {
                    identity.AddClaim(new Claim("erdi:perm", perm.Key));
                }
            }
            else
            {
                foreach (var perm in adminUser.Permissions)
                {
                    identity.AddClaim(new Claim("erdi:perm", perm.Permission));
                }
            }
        }
    }
}