using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace <OWNER_HANDLE>_ERC.Controllers
{
    [Authorize(Policy = "Admin.Community")]
    public class AdminCommunityController : Controller
    {
        private readonly ICommunityContentService _contentService;
        private readonly IMediaService _mediaService;
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly IAdminAuditService _audit;
        private readonly IWebhookAutomationService _webhookAuto;
        private readonly IStatsService _stats;
        private readonly IStaticDataCache _staticCache;

        public AdminCommunityController(
            ICommunityContentService contentService,
            IMediaService mediaService,
            AppDbContext db,
            IWebHostEnvironment env,
            IAdminAuditService audit,
            IWebhookAutomationService webhookAuto,
            IStatsService stats,
            IStaticDataCache staticCache)
        {
            _contentService = contentService;
            _mediaService = mediaService;
            _db = db;
            _env = env;
            _audit = audit;
            _webhookAuto = webhookAuto;
            _stats = stats;
            _staticCache = staticCache;
        }

        /// <summary>
        /// Punkteabzug-Strafen fließen direkt in die abgeleitete Tabelle ein — nach jeder
        /// Änderung daran die Liga neu berechnen und den öffentlichen Cache invalidieren.
        /// </summary>
        private async Task RecalculateAfterPenaltyAsync(string leagueId)
        {
            // Immer neu berechnen (auch wenn eine Strafe von "Punkteabzug" weg geändert wurde),
            // damit die abgeleitete Tabelle exakt dem aktuellen Strafen-Stand entspricht.
            if (string.IsNullOrWhiteSpace(leagueId)) return;
            await _stats.RebuildLeagueStandingsAsync(leagueId);
            _staticCache.InvalidateLeagues();
        }

        [HttpGet]
        [Authorize(Policy = "Admin.Community.Hub")]
        public async Task<IActionResult> Hub()
        {
            ViewBag.RecentNews = await _contentService.GetRecentNewsAsync(6);
            ViewBag.RecentVotes = await _contentService.GetRecentVotesAsync(6);
            ViewBag.RecentHighlights = await _contentService.GetRecentHighlightsAsync(6);
            ViewBag.PublicPenalties = await _contentService.GetPublicPenaltiesAsync(50);
            return View("~/Views/Admin/CommunityHub.cshtml");
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin.Community.Hub")]
        public async Task<IActionResult> SaveNewsPost(string title, string? category, string? summary, string content, string? authorName, bool isPinned = false, bool isPublished = true)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
            {
                TempData["AdminMessage"] = "Titel und Inhalt sind für News erforderlich.";
                return RedirectToAction(nameof(Hub));
            }

            var authorNameResolved = string.IsNullOrWhiteSpace(authorName) ? User.Identity?.Name ?? "Admin" : authorName;
            await _contentService.SaveNewsPostAsync(title, category, summary, content, authorNameResolved, isPinned, isPublished);
            if (isPublished)
                await _webhookAuto.FireAsync(WebhookEvents.NewsPostPublished, new()
                {
                    ["Title"]      = title,
                    ["Category"]   = category ?? "News",
                    ["Summary"]    = summary ?? "",
                    ["Author"]     = authorNameResolved,
                });
            TempData["AdminMessage"] = "Community-News gespeichert.";
            return RedirectToAction(nameof(Hub));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin.Community.Hub")]
        public async Task<IActionResult> SaveVotePoll(string title, string? category, string? description, string option1, string option2, string? option3 = null, string? option4 = null)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(option1) || string.IsNullOrWhiteSpace(option2))
            {
                TempData["AdminMessage"] = "Titel sowie mindestens zwei Optionen sind erforderlich.";
                return RedirectToAction(nameof(Hub));
            }

            await _contentService.SaveVotePollAsync(title, category, description, option1, option2, option3, option4);

            var options = string.Join(", ", new[] { option1, option2, option3, option4 }
                .Where(o => !string.IsNullOrWhiteSpace(o)));
            await _webhookAuto.FireAsync(WebhookEvents.VotePollPublished, new()
            {
                ["Title"]       = title,
                ["Category"]    = category ?? "Voting",
                ["Description"] = description ?? "",
                ["Options"]     = options,
            });

            TempData["AdminMessage"] = "Voting gespeichert.";
            return RedirectToAction(nameof(Hub));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin.Community.Hub")]
        public async Task<IActionResult> SaveHighlightClip(string title, string url, string? category, string? raceLabel)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
            {
                TempData["AdminMessage"] = "Titel und URL sind für Highlights erforderlich.";
                return RedirectToAction(nameof(Hub));
            }

            var submittedById = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var submittedByName = User.Identity?.Name ?? "Admin";
            await _contentService.SaveHighlightClipAsync(title, url, category, raceLabel, submittedById, submittedByName);
            await _webhookAuto.FireAsync(WebhookEvents.HighlightApproved, new()
            {
                ["Title"]     = title,
                ["Url"]       = url,
                ["Category"]  = category ?? "Highlight",
                ["RaceLabel"] = raceLabel ?? "",
                ["Author"]    = submittedByName,
            });
            TempData["AdminMessage"] = "Highlight gespeichert.";
            return RedirectToAction(nameof(Hub));
        }

        [HttpGet]
        [Authorize(Policy = "Admin.Community.Events")]
        public async Task<IActionResult> Events()
        {
            var events = await _db.RealLifeEvents
                .Include(x => x.Images)
                .OrderByDescending(x => x.Date)
                .ToListAsync();
            return View("~/Views/Admin/Events.cshtml", events);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin.Community.Events")]
        public async Task<IActionResult> SaveRealLifeEvent(int? id, string title, DateTime date, string? location, string? description, string? youTubeUrl, bool isUpcoming, IFormFile? image, bool removeImage = false)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["AdminMessage"] = "Titel ist erforderlich.";
                return RedirectToAction(nameof(Events));
            }

            var normalizedYouTubeUrl = string.IsNullOrWhiteSpace(youTubeUrl) ? null : youTubeUrl.Trim();
            Uri? parsedUrl = null;
            if (!string.IsNullOrWhiteSpace(normalizedYouTubeUrl)
                && !Uri.TryCreate(normalizedYouTubeUrl, UriKind.Absolute, out parsedUrl))
            {
                TempData["AdminMessage"] = "Der YouTube-Link ist ungültig.";
                return RedirectToAction(nameof(Events));
            }

            if (normalizedYouTubeUrl is not null
                && parsedUrl is not null
                && !parsedUrl.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)
                && !parsedUrl.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
            {
                TempData["AdminMessage"] = "Bitte nur YouTube-Links eintragen.";
                return RedirectToAction(nameof(Events));
            }

            RealLifeEvent? entity = id.HasValue && id.Value > 0
                ? await _db.RealLifeEvents.FindAsync(id.Value)
                : null;
            var isNew = entity is null;

            if (entity is null)
            {
                entity = new RealLifeEvent();
                _db.RealLifeEvents.Add(entity);
            }

            entity.Title = title.Trim();
            entity.Date = date;
            entity.Location = location?.Trim() ?? "";
            entity.Description = description?.Trim() ?? "";
            entity.YouTubeUrl = normalizedYouTubeUrl;
            entity.IsUpcoming = isUpcoming;

            if (removeImage && !string.IsNullOrEmpty(entity.ImageFileName))
            {
                _mediaService.TryDeleteEventImage(entity.ImageFileName);
                entity.ImageFileName = null;
            }

            if (image is { Length: > 0 })
            {
                var saved = await _mediaService.SaveEventImageAsync(image);
                if (saved is not null)
                {
                    if (!string.IsNullOrEmpty(entity.ImageFileName))
                        _mediaService.TryDeleteEventImage(entity.ImageFileName);
                    entity.ImageFileName = saved;
                }
            }

            await _db.SaveChangesAsync();
            if (isNew)
            {
                await _audit.LogAsync("SaveRealLifeEvent", "RealLifeEvent", entity.Id.ToString(), $"Title={entity.Title}");
            }

            TempData["AdminMessage"] = "Event gespeichert.";
            return RedirectToAction(nameof(Events));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin.Community.Events")]
        public async Task<IActionResult> DeleteRealLifeEvent(int id)
        {
            var entity = await _db.RealLifeEvents
                .Include(x => x.Images)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                TempData["AdminMessage"] = "Event nicht gefunden.";
                return RedirectToAction(nameof(Events));
            }

            if (!string.IsNullOrWhiteSpace(entity.ImageFileName))
            {
                _mediaService.TryDeleteEventImage(entity.ImageFileName);
            }

            foreach (var img in entity.Images)
            {
                if (!string.IsNullOrWhiteSpace(img.FileName))
                {
                    _mediaService.TryDeleteEventImage(img.FileName);
                }
            }

            _db.RealLifeEvents.Remove(entity);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("DeleteRealLifeEvent", "RealLifeEvent", id.ToString(), $"Title={entity.Title}");
            TempData["AdminMessage"] = "Event gelöscht.";
            return RedirectToAction(nameof(Events));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin.Community.Events")]
        public async Task<IActionResult> AddEventImages(int eventId, List<IFormFile>? images)
        {
            var ev = await _db.RealLifeEvents
                .AsTracking()
                .Include(x => x.Images)
                .FirstOrDefaultAsync(x => x.Id == eventId);

            if (ev is null)
            {
                TempData["AdminMessage"] = "Event nicht gefunden.";
                return RedirectToAction(nameof(Events));
            }

            var files = images?
                .Where(x => x is { Length: > 0 })
                .ToList() ?? new List<IFormFile>();

            if (files.Count == 0)
            {
                TempData["AdminMessage"] = "Bitte mindestens ein Bild auswählen.";
                return RedirectToAction(nameof(Events));
            }

            var added = 0;
            foreach (var file in files)
            {
                var saved = await _mediaService.SaveEventImageAsync(file);
                if (saved is null) continue;

                ev.Images.Add(new RealLifeEventImage
                {
                    EventId = ev.Id,
                    FileName = saved,
                    UploadedAt = DateTime.UtcNow
                });
                added++;
            }

            if (added > 0)
            {
                await _db.SaveChangesAsync();
                await _audit.LogAsync("AddEventImages", "RealLifeEvent", ev.Id.ToString(), $"Title={ev.Title}, Added={added}");
                TempData["AdminMessage"] = $"{added} Bild(er) hochgeladen.";
            }
            else
            {
                TempData["AdminMessage"] = "Es konnten keine Bilder gespeichert werden.";
            }

            return RedirectToAction(nameof(Events));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin.Community.Events")]
        public async Task<IActionResult> DeleteEventImage(int imageId, int eventId)
        {
            var image = await _db.RealLifeEventImages.FindAsync(imageId);
            if (image is null)
            {
                TempData["AdminMessage"] = "Bild nicht gefunden.";
                return RedirectToAction(nameof(Events));
            }

            _mediaService.TryDeleteEventImage(image.FileName);
            _db.RealLifeEventImages.Remove(image);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("DeleteEventImage", "RealLifeEventImage", imageId.ToString(), $"EventId={eventId}, File={image.FileName}");
            TempData["AdminMessage"] = "Bild gelöscht.";
            return RedirectToAction(nameof(Events));
        }

        [HttpGet]
        [Authorize(Policy = "Admin.Community.Streams")]
        public async Task<IActionResult> StreamSchedules()
        {
            var schedules = await _db.StreamSchedules
                .OrderByDescending(x => x.IsRecurring)
                .ThenBy(x => x.IsRecurring ? x.DayOfWeek : null)
                .ThenBy(x => x.IsRecurring ? x.TimeOfDay : null)
                .ThenBy(x => x.StartAt)
                .ToListAsync();
            return View("~/Views/Admin/StreamSchedules.cshtml", schedules);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin.Community.Streams")]
        public async Task<IActionResult> SaveStreamSchedule(int? id, DateTime? startAt, int durationMinutes, string title, string? url, bool isRecurring = false, int? dayOfWeek = null, TimeSpan? timeOfDay = null)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["AdminMessage"] = "Titel ist erforderlich.";
                return RedirectToAction(nameof(StreamSchedules));
            }

            if (isRecurring)
            {
                if (!dayOfWeek.HasValue || dayOfWeek < 0 || dayOfWeek > 6)
                {
                    TempData["AdminMessage"] = "Bitte einen Wochentag auswählen.";
                    return RedirectToAction(nameof(StreamSchedules));
                }

                if (!timeOfDay.HasValue)
                {
                    TempData["AdminMessage"] = "Bitte eine feste Uhrzeit für den wiederkehrenden Stream angeben.";
                    return RedirectToAction(nameof(StreamSchedules));
                }
            }
            else if (!startAt.HasValue)
            {
                TempData["AdminMessage"] = "Bitte einen Startzeitpunkt für den spontanen Stream angeben.";
                return RedirectToAction(nameof(StreamSchedules));
            }

            var normalizedUrl = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedUrl) && !Uri.TryCreate(normalizedUrl, UriKind.Absolute, out _))
            {
                TempData["AdminMessage"] = "Die Stream-URL ist ungültig.";
                return RedirectToAction(nameof(StreamSchedules));
            }

            StreamSchedule? entity = id.HasValue && id.Value > 0
                ? await _db.StreamSchedules.FindAsync(id.Value)
                : null;

            if (entity is null)
            {
                entity = new StreamSchedule
                {
                    CreatedAt = DateTime.UtcNow
                };
                _db.StreamSchedules.Add(entity);
            }

            entity.IsRecurring = isRecurring;
            entity.DayOfWeek = isRecurring ? dayOfWeek : null;
            entity.TimeOfDay = isRecurring ? timeOfDay : null;
            entity.StartAt = isRecurring
                ? ComputeNextOccurrence(dayOfWeek!.Value, timeOfDay!.Value, DateTime.Now)
                : startAt!.Value;
            entity.DurationMinutes = Math.Max(1, durationMinutes);
            entity.Title = title.Trim();
            entity.Url = normalizedUrl;

            await _db.SaveChangesAsync();
            await _audit.LogAsync("SaveStreamSchedule", "StreamSchedule", entity.Id.ToString(), isRecurring
                ? $"Recurring={entity.DayOfWeek}@{entity.TimeOfDay}, Title={entity.Title}"
                : $"StartAt={entity.StartAt:yyyy-MM-dd HH:mm}, Title={entity.Title}");
            TempData["AdminMessage"] = isRecurring ? "Wiederkehrender Stream gespeichert." : "Spontaner Stream gespeichert.";
            await _webhookAuto.FireAsync(WebhookEvents.StreamScheduled, new()
            {
                ["Title"]   = entity.Title,
                ["Url"]     = entity.Url ?? "",
                ["StartAt"] = entity.StartAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
            });
            return RedirectToAction(nameof(StreamSchedules));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin.Community.Streams")]
        public async Task<IActionResult> DeleteStreamSchedule(int id)
        {
            var entity = await _db.StreamSchedules.FindAsync(id);
            if (entity is null) return RedirectToAction(nameof(StreamSchedules));

            _db.StreamSchedules.Remove(entity);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("DeleteStreamSchedule", "StreamSchedule", id.ToString(), $"Title={entity.Title}");
            TempData["AdminMessage"] = "Stream-Termin gelöscht.";
            return RedirectToAction(nameof(StreamSchedules));
        }

        private static DateTime ComputeNextOccurrence(int dayOfWeek, TimeSpan timeOfDay, DateTime from)
        {
            var daysUntil = ((dayOfWeek - (int)from.DayOfWeek) + 7) % 7;
            var candidate = from.Date.AddDays(daysUntil).Add(timeOfDay);
            if (candidate < from)
            {
                candidate = candidate.AddDays(7);
            }

            return candidate;
        }

        // ── Stewarding ────────────────────────────────────────────────────────────

        [HttpGet]
        [Authorize(Policy = "Admin.Community.Stewarding")]
        public async Task<IActionResult> Stewarding(string? leagueFilter = null, string? typeFilter = null)
        {
            var query = _db.LeaguePenalties.AsQueryable();

            if (!string.IsNullOrWhiteSpace(leagueFilter))
                query = query.Where(x => x.LeagueId == leagueFilter);
            if (!string.IsNullOrWhiteSpace(typeFilter))
                query = query.Where(x => x.PenaltyType == typeFilter);

            var penalties = await query
                .OrderByDescending(x => x.Date)
                .ToListAsync();

            var allLeagues = await _db.LeaguePenalties
                .Select(x => x.LeagueId)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            ViewBag.Penalties    = penalties;
            ViewBag.AllLeagues   = allLeagues;
            ViewBag.LeagueFilter = leagueFilter ?? "";
            ViewBag.TypeFilter   = typeFilter   ?? "";

            var drivers = await _db.DriverStandings
                .Where(x => !string.IsNullOrEmpty(x.Driver))
                .Select(x => x.Driver)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            var leagues = await _db.Leagues
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Name })
                .ToListAsync();

            ViewBag.DriverList = drivers;
            ViewBag.LeagueList = leagues.Select(l => new { l.Id, l.Name }).ToList<dynamic>();

            return View("~/Views/Admin/Stewarding.cshtml");
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin.Community.Stewarding")]
        public async Task<IActionResult> SavePenalty(
            int? id,
            string leagueId,
            DateTime date,
            string driver,
            string penaltyType,
            int points,
            string? raceTrack,
            string? secondDriver,
            string? incident,
            string reason,
            bool isPublic = true)
        {
            if (string.IsNullOrWhiteSpace(leagueId) || string.IsNullOrWhiteSpace(driver) || string.IsNullOrWhiteSpace(reason))
            {
                TempData["AdminMessage"] = "Liga, Fahrer und Begründung sind Pflichtfelder.";
                return RedirectToAction(nameof(Stewarding));
            }

            LeaguePenalty? entity = id.HasValue && id.Value > 0
                ? await _db.LeaguePenalties.FindAsync(id.Value)
                : null;

            var isNew = entity is null;
            if (entity is null)
            {
                entity = new LeaguePenalty
                {
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = User.Identity?.Name ?? "Admin"
                };
                _db.LeaguePenalties.Add(entity);
            }

            entity.LeagueId     = leagueId.Trim();
            entity.Date         = date;
            entity.Driver       = driver.Trim();
            entity.PenaltyType  = penaltyType.Trim();
            entity.Points       = points;
            entity.RaceTrack    = string.IsNullOrWhiteSpace(raceTrack) ? null : raceTrack.Trim();
            entity.SecondDriver = string.IsNullOrWhiteSpace(secondDriver) ? null : secondDriver.Trim();
            entity.Incident     = string.IsNullOrWhiteSpace(incident) ? null : incident.Trim();
            entity.Reason       = reason.Trim();
            entity.IsPublic     = isPublic;

            await _db.SaveChangesAsync();
            await _audit.LogAsync(
                isNew ? "CreatePenalty" : "UpdatePenalty",
                "LeaguePenalty",
                entity.Id.ToString(),
                $"League={entity.LeagueId}, Driver={entity.Driver}, Type={entity.PenaltyType}, Points={entity.Points}, Public={entity.IsPublic}");

            // Punkteabzug wirkt direkt auf die Tabelle → Liga neu berechnen.
            await RecalculateAfterPenaltyAsync(entity.LeagueId);

            TempData["AdminMessage"] = isNew ? "Strafe gespeichert." : "Strafe aktualisiert.";
            if (isNew && isPublic)
                await _webhookAuto.FireAsync(WebhookEvents.PenaltySaved, new()
                {
                    ["Driver"]      = entity.Driver,
                    ["SecondDriver"]= entity.SecondDriver ?? "",
                    ["PenaltyType"] = entity.PenaltyType,
                    ["Points"]      = entity.Points.ToString(),
                    ["RaceTrack"]   = entity.RaceTrack ?? "",
                    ["League"]      = entity.LeagueId,
                    ["Date"]        = entity.Date.ToString("dd.MM.yyyy"),
                    ["Reason"]      = entity.Reason,
                });
            return RedirectToAction(nameof(Stewarding));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin.Community.Stewarding")]
        public async Task<IActionResult> DeletePenalty(int id)
        {
            var entity = await _db.LeaguePenalties.FindAsync(id);
            if (entity is null)
            {
                TempData["AdminMessage"] = "Strafe nicht gefunden.";
                return RedirectToAction(nameof(Stewarding));
            }

            var penaltyLeagueId = entity.LeagueId;
            _db.LeaguePenalties.Remove(entity);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("DeletePenalty", "LeaguePenalty", id.ToString(),
                $"League={entity.LeagueId}, Driver={entity.Driver}, Type={entity.PenaltyType}");

            // Entfernter Punkteabzug → Liga neu berechnen.
            await RecalculateAfterPenaltyAsync(penaltyLeagueId);

            TempData["AdminMessage"] = "Strafe gelöscht.";
            return RedirectToAction(nameof(Stewarding));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin.Community.Stewarding")]
        public async Task<IActionResult> TogglePenaltyPublic(int id)
        {
            var entity = await _db.LeaguePenalties.FindAsync(id);
            if (entity is null) return RedirectToAction(nameof(Stewarding));

            entity.IsPublic = !entity.IsPublic;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("TogglePenaltyPublic", "LeaguePenalty", id.ToString(),
                $"IsPublic={entity.IsPublic}");

            TempData["AdminMessage"] = entity.IsPublic ? "Strafe ist jetzt öffentlich." : "Strafe ist jetzt intern.";
            return RedirectToAction(nameof(Stewarding));
        }
    }
}
