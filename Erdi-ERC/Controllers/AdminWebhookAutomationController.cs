using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erdi_ERC.Controllers
{
    [Authorize(Policy = "Admin.System.Webhooks")]
    public class AdminWebhookAutomationController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IAdminAuditService _audit;
        private readonly IWebhookAutomationService _automation;

        public AdminWebhookAutomationController(AppDbContext db, IAdminAuditService audit, IWebhookAutomationService automation)
        {
            _db = db;
            _audit = audit;
            _automation = automation;
        }

        // Mock-Daten für die Test-Funktion, gruppiert pro Event-Typ.
        // Werden auch genutzt, wenn eine Rule ohne echte Event-Daten getestet wird.
        private static readonly Dictionary<string, Dictionary<string, string>> MockVarsByEvent = new()
        {
            [WebhookEvents.RaceResultSaved] = new()
            {
                ["League"] = "Main Division 1",
                ["Track"] = "Spa-Francorchamps",
                ["Date"] = DateTime.UtcNow.ToString("dd.MM.yyyy"),
                ["Winner"] = "Max Mustermann",
                ["FastestLap"] = "1:43.219"
            },
            [WebhookEvents.PenaltySaved] = new()
            {
                ["Driver"] = "Max Mustermann",
                ["SecondDriver"] = "Lisa Beispiel",
                ["PenaltyType"] = "5-Sekunden-Strafe",
                ["Points"] = "2",
                ["RaceTrack"] = "Monza",
                ["League"] = "Main Division 1",
                ["Date"] = DateTime.UtcNow.ToString("dd.MM.yyyy"),
                ["Reason"] = "Kollision in Kurve 1"
            },
            [WebhookEvents.NewsPostPublished] = new()
            {
                ["Title"] = "Saison 2026 startet im Mai",
                ["Category"] = "Ankündigung",
                ["Summary"] = "Alle Infos zum Saisonstart der ERC-Liga.",
                ["Author"] = "Erdi"
            },
            [WebhookEvents.RaceWeekendSaved] = new()
            {
                ["Track"] = "Silverstone",
                ["DistancePercent"] = "50",
                ["Order"] = "1",
                ["Legs"] = "Quali + Race"
            },
            [WebhookEvents.StreamScheduled] = new()
            {
                ["Title"] = "Erdi10 Live · Main Division 1",
                ["Url"] = "https://twitch.tv/erdi10",
                ["StartAt"] = DateTime.UtcNow.AddDays(1).ToString("dd.MM.yyyy HH:mm")
            },
            [WebhookEvents.HighlightApproved] = new()
            {
                ["Title"] = "Last-Lap-Overtake in Spa",
                ["Url"] = "https://youtube.com/watch?v=dQw4w9WgXcQ",
                ["Category"] = "Overtake",
                ["RaceLabel"] = "Spa GP – Runde 3",
                ["Author"] = "Erdi"
            },
            [WebhookEvents.VotePollPublished] = new()
            {
                ["Title"] = "Lieblings-Strecke Saison 2026",
                ["Category"] = "Voting",
                ["Description"] = "Welche Strecke möchtet ihr für das Bonus-Event?",
                ["Options"] = "Spa, Monza, Suzuka"
            }
        };

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var rules = await _db.WebhookAutomationRules
                .Include(r => r.Webhook)
                .OrderBy(r => r.EventType)
                .ToListAsync();

            var webhooks = await _db.DiscordWebhooks
                .OrderBy(w => w.Name)
                .ToListAsync();

            ViewBag.Webhooks = webhooks;
            ViewBag.Events   = WebhookEvents.All;
            return View("~/Views/Admin/WebhookAutomation/Index.cshtml", rules);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(
            int? id,
            string eventType,
            int webhookId,
            bool isEnabled,
            string? contentTemplate,
            string? usernameOverride,
            string? avatarUrlOverride,
            bool useEmbed,
            string? embedTitleTemplate,
            string? embedDescriptionTemplate,
            string? embedColor,
            string? embedFooterTemplate,
            string? embedThumbnailTemplate)
        {
            if (string.IsNullOrWhiteSpace(eventType) || webhookId <= 0)
            {
                TempData["Error"] = "Event-Typ und Webhook sind Pflichtfelder.";
                return RedirectToAction(nameof(Index));
            }

            WebhookAutomationRule? rule = id.HasValue && id.Value > 0
                ? await _db.WebhookAutomationRules.FindAsync(id.Value)
                : null;

            var isNew = rule is null;
            if (rule is null)
            {
                rule = new WebhookAutomationRule
                {
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = User.Identity?.Name ?? "Admin"
                };
                _db.WebhookAutomationRules.Add(rule);
            }

            rule.EventType                 = eventType.Trim();
            rule.WebhookId                 = webhookId;
            rule.IsEnabled                 = isEnabled;
            rule.ContentTemplate           = string.IsNullOrWhiteSpace(contentTemplate)           ? null : contentTemplate.Trim();
            rule.UsernameOverride          = string.IsNullOrWhiteSpace(usernameOverride)          ? null : usernameOverride.Trim();
            rule.AvatarUrlOverride         = string.IsNullOrWhiteSpace(avatarUrlOverride)         ? null : avatarUrlOverride.Trim();
            rule.UseEmbed                  = useEmbed;
            rule.EmbedTitleTemplate        = string.IsNullOrWhiteSpace(embedTitleTemplate)        ? null : embedTitleTemplate.Trim();
            rule.EmbedDescriptionTemplate  = string.IsNullOrWhiteSpace(embedDescriptionTemplate)  ? null : embedDescriptionTemplate.Trim();
            rule.EmbedColor                = string.IsNullOrWhiteSpace(embedColor)                ? "#e10600" : embedColor.Trim();
            rule.EmbedFooterTemplate       = string.IsNullOrWhiteSpace(embedFooterTemplate)       ? null : embedFooterTemplate.Trim();
            rule.EmbedThumbnailTemplate    = string.IsNullOrWhiteSpace(embedThumbnailTemplate)    ? null : embedThumbnailTemplate.Trim();

            await _db.SaveChangesAsync();
            await _audit.LogAsync(
                isNew ? "CreateAutomationRule" : "UpdateAutomationRule",
                "WebhookAutomationRule", rule.Id.ToString(),
                $"Event={rule.EventType}, Webhook={rule.WebhookId}, Enabled={rule.IsEnabled}");

            TempData["Success"] = isNew ? "Automatisierungsregel erstellt." : "Automatisierungsregel aktualisiert.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleEnabled(int id)
        {
            var rule = await _db.WebhookAutomationRules.FindAsync(id);
            if (rule is null) return NotFound();
            rule.IsEnabled = !rule.IsEnabled;
            await _db.SaveChangesAsync();
            TempData["Success"] = rule.IsEnabled ? "Regel aktiviert." : "Regel deaktiviert.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var rule = await _db.WebhookAutomationRules.FindAsync(id);
            if (rule is null) return NotFound();
            _db.WebhookAutomationRules.Remove(rule);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("DeleteAutomationRule", "WebhookAutomationRule", id.ToString(), $"Event={rule.EventType}");
            TempData["Success"] = "Regel gelöscht.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Schickt die Rule mit Mock-Daten an Discord – nützlich zum Verifizieren von Templates
        /// ohne dass das eigentliche System-Event ausgelöst werden muss.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Test(int id)
        {
            var rule = await _db.WebhookAutomationRules.Include(r => r.Webhook).FirstOrDefaultAsync(r => r.Id == id);
            if (rule is null) return NotFound();

            var mockVars = MockVarsByEvent.TryGetValue(rule.EventType, out var v)
                ? v
                : new Dictionary<string, string>();

            var (success, error) = await _automation.TestRuleAsync(id, mockVars, HttpContext.RequestAborted);

            await _audit.LogAsync("TestAutomationRule", "WebhookAutomationRule", id.ToString(),
                $"Event={rule.EventType}, Webhook={rule.Webhook?.Name}, Result={(success ? "OK" : "Failed")}");

            if (success)
                TempData["Success"] = $"Test-Nachricht an \"{rule.Webhook?.Name}\" gesendet.";
            else
                TempData["Error"] = $"Test fehlgeschlagen: {error}";

            return RedirectToAction(nameof(Index));
        }

        /// <summary>Dupliziert eine bestehende Rule als deaktivierte Kopie.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Duplicate(int id)
        {
            var src = await _db.WebhookAutomationRules.FindAsync(id);
            if (src is null) return NotFound();

            var copy = new WebhookAutomationRule
            {
                EventType = src.EventType,
                WebhookId = src.WebhookId,
                IsEnabled = false,
                ContentTemplate = src.ContentTemplate,
                UsernameOverride = src.UsernameOverride,
                AvatarUrlOverride = src.AvatarUrlOverride,
                UseEmbed = src.UseEmbed,
                EmbedTitleTemplate = src.EmbedTitleTemplate,
                EmbedDescriptionTemplate = src.EmbedDescriptionTemplate,
                EmbedColor = src.EmbedColor,
                EmbedFooterTemplate = src.EmbedFooterTemplate,
                EmbedThumbnailTemplate = src.EmbedThumbnailTemplate,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "Admin"
            };
            _db.WebhookAutomationRules.Add(copy);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("DuplicateAutomationRule", "WebhookAutomationRule", copy.Id.ToString(),
                $"Source={src.Id}, Event={src.EventType}");

            TempData["Success"] = "Regel dupliziert (deaktiviert). Anpassen und aktivieren nicht vergessen.";
            return RedirectToAction(nameof(Index));
        }
    }
}
