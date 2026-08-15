using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace Erdi_ERC.Services
{
    public interface IWebhookAutomationService
    {
        /// <summary>
        /// Fire all enabled rules for <paramref name="eventType"/>.
        /// Variables in templates like {{RaceTrack}} are replaced from <paramref name="vars"/> (case-insensitive).
        /// <para>
        /// Bewusst KEIN CancellationToken: Webhook-Delivery soll auch dann durchlaufen, wenn der
        /// auslösende Request bereits beendet ist (Save-Action returnt sofort, Webhooks fire-and-forget).
        /// </para>
        /// </summary>
        Task FireAsync(string eventType, Dictionary<string, string> vars);

        /// <summary>
        /// Send a single rule with the given variables (used for testing rules from the admin UI).
        /// Updates the rule's run-status fields. Cancellable, weil der Admin interaktiv auf das Ergebnis wartet.
        /// </summary>
        Task<(bool Success, string? Error)> TestRuleAsync(int ruleId, Dictionary<string, string> vars, CancellationToken ct = default);
    }

    public class WebhookAutomationService : IWebhookAutomationService
    {
        private const string HttpClientName = "WebhookAutomation";
        private static readonly int[] RetryDelaysMs = { 250, 750, 2000 };

        private readonly AppDbContext _db;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WebhookAutomationService> _logger;

        public WebhookAutomationService(AppDbContext db, IHttpClientFactory httpClientFactory, ILogger<WebhookAutomationService> logger)
        {
            _db = db;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task FireAsync(string eventType, Dictionary<string, string> vars)
        {
            // Bewusst KEIN AsTracking: Status-Updates laufen über ExecuteUpdateAsync (atomic SQL-Increment),
            // damit zwei parallele Events auf dieselbe Rule nicht den Counter überschreiben.
            var rules = await _db.WebhookAutomationRules
                .Include(r => r.Webhook)
                .Where(r => r.EventType == eventType && r.IsEnabled && r.Webhook != null)
                .ToListAsync();

            if (rules.Count == 0) return;

            var client = _httpClientFactory.CreateClient(HttpClientName);

            foreach (var rule in rules)
            {
                await ExecuteRuleAsync(client, rule, vars);
            }
        }

        public async Task<(bool Success, string? Error)> TestRuleAsync(int ruleId, Dictionary<string, string> vars, CancellationToken ct = default)
        {
            var rule = await _db.WebhookAutomationRules
                .Include(r => r.Webhook)
                .FirstOrDefaultAsync(r => r.Id == ruleId, ct);

            if (rule is null) return (false, "Rule nicht gefunden");
            if (rule.Webhook is null) return (false, "Kein Webhook verknüpft");

            var client = _httpClientFactory.CreateClient(HttpClientName);
            await ExecuteRuleAsync(client, rule, vars, ct);

            return rule.LastStatus == "OK" ? (true, null) : (false, rule.LastError);
        }

        // ── Internal ─────────────────────────────────────────────────────────────

        private async Task ExecuteRuleAsync(HttpClient client, WebhookAutomationRule rule, Dictionary<string, string> vars, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            try
            {
                await SendRuleWithRetryAsync(client, rule, vars, ct);

                // In-Memory-Update für direkten Return-Value (TestRule liest LastStatus).
                rule.LastRunAt = now;
                rule.LastStatus = "OK";
                rule.LastError = null;
                rule.SuccessCount++;

                // Atomarer DB-Increment: zwei parallele Events auf dieselbe Rule können sich
                // nicht gegenseitig überschreiben (SQL UPDATE x SET cnt = cnt + 1).
                await _db.WebhookAutomationRules
                    .Where(r => r.Id == rule.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(r => r.LastRunAt, now)
                        .SetProperty(r => r.LastStatus, "OK")
                        .SetProperty(r => r.LastError, (string?)null)
                        .SetProperty(r => r.SuccessCount, r => r.SuccessCount + 1), ct);

                if (rule.Webhook != null)
                {
                    await _db.DiscordWebhooks
                        .Where(w => w.Id == rule.Webhook.Id)
                        .ExecuteUpdateAsync(s => s.SetProperty(w => w.LastUsedAt, now), ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook-Automation failed for rule {RuleId} / event {Event}", rule.Id, rule.EventType);
                var msg = Truncate(ex.Message, 500);

                rule.LastRunAt = now;
                rule.LastStatus = "Failed";
                rule.LastError = msg;
                rule.FailureCount++;

                try
                {
                    await _db.WebhookAutomationRules
                        .Where(r => r.Id == rule.Id)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(r => r.LastRunAt, now)
                            .SetProperty(r => r.LastStatus, "Failed")
                            .SetProperty(r => r.LastError, msg)
                            .SetProperty(r => r.FailureCount, r => r.FailureCount + 1), ct);
                }
                catch (Exception inner)
                {
                    // Wenn selbst das Status-Update scheitert, sonst nur loggen – nicht eskalieren.
                    _logger.LogError(inner, "Failed to persist webhook failure-status for rule {RuleId}", rule.Id);
                }
            }
        }

        private static async Task SendRuleWithRetryAsync(HttpClient client, WebhookAutomationRule rule, Dictionary<string, string> vars, CancellationToken ct)
        {
            var payload = BuildPayload(rule, vars);
            if (payload.Count == 0) return;

            var json = JsonSerializer.Serialize(payload);
            var url = rule.Webhook!.Url;

            for (var attempt = 0; ; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                HttpResponseMessage response;
                try
                {
                    response = await client.SendAsync(request, ct);
                }
                catch (HttpRequestException) when (attempt < RetryDelaysMs.Length)
                {
                    await Task.Delay(RetryDelaysMs[attempt], ct);
                    continue;
                }

                if (response.IsSuccessStatusCode)
                {
                    response.Dispose();
                    return;
                }

                var status = (int)response.StatusCode;
                var body = await response.Content.ReadAsStringAsync(ct);
                response.Dispose();

                var isTransient = status >= 500 || status == 429;
                if (isTransient && attempt < RetryDelaysMs.Length)
                {
                    await Task.Delay(RetryDelaysMs[attempt], ct);
                    continue;
                }

                throw new InvalidOperationException($"Discord returned {status}: {Truncate(body, 300)}");
            }
        }

        private static Dictionary<string, object?> BuildPayload(WebhookAutomationRule rule, Dictionary<string, string> vars)
        {
            var payload = new Dictionary<string, object?>();

            var content = Substitute(rule.ContentTemplate, vars);
            if (!string.IsNullOrWhiteSpace(content))
                payload["content"] = content;

            if (!string.IsNullOrWhiteSpace(rule.UsernameOverride))
                payload["username"] = rule.UsernameOverride;

            if (!string.IsNullOrWhiteSpace(rule.AvatarUrlOverride))
                payload["avatar_url"] = rule.AvatarUrlOverride;

            if (rule.UseEmbed)
            {
                var embed = new Dictionary<string, object?>();

                var title = Substitute(rule.EmbedTitleTemplate, vars);
                var desc = Substitute(rule.EmbedDescriptionTemplate, vars);
                var footer = Substitute(rule.EmbedFooterTemplate, vars);
                var thumb = Substitute(rule.EmbedThumbnailTemplate, vars);

                if (!string.IsNullOrWhiteSpace(title)) embed["title"] = title;
                if (!string.IsNullOrWhiteSpace(desc)) embed["description"] = desc;
                if (!string.IsNullOrWhiteSpace(footer)) embed["footer"] = new { text = footer };
                if (!string.IsNullOrWhiteSpace(thumb)) embed["thumbnail"] = new { url = thumb };

                if (!string.IsNullOrWhiteSpace(rule.EmbedColor) &&
                    rule.EmbedColor.StartsWith('#') &&
                    int.TryParse(rule.EmbedColor[1..], System.Globalization.NumberStyles.HexNumber, null, out int colorInt))
                    embed["color"] = colorInt;

                embed["timestamp"] = DateTime.UtcNow.ToString("o");
                payload["embeds"] = new[] { embed };
            }

            return payload;
        }

        private static string? Substitute(string? template, Dictionary<string, string> vars)
        {
            if (string.IsNullOrWhiteSpace(template)) return template;

            // Case-insensitive Lookup: erlaubt {{track}} und {{Track}} im Template
            // gegen vars["Track"] zu matchen.
            var ciVars = new Dictionary<string, string>(vars, StringComparer.OrdinalIgnoreCase);

            var sb = new StringBuilder(template);
            // Erst exakte Matches (schnellster Pfad), dann case-insensitive Pattern-Match.
            foreach (var (key, value) in vars)
                sb.Replace($"{{{{{key}}}}}", value);

            // Für case-insensitive: per Regex über das Template laufen.
            var result = System.Text.RegularExpressions.Regex.Replace(
                sb.ToString(),
                @"\{\{(\w+)\}\}",
                m => ciVars.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);

            return result;
        }

        private static string Truncate(string s, int max)
            => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];
    }
}
