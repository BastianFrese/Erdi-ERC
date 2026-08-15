using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Erdi_ERC.Controllers
{
    [Authorize(Policy = "Admin.System.Webhooks")]
    public class AdminWebhooksController : Controller
    {
        // Akzeptiert sowohl discord.com als auch discordapp.com (Legacy-Domain).
        private static readonly Regex DiscordWebhookUrlPattern =
            new(@"^https://(?:ptb\.|canary\.)?discord(?:app)?\.com/api/(?:v\d+/)?webhooks/\d+/[\w-]+/?$",
                RegexOptions.Compiled);

        private readonly AppDbContext _db;
        private readonly IAdminAuditService _audit;
        private readonly IHttpClientFactory _httpClientFactory;

        public AdminWebhooksController(AppDbContext db, IAdminAuditService audit, IHttpClientFactory httpClientFactory)
        {
            _db = db;
            _audit = audit;
            _httpClientFactory = httpClientFactory;
        }

        // ── Index ────────────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var webhooks = await _db.DiscordWebhooks.OrderByDescending(w => w.CreatedAt).ToListAsync();
            ViewBag.Categories = webhooks
                .Where(w => !string.IsNullOrWhiteSpace(w.Category))
                .Select(w => w.Category!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList();
            return View("~/Views/Admin/Webhooks/Index.cshtml", webhooks);
        }

        // ── Create ───────────────────────────────────────────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string name, string url, string? description, string? category)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
            {
                TempData["WebhookError"] = "Name und URL sind Pflichtfelder.";
                return RedirectToAction(nameof(Index));
            }

            url = url.Trim();
            if (!DiscordWebhookUrlPattern.IsMatch(url))
            {
                TempData["WebhookError"] = "Ungültige Webhook-URL. Erwartet: https://discord.com/api/webhooks/<id>/<token>";
                return RedirectToAction(nameof(Index));
            }

            var webhook = new DiscordWebhook
            {
                Name = name.Trim(),
                Url = url,
                Description = description?.Trim(),
                Category = category?.Trim(),
                CreatedBy = User.Identity?.Name ?? "Admin",
                CreatedAt = DateTime.UtcNow
            };
            _db.DiscordWebhooks.Add(webhook);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Webhook erstellt", "DiscordWebhook", webhook.Id.ToString(), $"Name: {webhook.Name}, By: {User.Identity?.Name}");
            TempData["WebhookSuccess"] = $"Webhook \"{webhook.Name}\" wurde gespeichert.";
            return RedirectToAction(nameof(Index));
        }

        // ── Edit ─────────────────────────────────────────────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string name, string url, string? description, string? category)
        {
            var webhook = await _db.DiscordWebhooks.FindAsync(id);
            if (webhook == null) return NotFound();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
            {
                TempData["WebhookError"] = "Name und URL sind Pflichtfelder.";
                return RedirectToAction(nameof(Index));
            }

            url = url.Trim();
            if (!DiscordWebhookUrlPattern.IsMatch(url))
            {
                TempData["WebhookError"] = "Ungültige Webhook-URL. Erwartet: https://discord.com/api/webhooks/<id>/<token>";
                return RedirectToAction(nameof(Index));
            }

            webhook.Name = name.Trim();
            webhook.Url = url;
            webhook.Description = description?.Trim();
            webhook.Category = category?.Trim();
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Webhook bearbeitet", "DiscordWebhook", webhook.Id.ToString(), $"Name: {webhook.Name}, By: {User.Identity?.Name}");
            TempData["WebhookSuccess"] = $"Webhook \"{webhook.Name}\" wurde aktualisiert.";
            return RedirectToAction(nameof(Index));
        }

        // ── Delete ───────────────────────────────────────────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var webhook = await _db.DiscordWebhooks.FindAsync(id);
            if (webhook == null) return NotFound();

            _db.DiscordWebhooks.Remove(webhook);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Webhook gelöscht", "DiscordWebhook", webhook.Id.ToString(), $"Name: {webhook.Name}, By: {User.Identity?.Name}");
            TempData["WebhookSuccess"] = $"Webhook \"{webhook.Name}\" wurde gelöscht.";
            return RedirectToAction(nameof(Index));
        }

        // ── Test ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Schickt einen kleinen Test-Embed an den Webhook. Bestätigt URL + Token,
        /// ohne dass eine vollständige Nachricht getippt werden muss.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Test(int id)
        {
            var webhook = await _db.DiscordWebhooks.FindAsync(id);
            if (webhook == null) return NotFound();

            var payload = new
            {
                username = "ERC Webhook Test",
                embeds = new[]
                {
                    new
                    {
                        title = "✅ Webhook-Test erfolgreich",
                        description = $"Dieser Webhook (`{webhook.Name}`) ist erreichbar und korrekt konfiguriert.",
                        color = 0x57f287,
                        timestamp = DateTime.UtcNow.ToString("o"),
                        footer = new { text = $"Test von {User.Identity?.Name ?? "Admin"} · erdi-erc.de" }
                    }
                }
            };

            var client = _httpClientFactory.CreateClient("WebhookAutomation");
            try
            {
                var json = JsonSerializer.Serialize(payload);
                using var request = new HttpRequestMessage(HttpMethod.Post, webhook.Url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    webhook.LastUsedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                    await _audit.LogAsync("Webhook getestet", "DiscordWebhook", webhook.Id.ToString(), $"Name: {webhook.Name}");
                    TempData["WebhookSuccess"] = $"Test an \"{webhook.Name}\" erfolgreich gesendet.";
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync();
                    TempData["WebhookError"] = $"Discord-Fehler {(int)response.StatusCode}: {Truncate(body, 200)}";
                }
            }
            catch (Exception ex)
            {
                TempData["WebhookError"] = $"Test fehlgeschlagen: {Truncate(ex.Message, 200)}";
            }

            return RedirectToAction(nameof(Index));
        }

        // ── Send ─────────────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> Send(int id)
        {
            var webhook = await _db.DiscordWebhooks.FindAsync(id);
            if (webhook == null) return NotFound();
            ViewBag.Webhook = webhook;
            return View("~/Views/Admin/Webhooks/Send.cshtml");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(
            int id,
            string? content,
            string? username,
            string? avatarUrl,
            // embed fields
            bool useEmbed,
            string? embedTitle,
            string? embedDescription,
            string? embedUrl,
            string? embedColor,
            string? embedThumbnail,
            string? embedImage,
            string? embedFooter,
            string? embedAuthorName,
            string? embedAuthorIconUrl,
            // fields (JSON array from hidden input)
            string? fieldsJson)
        {
            var webhook = await _db.DiscordWebhooks.FindAsync(id);
            if (webhook == null) return NotFound();

            // Build payload
            var payload = new Dictionary<string, object?>();

            if (!string.IsNullOrWhiteSpace(content))
                payload["content"] = content;

            if (!string.IsNullOrWhiteSpace(username))
                payload["username"] = username.Trim();

            if (!string.IsNullOrWhiteSpace(avatarUrl))
                payload["avatar_url"] = avatarUrl.Trim();

            if (useEmbed)
            {
                var embed = new Dictionary<string, object?>();

                if (!string.IsNullOrWhiteSpace(embedTitle))       embed["title"]       = embedTitle;
                if (!string.IsNullOrWhiteSpace(embedDescription)) embed["description"] = embedDescription;
                if (!string.IsNullOrWhiteSpace(embedUrl))         embed["url"]         = embedUrl;

                if (!string.IsNullOrWhiteSpace(embedColor) && embedColor.StartsWith('#') && int.TryParse(embedColor[1..], System.Globalization.NumberStyles.HexNumber, null, out int colorInt))
                    embed["color"] = colorInt;

                if (!string.IsNullOrWhiteSpace(embedThumbnail))
                    embed["thumbnail"] = new { url = embedThumbnail.Trim() };

                if (!string.IsNullOrWhiteSpace(embedImage))
                    embed["image"] = new { url = embedImage.Trim() };

                if (!string.IsNullOrWhiteSpace(embedFooter))
                    embed["footer"] = new { text = embedFooter.Trim() };

                if (!string.IsNullOrWhiteSpace(embedAuthorName))
                {
                    var author = new Dictionary<string, string> { ["name"] = embedAuthorName.Trim() };
                    if (!string.IsNullOrWhiteSpace(embedAuthorIconUrl))
                        author["icon_url"] = embedAuthorIconUrl.Trim();
                    embed["author"] = author;
                }

                // Fields
                if (!string.IsNullOrWhiteSpace(fieldsJson))
                {
                    try
                    {
                        var fields = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(fieldsJson);
                        if (fields?.Count > 0)
                            embed["fields"] = fields;
                    }
                    catch { /* ignore malformed */ }
                }

                embed["timestamp"] = DateTime.UtcNow.ToString("o");
                payload["embeds"] = new[] { embed };
            }

            if (payload.Count == 0)
            {
                TempData["WebhookError"] = "Die Nachricht darf nicht leer sein.";
                ViewBag.Webhook = webhook;
                PopulateDraft(content, username, avatarUrl, useEmbed, embedTitle, embedDescription, embedUrl,
                              embedColor, embedThumbnail, embedImage, embedFooter, embedAuthorName, embedAuthorIconUrl, fieldsJson);
                return View("~/Views/Admin/Webhooks/Send.cshtml");
            }

            var json    = JsonSerializer.Serialize(payload);
            var client  = _httpClientFactory.CreateClient("WebhookAutomation");

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, webhook.Url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    webhook.LastUsedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                    await _audit.LogAsync("Webhook-Nachricht gesendet", "DiscordWebhook", webhook.Id.ToString(), $"Webhook: {webhook.Name}, By: {User.Identity?.Name}");
                    TempData["WebhookSuccess"] = "Nachricht erfolgreich an Discord gesendet!";
                    return RedirectToAction(nameof(Index));
                }

                var body = await response.Content.ReadAsStringAsync();
                TempData["WebhookError"] = $"Discord-Fehler {(int)response.StatusCode}: {Truncate(body, 200)}";
            }
            catch (Exception ex)
            {
                TempData["WebhookError"] = $"Senden fehlgeschlagen: {Truncate(ex.Message, 200)}";
            }

            ViewBag.Webhook = webhook;
            PopulateDraft(content, username, avatarUrl, useEmbed, embedTitle, embedDescription, embedUrl,
                          embedColor, embedThumbnail, embedImage, embedFooter, embedAuthorName, embedAuthorIconUrl, fieldsJson);
            return View("~/Views/Admin/Webhooks/Send.cshtml");
        }

        private void PopulateDraft(
            string? content, string? username, string? avatarUrl, bool useEmbed,
            string? embedTitle, string? embedDescription, string? embedUrl, string? embedColor,
            string? embedThumbnail, string? embedImage, string? embedFooter,
            string? embedAuthorName, string? embedAuthorIconUrl, string? fieldsJson)
        {
            ViewBag.Draft = new Dictionary<string, string?>
            {
                ["content"] = content,
                ["username"] = username,
                ["avatarUrl"] = avatarUrl,
                ["useEmbed"] = useEmbed ? "true" : null,
                ["embedTitle"] = embedTitle,
                ["embedDescription"] = embedDescription,
                ["embedUrl"] = embedUrl,
                ["embedColor"] = embedColor,
                ["embedThumbnail"] = embedThumbnail,
                ["embedImage"] = embedImage,
                ["embedFooter"] = embedFooter,
                ["embedAuthorName"] = embedAuthorName,
                ["embedAuthorIconUrl"] = embedAuthorIconUrl,
                ["fieldsJson"] = fieldsJson,
            };
        }

        private static string Truncate(string s, int max)
            => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];
    }
}
