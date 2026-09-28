using Erdi_ERC.Data;
using Erdi_ERC.Helpers;
using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erdi_ERC.Controllers
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
        /// Maximale Länge der Begründung eines Stewarding-Dokuments. Muss zu
        /// <c>LeaguePenalties.Reason</c> (varchar(1024)) und dem maxlength der Formulare passen.
        /// </summary>
        private const int MaxPenaltyReasonLength = 1024;

        /// <summary>
        /// Maximale Länge des Video-Links eines Events. Muss zu
        /// <c>RealLifeEvent.YouTubeUrl</c> (varchar(512)) und dem maxlength der Formulare
        /// passen: ohne diese Prüfung läuft ein zu langer POST in einen MySQL-Fehler (500)
        /// statt in die freundliche Meldung — das maxlength im Formular gilt nur im Browser.
        /// </summary>
        private const int MaxVideoUrlLength = 512;

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
            // Nur http(s): der Link steht öffentlich als href (siehe VideoEmbedHelper.SafeLinkOrNull).
            var safeUrl = VideoEmbedHelper.SafeLinkOrNull(url);
            if (string.IsNullOrWhiteSpace(title) || safeUrl is null)
            {
                TempData["AdminMessage"] = "Titel und ein vollständiger Link mit https:// sind für Highlights erforderlich.";
                return RedirectToAction(nameof(Hub));
            }

            var submittedById = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var submittedByName = User.Identity?.Name ?? "Admin";
            await _contentService.SaveHighlightClipAsync(title, safeUrl, category, raceLabel, submittedById, submittedByName);
            await _webhookAuto.FireAsync(WebhookEvents.HighlightApproved, new()
            {
                ["Title"]     = title,
                ["Url"]       = safeUrl,
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

        /// <summary>
        /// Legt ein Real-Life-Event an oder aktualisiert es. Der Video-Link ist optional; ist
        /// er gesetzt, muss er ein Link eines bekannten Anbieters (YouTube oder Twitch) sein.
        /// </summary>
        /// <param name="videoUrl">
        /// Video-Link des Events. Die Spalte heißt historisch <c>YouTubeUrl</c> und nimmt seit
        /// 2026-09-25 auch Twitch-Clips und -VODs auf — erkannt über
        /// <see cref="VideoEmbedHelper.DetectPlatform"/>.
        /// </param>
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin.Community.Events")]
        public async Task<IActionResult> SaveRealLifeEvent(int? id, string title, DateTime date, string? location, string? description, string? videoUrl, bool isUpcoming, IFormFile? image, bool removeImage = false)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["AdminMessage"] = "Titel ist erforderlich.";
                return RedirectToAction(nameof(Events));
            }

            var normalizedVideoUrl = string.IsNullOrWhiteSpace(videoUrl) ? null : videoUrl.Trim();
            if (normalizedVideoUrl is { Length: > MaxVideoUrlLength })
            {
                TempData["AdminMessage"] = $"Der Video-Link ist zu lang (max. {MaxVideoUrlLength} Zeichen).";
                return RedirectToAction(nameof(Events));
            }

            if (normalizedVideoUrl is not null && VideoEmbedHelper.DetectPlatform(normalizedVideoUrl) is null)
            {
                TempData["AdminMessage"] = "Bitte nur YouTube- oder Twitch-Links eintragen — vollständige URL (http:// oder https://).";
                return RedirectToAction(nameof(Events));
            }

            RealLifeEvent? entity = id.HasValue && id.Value > 0
                ? await _db.RealLifeEvents.AsTracking().FirstOrDefaultAsync(x => x.Id == id.Value)
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
            entity.YouTubeUrl = normalizedVideoUrl;
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
                await _audit.LogAndSaveAsync("SaveRealLifeEvent", "RealLifeEvent", entity.Id.ToString(), $"Title={entity.Title}");
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
            await _audit.LogAndSaveAsync("DeleteRealLifeEvent", "RealLifeEvent", id.ToString(), $"Title={entity.Title}");
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
                await _audit.LogAndSaveAsync("AddEventImages", "RealLifeEvent", ev.Id.ToString(), $"Title={ev.Title}, Added={added}");
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
            await _audit.LogAndSaveAsync("DeleteEventImage", "RealLifeEventImage", imageId.ToString(), $"EventId={eventId}, File={image.FileName}");
            TempData["AdminMessage"] = "Bild gelöscht.";
            return RedirectToAction(nameof(Events));
        }

        // ── Giveaways (Infotafel-Startseite) ───────────────────────────────────────

        [HttpGet]
        [Authorize(Policy = "Admin.Community.Giveaways")]
        public async Task<IActionResult> Giveaways()
        {
            var giveaways = await _db.Giveaways
                .OrderByDescending(x => x.StartAt)
                .ToListAsync();
            return View("~/Views/Admin/Giveaways.cshtml", giveaways);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin.Community.Giveaways")]
        public async Task<IActionResult> SaveGiveaway(int? id, string title, string? description, string? prize, DateTime startAt, DateTime endAt, string? link)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["AdminMessage"] = "Titel ist erforderlich.";
                return RedirectToAction(nameof(Giveaways));
            }

            var normalizedLink = string.IsNullOrWhiteSpace(link) ? null : link.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedLink))
            {
                if (!Uri.TryCreate(normalizedLink, UriKind.Absolute, out var parsed)
                    || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
                {
                    TempData["AdminMessage"] = "Der Link ist ungültig — bitte eine http(s)-Adresse angeben.";
                    return RedirectToAction(nameof(Giveaways));
                }
                normalizedLink = parsed!.ToString();
            }

            if (endAt <= startAt)
            {
                TempData["AdminMessage"] = "Das Enddatum muss nach dem Startdatum liegen.";
                return RedirectToAction(nameof(Giveaways));
            }

            // AsTracking() ist hier zwingend: der DbContext läuft global mit
            // NoTrackingWithIdentityResolution (Program.cs), FindAsync liefert daher eine
            // detached Instanz — ohne Tracking würde SaveChangesAsync() beim Bearbeiten
            // eines Giveaways stillschweigend nichts schreiben.
            Giveaway? entity = id.HasValue && id.Value > 0
                ? await _db.Giveaways.AsTracking().FirstOrDefaultAsync(x => x.Id == id.Value)
                : null;
            var isNew = entity is null;

            if (entity is null)
            {
                entity = new Giveaway();
                _db.Giveaways.Add(entity);
            }

            entity.Title = title.Trim();
            entity.Description = description?.Trim() ?? "";
            entity.Prize = prize?.Trim() ?? "";
            // datetime-local liefert lokale Wanduhrzeit — genau so bleibt sie stehen.
            // (Keine UTC-Umrechnung: die Spalte wird projektweit als Wanduhrzeit gelesen,
            // siehe Docs/Features/Zeitzonen-Konvention.md.)
            entity.StartAt = DateTime.SpecifyKind(startAt, DateTimeKind.Unspecified);
            entity.EndAt = DateTime.SpecifyKind(endAt, DateTimeKind.Unspecified);
            entity.Link = normalizedLink;

            await _db.SaveChangesAsync();
            if (isNew)
            {
                await _audit.LogAndSaveAsync("SaveGiveaway", "Giveaway", entity.Id.ToString(), $"Title={entity.Title}, End={entity.EndAt:O}");
            }

            TempData["AdminMessage"] = "Giveaway gespeichert.";
            return RedirectToAction(nameof(Giveaways));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin.Community.Giveaways")]
        public async Task<IActionResult> DeleteGiveaway(int id)
        {
            var entity = await _db.Giveaways.FindAsync(id);
            if (entity is null)
            {
                TempData["AdminMessage"] = "Giveaway nicht gefunden.";
                return RedirectToAction(nameof(Giveaways));
            }

            _db.Giveaways.Remove(entity);
            await _db.SaveChangesAsync();
            await _audit.LogAndSaveAsync("DeleteGiveaway", "Giveaway", id.ToString(), $"Title={entity.Title}");
            TempData["AdminMessage"] = "Giveaway gelöscht.";
            return RedirectToAction(nameof(Giveaways));
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

            // AsTracking() ist hier zwingend: der DbContext läuft global mit
            // NoTrackingWithIdentityResolution (Program.cs), FindAsync liefert daher eine
            // detached Instanz — ohne Tracking würde SaveChangesAsync() beim Bearbeiten
            // eines Stream-Termins stillschweigend nichts schreiben.
            StreamSchedule? entity = id.HasValue && id.Value > 0
                ? await _db.StreamSchedules.AsTracking().FirstOrDefaultAsync(x => x.Id == id.Value)
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
            // Beide Zweige erzeugen Wanduhrzeit in Server-Lokalzeit (Kind=Unspecified).
            // Vorher stand hier SpecifyKind(..., Utc) bzw. eine Rechnung mit DateTime.UtcNow:
            // das Formular liefert aber lokale Zeit, dadurch lag der Termin im Sommer 2 h
            // daneben und der wiederkehrende Stream rutschte auf den falschen Wochentag
            // (siehe Docs/Features/Zeitzonen-Konvention.md).
            entity.StartAt = isRecurring
                ? StreamScheduleMath.ComputeNextOccurrence(dayOfWeek!.Value, timeOfDay!.Value, DateTime.Now)
                : DateTime.SpecifyKind(startAt!.Value, DateTimeKind.Unspecified);
            entity.DurationMinutes = Math.Max(1, durationMinutes);
            entity.Title = title.Trim();
            entity.Url = normalizedUrl;

            await _db.SaveChangesAsync();
            await _audit.LogAndSaveAsync("SaveStreamSchedule", "StreamSchedule", entity.Id.ToString(), isRecurring
                ? $"Recurring={entity.DayOfWeek}@{entity.TimeOfDay}, Title={entity.Title}"
                : $"StartAt={entity.StartAt:yyyy-MM-dd HH:mm}, Title={entity.Title}");
            TempData["AdminMessage"] = isRecurring ? "Wiederkehrender Stream gespeichert." : "Spontaner Stream gespeichert.";
            await _webhookAuto.FireAsync(WebhookEvents.StreamScheduled, new()
            {
                ["Title"]   = entity.Title,
                ["Url"]     = entity.Url ?? "",
                ["StartAt"] = entity.StartAt.ToString("dd.MM.yyyy HH:mm"),
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
            await _audit.LogAndSaveAsync("DeleteStreamSchedule", "StreamSchedule", id.ToString(), $"Title={entity.Title}");
            TempData["AdminMessage"] = "Stream-Termin gelöscht.";
            return RedirectToAction(nameof(StreamSchedules));
        }

        // ── Stewarding ────────────────────────────────────────────────────────────

        [HttpGet]
        [Authorize(Policy = "Admin.Community.Stewarding")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Stewarding(
            string? leagueFilter = null,
            string? typeFilter = null,
            string? driverLeagueFilter = null,
            // Fallback: SavePenalty redirectet mit "leagueId" (Body-Param), damit der
            // Datalist-Filter nach dem Speichern erhalten bleibt.
            string? leagueId = null)
        {
            // Wenn die Liga explizit per leagueId mitkommt (POST-Redirect),
            // uebernimm sie als driverLeagueFilter.
            if (string.IsNullOrWhiteSpace(driverLeagueFilter) && !string.IsNullOrWhiteSpace(leagueId))
                driverLeagueFilter = leagueId;

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
            ViewBag.DriverLeagueFilter = driverLeagueFilter ?? "";

            var leagues = await _db.Leagues
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Name })
                .ToListAsync();

            // Driver-Standings können optional auf eine Liga eingeschränkt werden, sodass
            // das Create-Formular nur die Fahrer der gewählten Liga anbietet.
            // Hinweis: string.Equals(..., StringComparison.OrdinalIgnoreCase) wird von Pomelo
            // MySQL nicht in SQL übersetzt → stattdessen ToLower()-Vergleich, der sauber zu
            // LOWER(col) = LOWER(@p) translatiert.
            var driverQuery = _db.DriverStandings
                .Where(x => !string.IsNullOrEmpty(x.Driver));
            if (!string.IsNullOrWhiteSpace(driverLeagueFilter))
            {
                var needle = driverLeagueFilter.ToLower();
                driverQuery = driverQuery.Where(x => x.LeagueId != null && x.LeagueId.ToLower() == needle);
            }

            var driverStandings = await driverQuery
                .Select(x => new { x.Driver, x.DriverNumber })
                .Distinct()
                .OrderBy(x => x.Driver)
                .ToListAsync();

            // Pro-Liga-Mapping für die Edit-Panels (jedes Dokument filtert auf seine eigene Liga).
            // Wir laden die gefilterten DriverStandings einmalig (serverseitig) und gruppieren
            // clientseitig. Ein korreliertes _db.DriverStandings.Where(...) innerhalb eines
            // Select-Ausdrucks erzeugt OUTER APPLY, das Pomelo MySQL nicht übersetzt.
            var allLeagueDriversRaw = await _db.DriverStandings
                .Where(d => d.Driver != null && d.Driver != "")
                .Select(d => new { d.LeagueId, d.Driver, d.DriverNumber })
                .Distinct()
                .OrderBy(d => d.Driver)
                .ToListAsync();

            var allDriversByLeague = allLeagueDriversRaw
                .GroupBy(d => d.LeagueId ?? "", StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    LeagueId = g.Key,
                    Drivers = g.ToList()
                })
                .OrderBy(x => x.LeagueId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ViewBag.DriverNumberMap = driverStandings
                .GroupBy(x => x.Driver, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().DriverNumber, StringComparer.OrdinalIgnoreCase);
            ViewBag.DriverList = driverStandings
                .Select(x => FormatDriverWithNumber(x.Driver, x.DriverNumber))
                .ToList();
            ViewBag.LeagueList = leagues.Select(l => new { l.Id, l.Name }).ToList<dynamic>();
            ViewBag.AllDriversByLeague = allDriversByLeague.ToDictionary(
                x => x.LeagueId ?? "",
                x => x.Drivers.Select(d => FormatDriverWithNumber(d.Driver, d.DriverNumber)).ToList(),
                StringComparer.OrdinalIgnoreCase);

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
            bool isBetweenTwoDrivers,
            string? incident,
            string reason,
            bool isPublic)
        {
            if (string.IsNullOrWhiteSpace(leagueId) || string.IsNullOrWhiteSpace(driver) || string.IsNullOrWhiteSpace(reason))
            {
                TempData["AdminMessage"] = "Liga, Fahrer und Begründung sind Pflichtfelder.";
                return RedirectToAction(nameof(Stewarding));
            }

            // Spiegelt LeaguePenalties.Reason = varchar(1024): ohne diesen Guard wirft ein
            // direkter POST mit mehr Zeichen einen MySQL-Truncation-Fehler (500) statt einer Meldung.
            if (reason.Trim().Length > MaxPenaltyReasonLength)
            {
                TempData["AdminMessage"] = $"Die Begründung ist zu lang (max. {MaxPenaltyReasonLength} Zeichen).";
                return RedirectToAction(nameof(Stewarding), new { leagueId = leagueId });
            }

            // Server-side Guard: der eingetragene Fahrer MUSS in den DriverStandings
            // der gewaehlten Liga existieren. Verhindert, dass ein User per direktem
            // POST oder Browser-Manipulation einen Fahrer aus einer fremden Liga
            // (oder einen Fantasienamen) eintragen kann. Der Driver-Select im UI
            // bietet zwar nur die Liga-Fahrer an, aber der Controller ist die
            // letzte Verteidigungslinie.
            var (driverNameCheck, _) = ParseDriverInput(driver);
            if (!await IsDriverInLeagueAsync(leagueId, driverNameCheck))
            {
                TempData["AdminMessage"] = $"Fahrer \"{driverNameCheck}\" gehoert nicht zur Liga \"{leagueId}\" oder existiert nicht. Bitte einen Fahrer der gewaehlten Liga auswaehlen.";
                return RedirectToAction(nameof(Stewarding), new { leagueId = leagueId });
            }
            if (!string.IsNullOrWhiteSpace(secondDriver))
            {
                var (secondNameCheck, _) = ParseDriverInput(secondDriver);
                if (!await IsDriverInLeagueAsync(leagueId, secondNameCheck))
                {
                    TempData["AdminMessage"] = $"Zweiter Fahrer \"{secondNameCheck}\" gehoert nicht zur Liga \"{leagueId}\" oder existiert nicht. Bitte einen Fahrer der gewaehlten Liga auswaehlen.";
                    return RedirectToAction(nameof(Stewarding), new { leagueId = leagueId });
                }
            }

            // AsTracking() ist hier zwingend: der DbContext läuft global mit
            // NoTrackingWithIdentityResolution (Program.cs), und FindAsync liefert eine
            // detached Instanz, sobald die Entität nicht schon im ChangeTracker liegt —
            // genau der Normalfall bei einem frischen Request. Ohne Tracking würde
            // SaveChangesAsync() beim Update stillschweigend nichts schreiben.
            LeaguePenalty? entity = id.HasValue && id.Value > 0
                ? await _db.LeaguePenalties.AsTracking().FirstOrDefaultAsync(x => x.Id == id.Value)
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

            var (driverName, driverNumber) = ParseDriverInput(driver);
            var (secondDriverName, secondDriverNumber) = ParseDriverInput(secondDriver);

            entity.LeagueId           = leagueId.Trim();
            entity.Date               = date;
            entity.Driver             = driverName;
            entity.DriverNumber       = driverNumber ?? await ResolveDriverNumberAsync(driverName);
            entity.PenaltyType        = penaltyType.Trim();
            entity.Points             = points;
            entity.RaceTrack          = string.IsNullOrWhiteSpace(raceTrack) ? null : raceTrack.Trim();
            entity.SecondDriver       = secondDriverName;
            entity.SecondDriverNumber = secondDriverNumber ?? await ResolveDriverNumberAsync(secondDriverName);
            entity.IsBetweenTwoDrivers= isBetweenTwoDrivers;
            entity.Incident           = string.IsNullOrWhiteSpace(incident) ? null : incident.Trim();
            entity.Reason             = reason.Trim();
            entity.IsPublic           = isPublic;

            // Strafpunkte-Gesamtstand: Nur beim ANLEGEN als Snapshot speichern (Stand des
            // Fahrers in dieser Liga, inkl. der Punkte dieses Dokuments). Bei Bearbeitung
            // eines Alt-Dokuments bleibt der Wert eingefroren — alte Berichte dürfen den
            // Wert nicht aktualisieren (User-Anforderung). entity ist beim Anlegen zwar
            // getrackt, aber noch nicht gespeichert → fließt nicht in die Summen-Query ein.
            if (isNew)
            {
                var prior = await _db.LeaguePenalties
                    .Where(x => x.LeagueId == entity.LeagueId && x.Driver == entity.Driver)
                    .SumAsync(x => (int?)x.Points) ?? 0;
                entity.DriverPointsTotal = prior + entity.Points;
            }

            await _db.SaveChangesAsync();
            await _audit.LogAndSaveAsync(
                isNew ? "CreatePenalty" : "UpdatePenalty",
                "LeaguePenalty",
                entity.Id.ToString(),
                $"League={entity.LeagueId}, Driver={entity.Driver}, Type={entity.PenaltyType}, Points={entity.Points}, Public={entity.IsPublic}");

            // Zeitstrafe + Strafpunkte / Punkteabzug wirkt direkt auf die Tabelle → Liga neu berechnen.
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
            // leagueId mitgeben, damit der Liga-Filter im Formular nach dem Speichern
            // nicht zurückspringt (wie auf den Fehlerpfaden oben).
            return RedirectToAction(nameof(Stewarding), new { leagueId = entity.LeagueId });
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
            await _audit.LogAndSaveAsync("DeletePenalty", "LeaguePenalty", id.ToString(),
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
            // Pessimistisches UPDATE via raw SQL, um Change-Tracker-Eigenheiten
            // (z. B. bei getrackten Entities aus vorangegangenen Queries im selben
            // Request-Scope) sicher zu umgehen. Das ExecuteSqlInterpolated nutzt
            // parameterisierte Queries — kein SQL-Injection-Risiko.
            var before = await _db.LeaguePenalties
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => x.IsPublic)
                .FirstOrDefaultAsync();

            var newValue = !before;
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE LeaguePenalties SET IsPublic = {newValue} WHERE Id = {id}");

            await _audit.LogAndSaveAsync("TogglePenaltyPublic", "LeaguePenalty", id.ToString(),
                $"Before={before}, After={newValue}");

            TempData["AdminMessage"] = newValue ? "Strafe ist jetzt öffentlich." : "Strafe ist jetzt intern.";
            // Cache-Buster: bfcache speichert HTML unter URL → neue URL erzwingt Frische.
            return RedirectToAction(nameof(Stewarding), new { ts = Guid.NewGuid().ToString("N") });
        }

        // ── Stewarding Helpers ────────────────────────────────────────────────────

        private static string FormatDriverWithNumber(string driver, int? number) =>
            number.HasValue ? $"{number.Value} - {driver}" : driver;

        private static (string Name, int? Number) ParseDriverInput(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return (string.Empty, null);
            var trimmed = input.Trim();

            // Format "16 - Charles Leclerc" → Name = "Charles Leclerc", Number = 16
            var dashIdx = trimmed.IndexOf(" - ");
            if (dashIdx > 0 && int.TryParse(trimmed[..dashIdx], out var parsedNumber))
            {
                var name = trimmed[(dashIdx + 3)..].Trim();
                return (name, parsedNumber);
            }

            return (trimmed, null);
        }

        private async Task<int?> ResolveDriverNumberAsync(string? driverName)
        {
            if (string.IsNullOrWhiteSpace(driverName)) return null;
            var normalized = driverName.Trim();
            var standing = await _db.DriverStandings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Driver == normalized);
            return standing?.DriverNumber;
        }

        /// <summary>
        /// Prueft, ob ein Fahrername in der angegebenen Liga in den DriverStandings
        /// existiert. Wird in SavePenalty als harter Server-Side-Guard benutzt,
        /// damit niemand einen Fahrer einer fremden Liga (oder einen Fantasienamen)
        /// eintragen kann — selbst wenn das Form-Frontend umgangen wird.
        /// </summary>
        private async Task<bool> IsDriverInLeagueAsync(string? leagueId, string? driverName)
        {
            if (string.IsNullOrWhiteSpace(leagueId) || string.IsNullOrWhiteSpace(driverName)) return false;
            var l = leagueId.Trim();
            var d = driverName.Trim();
            return await _db.DriverStandings
                .AsNoTracking()
                .AnyAsync(s => s.Driver != null
                            && s.Driver.ToLower() == d.ToLower()
                            && s.LeagueId != null
                            && s.LeagueId.ToLower() == l.ToLower());
        }
    }
}
