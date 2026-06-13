using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace <OWNER_HANDLE>_ERC.Controllers
{
    [Authorize(Policy = "Admin.League")]
    public class AdminRaceCalendarController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IAdminAuditService _audit;
        private readonly IMediaService _media;
        private readonly IWebhookAutomationService _webhookAuto;

        public AdminRaceCalendarController(
            AppDbContext db,
            IAdminAuditService audit,
            IMediaService media,
            IWebhookAutomationService webhookAuto)
        {
            _db = db;
            _audit = audit;
            _media = media;
            _webhookAuto = webhookAuto;
        }

        // ── Index ────────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var settings = await GetOrCreateSettingsAsync();
            var leagues = await _db.Leagues
                .Where(l => !l.IsArchived)
                .OrderBy(l => l.Name)
                .ToListAsync();
            var weekends = await _db.RaceWeekends
                .Include(w => w.Legs)
                .OrderBy(w => w.Order)
                .ThenBy(w => w.Id)
                .ToListAsync();

            ViewBag.Settings = settings;
            ViewBag.Leagues = leagues;
            ViewBag.Weekends = weekends;
            return View("~/Views/Admin/RaceCalendar.cshtml");
        }

        // ── Settings (Background + Saison-Titel) ─────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSettings(
            string? seasonTitle,
            string? seasonSubtitle,
            IFormFile? backgroundImage,
            bool removeBackground = false)
        {
            var settings = await GetOrCreateSettingsAsync();

            settings.SeasonTitle = string.IsNullOrWhiteSpace(seasonTitle) ? null : seasonTitle.Trim();
            settings.SeasonSubtitle = string.IsNullOrWhiteSpace(seasonSubtitle) ? null : seasonSubtitle.Trim();

            var msgParts = new List<string>();
            msgParts.Add($"Datei empfangen: {(backgroundImage is null ? "—" : $"{backgroundImage.FileName} ({backgroundImage.Length} bytes)")}");

            if (removeBackground && !string.IsNullOrWhiteSpace(settings.BackgroundImageFileName))
            {
                _media.TryDeleteCalendarBackground(settings.BackgroundImageFileName);
                settings.BackgroundImageFileName = null;
                msgParts.Add("Altes Bild entfernt.");
            }

            if (backgroundImage is { Length: > 0 })
            {
                var saved = await _media.SaveCalendarBackgroundAsync(backgroundImage);
                if (saved is not null)
                {
                    if (!string.IsNullOrWhiteSpace(settings.BackgroundImageFileName))
                    {
                        _media.TryDeleteCalendarBackground(settings.BackgroundImageFileName);
                    }
                    settings.BackgroundImageFileName = saved;
                    msgParts.Add($"Neues Bild gespeichert ({saved}).");
                }
                else
                {
                    msgParts.Add("⚠ Bild abgelehnt (Format/Größe). Erlaubt: jpg, png, gif, webp, avif – max. 10 MB.");
                }
            }

            settings.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("SaveCalendarSettings", "RaceCalendarSettings", settings.Id.ToString(),
                $"Title={settings.SeasonTitle}, Bg={settings.BackgroundImageFileName}");

            TempData["AdminMessage"] = "Kalender-Einstellungen gespeichert · " + string.Join(" · ", msgParts);
            return RedirectToAction(nameof(Index));
        }

        // ── RaceWeekend Save ─────────────────────────────────────────────────────
        // legLeagueIds[] + legDates[] sind parallel: legLeagueIds[i] entspricht legDates[i].
        // Leere Date-Strings bedeuten "kein Termin für diese Liga".
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveWeekend(
            int? id,
            int order,
            string track,
            int distancePercent,
            string[]? legLeagueIds,
            string[]? legDates)
        {
            track = (track ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(track))
            {
                TempData["AdminMessage"] = "Streckenname muss angegeben werden.";
                return RedirectToAction(nameof(Index));
            }

            if (distancePercent is not (25 or 35 or 50 or 100))
            {
                distancePercent = 100;
            }

            RaceWeekend? weekend = id.HasValue
                ? await _db.RaceWeekends.AsTracking().Include(w => w.Legs).FirstOrDefaultAsync(w => w.Id == id.Value)
                : null;
            var isNew = weekend is null;
            if (weekend is null)
            {
                weekend = new RaceWeekend();
                _db.RaceWeekends.Add(weekend);
            }
            weekend.Track = track;
            weekend.Order = order;
            weekend.DistancePercent = distancePercent;

            var ids   = legLeagueIds ?? Array.Empty<string>();
            var dates = legDates     ?? Array.Empty<string>();
            var existingByLeague = weekend.Legs.ToDictionary(l => l.LeagueId, StringComparer.OrdinalIgnoreCase);
            var keepLeagueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < ids.Length && i < dates.Length; i++)
            {
                var leagueId = ids[i]?.Trim();
                var rawDate  = dates[i]?.Trim();
                if (string.IsNullOrWhiteSpace(leagueId) || string.IsNullOrWhiteSpace(rawDate)) continue;
                if (!DateTime.TryParse(rawDate, out var parsed)) continue;

                keepLeagueIds.Add(leagueId);
                if (existingByLeague.TryGetValue(leagueId, out var leg))
                {
                    leg.Date = parsed;
                }
                else
                {
                    weekend.Legs.Add(new RaceWeekendLeg { LeagueId = leagueId, Date = parsed });
                }
            }

            var toRemove = weekend.Legs.Where(l => !keepLeagueIds.Contains(l.LeagueId)).ToList();
            foreach (var rm in toRemove) weekend.Legs.Remove(rm);

            await _db.SaveChangesAsync();
            await _audit.LogAsync("SaveRaceWeekend", "RaceWeekend", weekend.Id.ToString(),
                $"Track={track}, Distance={distancePercent}%, Legs={weekend.Legs.Count}");

            if (isNew)
            {
                await _webhookAuto.FireAsync(WebhookEvents.RaceWeekendSaved, new()
                {
                    ["Track"]           = weekend.Track,
                    ["DistancePercent"] = weekend.DistancePercent.ToString(),
                    ["Order"]           = weekend.Order.ToString(),
                    ["Legs"]            = string.Join(" · ",
                        weekend.Legs.OrderBy(l => l.Date).Select(l => $"{l.LeagueId} {l.Date:dd.MM.yyyy HH:mm}")),
                });
            }

            TempData["AdminMessage"] = isNew ? "Renn-Wochenende angelegt." : "Renn-Wochenende aktualisiert.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteWeekend(int id)
        {
            var weekend = await _db.RaceWeekends.Include(w => w.Legs).FirstOrDefaultAsync(w => w.Id == id);
            if (weekend is null) return RedirectToAction(nameof(Index));
            _db.RaceWeekends.Remove(weekend);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("DeleteRaceWeekend", "RaceWeekend", id.ToString(), $"Track={weekend.Track}");
            TempData["AdminMessage"] = "Renn-Wochenende entfernt.";
            return RedirectToAction(nameof(Index));
        }

        // ── Helpers ──────────────────────────────────────────────────────────────
        private async Task<RaceCalendarSettings> GetOrCreateSettingsAsync()
        {
            var settings = await _db.RaceCalendarSettings.AsTracking().FirstOrDefaultAsync();
            if (settings is null)
            {
                settings = new RaceCalendarSettings { Id = 1, UpdatedAt = DateTime.UtcNow };
                _db.RaceCalendarSettings.Add(settings);
                await _db.SaveChangesAsync();
            }
            return settings;
        }
    }
}
