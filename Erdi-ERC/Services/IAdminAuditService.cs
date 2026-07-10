namespace <OWNER_HANDLE>_ERC.Services
{
    public interface IAdminAuditService
    {
        /// <summary>
        /// Fügt einen Audit-Eintrag hinzu, OHNE zu speichern. Der Aufrufer muss
        /// anschließend <c>SaveChangesAsync</c> aufrufen, damit der Eintrag mit der
        /// auditierten Operation in derselben Transaktion persistiert wird.
        /// </summary>
        Task LogAsync(string action, string entityType, string entityId, string details);

        /// <summary>
        /// Fügt einen Audit-Eintrag hinzu UND speichert ihn sofort. Für Audits, die
        /// nach einem bereits committeten Vorgang (post-commit, Batch) entstehen und
        /// daher von keinem nachfolgenden <c>SaveChangesAsync</c> mehr erfasst würden.
        /// </summary>
        Task LogAndSaveAsync(string action, string entityType, string entityId, string details);
    }
}
