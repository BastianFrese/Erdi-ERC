using System.Text;
using System.Text.Json;
using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models.Troll;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace <OWNER_HANDLE>_ERC.Controllers
{
    /// <summary>
    /// Admin-Verwaltung des <OWNER_HANDLE>-Troll-Systems: Übersicht aller Gags (eingebaut + eigene),
    /// an/aus &amp; Gewicht je Gag, globale Settings sowie CRUD für eigene Inhalts-Gags.
    /// Schreibt nie in den Login-Pfad direkt – Änderungen werden über den (gecachten,
    /// fail-open) <see cref="ITrollService"/> wirksam; nach jeder Änderung wird dessen Cache verworfen.
    /// </summary>
    [Authorize(Policy = "Admin.System.Troll")]
    public sealed class AdminTrollController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IAdminAuditService _audit;
        private readonly ITrollService _troll;

        public AdminTrollController(AppDbContext db, IAdminAuditService audit, ITrollService troll)
        {
            _db = db;
            _audit = audit;
            _troll = troll;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var overrides = await _db.TrollGagOverrides.AsNoTracking().ToListAsync();
            var ovByKey = overrides
                .GroupBy(o => o.Key)
                .ToDictionary(g => g.Key, g => g.First());

            var builtIns = _troll.BuiltInCatalog.Select(def =>
            {
                var ov = ovByKey.TryGetValue(def.Key, out var o) ? o : null;
                return new TrollAdminGagRow
                {
                    Def = def,
                    IsEnabled = ov?.IsEnabled ?? true,
                    Weight = ov?.Weight ?? def.DefaultWeight,
                    IsOverridden = ov is not null
                };
            }).ToList();

            var custom = await _db.TrollCustomGags.AsNoTracking()
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var settings = await _db.TrollSettings.AsNoTracking().FirstOrDefaultAsync()
                           ?? new TrollSettingsEntity();

            var vm = new TrollAdminViewModel
            {
                Settings = settings,
                BuiltIns = builtIns,
                Custom = custom
            };
            return View("~/Views/Admin/Troll/Index.cshtml", vm);
        }

        // ───────────────────────── Globale Settings ─────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSettings(
            bool enabled, double triggerChance, int cooldownMinutes,
            bool applyToAdmins, int mercyAfterAttempts, int mathMaxOperand)
        {
            var settings = await _db.TrollSettings.FirstOrDefaultAsync();
            var isNew = settings is null;
            if (settings is null)
            {
                settings = new TrollSettingsEntity { Id = 1 };
                _db.TrollSettings.Add(settings);
            }

            settings.Enabled = enabled;
            settings.TriggerChance = Math.Clamp(triggerChance, 0d, 1d);
            settings.CooldownMinutes = Math.Max(0, cooldownMinutes);
            settings.ApplyToAdmins = applyToAdmins;
            settings.MercyAfterAttempts = Math.Max(1, mercyAfterAttempts);
            settings.MathMaxOperand = Math.Clamp(mathMaxOperand, 1, 9);

            await _db.SaveChangesAsync();
            _troll.InvalidateCache();
            await _audit.LogAsync("UpdateTrollSettings", "TrollSettings", "1",
                $"Enabled={settings.Enabled}, Chance={settings.TriggerChance:0.***REMOVED******REMOVED***}, Cooldown={settings.CooldownMinutes}min");

            TempData["Success"] = isNew ? "Troll-Settings gespeichert." : "Troll-Settings aktualisiert.";
            return RedirectToAction(nameof(Index));
        }

        // ───────────────────────── Eingebaute Gags (Override) ─────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveOverride(string key, bool isEnabled, int weight)
        {
            var def = _troll.BuiltInCatalog.FirstOrDefault(g => g.Key == key);
            if (def is null) return NotFound();

            var ov = await _db.TrollGagOverrides.FindAsync(key);
            if (ov is null)
            {
                ov = new TrollGagOverride { Key = key };
                _db.TrollGagOverrides.Add(ov);
            }
            ov.IsEnabled = isEnabled;
            ov.Weight = Math.Max(0, weight);

            await _db.SaveChangesAsync();
            _troll.InvalidateCache();
            await _audit.LogAsync("UpdateTrollGag", "TrollGagOverride", key,
                $"Enabled={ov.IsEnabled}, Weight={ov.Weight}");

            TempData["Success"] = $"Gag \"{key}\" gespeichert.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>Setzt einen eingebauten Gag auf Code-Default zurück (löscht den Override).</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetOverride(string key)
        {
            var ov = await _db.TrollGagOverrides.FindAsync(key);
            if (ov is not null)
            {
                _db.TrollGagOverrides.Remove(ov);
                await _db.SaveChangesAsync();
                _troll.InvalidateCache();
                await _audit.LogAsync("ResetTrollGag", "TrollGagOverride", key, "auf Code-Default zurückgesetzt");
            }
            TempData["Success"] = $"Gag \"{key}\" auf Standard zurückgesetzt.";
            return RedirectToAction(nameof(Index));
        }

        // ───────────────────────── Eigene Inhalts-Gags (CRUD) ─────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCustom(
            int? id, string kind, int category, string? key, string title,
            string? eyebrow, string? lead, string? body,
            string? question, string? answer, string? optionsRaw,
            bool isEnabled, int weight)
        {
            var gagKind = string.Equals(kind, "Quiz", StringComparison.OrdinalIgnoreCase)
                ? TrollCustomGagKind.Quiz
                : TrollCustomGagKind.Message;

            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["Error"] = "Titel ist ein Pflichtfeld.";
                return RedirectToAction(nameof(Index));
            }

            string? optionsJson = null;
            if (gagKind == TrollCustomGagKind.Quiz)
            {
                if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
                {
                    TempData["Error"] = "Quiz braucht Frage und Antwort.";
                    return RedirectToAction(nameof(Index));
                }

                var options = (optionsRaw ?? string.Empty)
                    .Split(new[] { '\n', '\r', ';', '|' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .ToList();
                // Sicherstellen, dass die richtige Antwort als Option vorhanden ist.
                if (options.Count > 0 && !options.Any(o => string.Equals(o, answer.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    options.Insert(0, answer.Trim());
                }
                if (options.Count > 0)
                {
                    optionsJson = JsonSerializer.Serialize(options);
                }
            }

            TrollCustomGag? gag = id is > 0 ? await _db.TrollCustomGags.FindAsync(id.Value) : null;
            var isNew = gag is null;
            if (gag is null)
            {
                gag = new TrollCustomGag
                {
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = User.Identity?.Name ?? "Admin"
                };
                _db.TrollCustomGags.Add(gag);
            }

            gag.Kind = gagKind;
            gag.Category = Enum.IsDefined(typeof(TrollGagCategory), category) ? (TrollGagCategory)category : TrollGagCategory.GlueckSprueche;
            gag.Key = await EnsureUniqueKeyAsync(key, title, gag.Id);
            gag.Title = title.Trim();
            gag.Eyebrow = Clean(eyebrow);
            gag.Lead = Clean(lead);
            gag.Body = gagKind == TrollCustomGagKind.Message ? Clean(body) : null;
            gag.Question = gagKind == TrollCustomGagKind.Quiz ? Clean(question) : null;
            gag.Answer = gagKind == TrollCustomGagKind.Quiz ? Clean(answer) : null;
            gag.OptionsJson = optionsJson;
            gag.IsEnabled = isEnabled;
            gag.Weight = Math.Max(0, weight);

            await _db.SaveChangesAsync();
            _troll.InvalidateCache();
            await _audit.LogAsync(isNew ? "CreateTrollCustomGag" : "UpdateTrollCustomGag",
                "TrollCustomGag", gag.Id.ToString(), $"Key={gag.Key}, Kind={gag.Kind}, Enabled={gag.IsEnabled}");

            TempData["Success"] = isNew ? "Eigener Gag erstellt." : "Eigener Gag aktualisiert.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleCustom(int id)
        {
            var gag = await _db.TrollCustomGags.FindAsync(id);
            if (gag is null) return NotFound();
            gag.IsEnabled = !gag.IsEnabled;
            await _db.SaveChangesAsync();
            _troll.InvalidateCache();
            TempData["Success"] = gag.IsEnabled ? "Gag aktiviert." : "Gag deaktiviert.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCustom(int id)
        {
            var gag = await _db.TrollCustomGags.FindAsync(id);
            if (gag is null) return NotFound();
            _db.TrollCustomGags.Remove(gag);
            await _db.SaveChangesAsync();
            _troll.InvalidateCache();
            await _audit.LogAsync("DeleteTrollCustomGag", "TrollCustomGag", id.ToString(), $"Key={gag.Key}");
            TempData["Success"] = "Eigener Gag gelöscht.";
            return RedirectToAction(nameof(Index));
        }

        // ───────────────────────── Live-Vorschau ─────────────────────────

        /// <summary>
        /// Erzwingt einen bestimmten Gag und zeigt die echte Gate-Seite – ohne auf den
        /// Login-Würfel zu warten. Nutzt dieselben TempData-Keys wie der Login-Trigger;
        /// nach „Reinlassen"/„Lösen" geht es zurück zur Admin-Übersicht.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Preview(string key)
        {
            var gag = _troll.FindGag(key);
            if (gag is null)
            {
                TempData["Error"] = $"Gag \"{key}\" nicht gefunden (evtl. deaktiviert?).";
                return RedirectToAction(nameof(Index));
            }

            var challenge = _troll.BuildChallenge(gag);
            TempData[TrollController.TkGag] = gag.Key;
            if (challenge.Prompt is not null) TempData[TrollController.TkPrompt] = challenge.Prompt;
            if (challenge.ExpectedAnswer is not null) TempData[TrollController.TkAnswer] = challenge.ExpectedAnswer;
            TempData[TrollController.TkReturnUrl] = Url.Action(nameof(Index)) ?? "/Admin/Troll";

            return RedirectToAction("Gate", "Troll");
        }

        // ───────────────────────── Helpers ─────────────────────────

        private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>Baut einen eindeutigen, stabilen Key (Slug) und stellt Kollisionsfreiheit sicher.</summary>
        private async Task<string> EnsureUniqueKeyAsync(string? requestedKey, string title, int currentId)
        {
            var baseKey = Slugify(string.IsNullOrWhiteSpace(requestedKey) ? title : requestedKey!);
            if (string.IsNullOrEmpty(baseKey)) baseKey = "gag";
            if (!baseKey.StartsWith("custom-", StringComparison.Ordinal)) baseKey = "custom-" + baseKey;

            var candidate = baseKey;
            var suffix = 2;
            while (await _db.TrollCustomGags.AnyAsync(g => g.Key == candidate && g.Id != currentId))
            {
                candidate = $"{baseKey}-{suffix++}";
            }
            return candidate;
        }

        private static string Slugify(string input)
        {
            var sb = new StringBuilder(input.Length);
            var lastDash = false;
            foreach (var ch in input.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(ch) && ch < 128)
                {
                    sb.Append(ch);
                    lastDash = false;
                }
                else if (ch is 'ä') { sb.Append("ae"); lastDash = false; }
                else if (ch is 'ö') { sb.Append("oe"); lastDash = false; }
                else if (ch is 'ü') { sb.Append("ue"); lastDash = false; }
                else if (ch is 'ß') { sb.Append("ss"); lastDash = false; }
                else if (!lastDash) { sb.Append('-'); lastDash = true; }
            }
            return sb.ToString().Trim('-');
        }
    }
}
