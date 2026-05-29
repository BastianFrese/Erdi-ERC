namespace <OWNER_HANDLE>_ERC.Models
{
    public class AdminUser
    {
        /// <summary>Discord User-ID (Snowflake) – z. B. "123456789012345678".</summary>
        public string DiscordId { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Superadmins haben vollen Zugriff auf alle Bereiche, unabhängig von einzelnen Berechtigungen.
        /// </summary>
        public bool IsSuperAdmin { get; set; } = false;

        public List<AdminUserPermission> Permissions { get; set; } = [];
    }

    /// <summary>
    /// Einzelne Berechtigungszuweisung für einen Admin-User.
    /// </summary>
    public class AdminUserPermission
    {
        public int Id { get; set; }
        public string DiscordId { get; set; } = string.Empty;

        /// <summary>Berechtigungsschlüssel, z. B. "applications". Siehe <see cref="AdminPermissions"/>.</summary>
        public string Permission { get; set; } = string.Empty;

        public AdminUser? AdminUser { get; set; }
    }
}
