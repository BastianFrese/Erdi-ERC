using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace <OWNER_HANDLE>_ERC.Models
{
    /// <summary>
    /// Defines which Discord webhook fires automatically when a specific system event occurs.
    /// Message templates support {{variable}} placeholders that get substituted at runtime.
    /// </summary>
    public class WebhookAutomationRule
    {
        public int Id { get; set; }

        /// <summary>System event key, e.g. "race.result.saved"</summary>
        [Required, MaxLength(64)]
        public string EventType { get; set; } = string.Empty;

        /// <summary>FK to the webhook that will be triggered.</summary>
        public int WebhookId { get; set; }

        [ForeignKey(nameof(WebhookId))]
        public DiscordWebhook? Webhook { get; set; }

        public bool IsEnabled { get; set; } = true;

        // ── Plain message ────────────────────────────────────────────────
        [MaxLength(2000)]
        public string? ContentTemplate { get; set; }

        // ── Bot override ─────────────────────────────────────────────────
        [MaxLength(80)]
        public string? UsernameOverride { get; set; }

        [MaxLength(512)]
        public string? AvatarUrlOverride { get; set; }

        // ── Embed ────────────────────────────────────────────────────────
        public bool UseEmbed { get; set; } = true;

        [MaxLength(256)]
        public string? EmbedTitleTemplate { get; set; }

        [MaxLength(4096)]
        public string? EmbedDescriptionTemplate { get; set; }

        /// <summary>Hex color like ***REMOVED***e10600</summary>
        [MaxLength(9)]
        public string? EmbedColor { get; set; } = "***REMOVED***e10600";

        [MaxLength(512)]
        public string? EmbedFooterTemplate { get; set; }

        /// <summary>Optional thumbnail URL template.</summary>
        [MaxLength(512)]
        public string? EmbedThumbnailTemplate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(128)]
        public string CreatedBy { get; set; } = string.Empty;

        // ── Run-Status (gefüllt vom WebhookAutomationService nach jedem FireAsync) ────
        public DateTime? LastRunAt { get; set; }

        /// <summary>Result of the last run: "OK", "Failed", "Skipped".</summary>
        [MaxLength(16)]
        public string? LastStatus { get; set; }

        /// <summary>Truncated error message from the last failed run.</summary>
        [MaxLength(500)]
        public string? LastError { get; set; }

        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
    }
}
