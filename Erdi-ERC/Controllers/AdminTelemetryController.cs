using Erdi_ERC.Data;
using Erdi_ERC.Helpers;
using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Erdi_ERC.Controllers;

/// <summary>
/// Telemetrie-Review-Inbox: eingehende Rennergebnisse der Telemetrie-App (Pending-Entwürfe)
/// prüfen und korrigieren, als finales Rennen übernehmen (Accept) oder ablehnen (Reject).
/// Dazu die Verwaltung der persönlichen Sende-Keys pro Fahrer (Keys-Ansicht).
/// Index/Detail/Keys: grundsätzlich Liga-Admin. Accept/Reject/Key-Änderungen greifen ins
/// Renn-Geschehen ein → zusätzlich "Admin.League.Races".
/// </summary>
[Authorize(Policy = "Admin.League")]
public class AdminTelemetryController : Controller
{
    private readonly AppDbContext _db;
    private readonly ITelemetryKeyService _keys;
    private readonly IPendingRacePromotionService _promotion;

    public AdminTelemetryController(AppDbContext db, ITelemetryKeyService keys, IPendingRacePromotionService promotion)
    {
        _db = db;
        _keys = keys;
        _promotion = promotion;
    }

    // ── Inbox ──────────────────────────────────────────────────────────────────

    [HttpGet("/admin/telemetry")]
    public async Task<IActionResult> Index(string? status, int page = 1)
    {
        ViewData["Title"] = "Telemetrie";
        ViewData["Kicker"] = "Liga";
        ViewData["HeroTitle"] = "Telemetrie-Eingänge";

        ViewBag.StatusFilter = status;
        ViewBag.Page = page;

        PendingRaceStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<PendingRaceStatus>(status, ignoreCase: true, out var parsed))
        {
            statusFilter = parsed;
        }

        const int PageSize = 25;
        var skip = (Math.Max(1, page) - 1) * PageSize;

        var query = _db.PendingRaceResults.AsNoTracking();
        if (statusFilter.HasValue)
        {
            query = query.Where(p => p.Status == (int)statusFilter.Value);
        }

        var items = await query
            .OrderByDescending(p => p.ReceivedAt)
            .Skip(skip)
            .Take(PageSize)
            .Select(p => new TelemetryListItem
            {
                Id = p.Id,
                SourceTrack = p.SourceTrack,
                SourceDate = p.SourceDate,
                SourceSeason = p.SourceSeason,
                SourceLeague = p.SourceLeague,
                SenderDiscordId = p.SenderDiscordId,
                ReceivedAt = p.ReceivedAt,
                Status = p.Status,
                FinishCount = p.Finishes.Count,
                ReviewNote = p.ReviewNote,
            })
            .ToListAsync(HttpContext.RequestAborted);

        // Sender-Anzeigenamen in einem Batch auflösen (kein N+1).
        var senderIds = items
            .Select(i => i.SenderDiscordId)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .ToList();
        var senderMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (senderIds.Count > 0)
        {
            var profiles = await _db.DriverProfiles.AsNoTracking()
                .Where(p => senderIds.Contains(p.DiscordId))
                .Select(p => new { p.DiscordId, p.DiscordName, p.DisplayName })
                .ToListAsync(HttpContext.RequestAborted);
            foreach (var p in profiles)
            {
                senderMap[p.DiscordId] = p.DisplayName ?? p.DiscordName;
            }
        }

        ViewBag.Items = items;
        ViewBag.SenderMap = senderMap;
        ViewBag.HasMore = items.Count == PageSize;

        return View("~/Views/Admin/Telemetry/Index.cshtml");
    }

    [HttpGet("/admin/telemetry/detail/{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var pending = await _db.PendingRaceResults
            .AsNoTracking()
            .Include(p => p.Finishes)
            .FirstOrDefaultAsync(p => p.Id == id, HttpContext.RequestAborted);
        if (pending is null) return NotFound();

        pending.Finishes = pending.Finishes
            .OrderBy(f => f.Position == 0 ? int.MaxValue : f.Position)
            .ThenBy(f => f.Driver, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var vm = new TelemetryDetailViewModel
        {
            Pending = pending,
            SenderName = await ResolveProfileNameAsync(pending.SenderDiscordId, HttpContext.RequestAborted),
            DeciderName = await ResolveAdminNameAsync(pending.DecidedByDiscordId, HttpContext.RequestAborted),
            Leagues = await _db.Leagues.AsNoTracking()
                .Where(l => !l.IsArchived)
                .OrderBy(l => l.SortOrder)
                .ThenBy(l => l.Name)
                .ToListAsync(HttpContext.RequestAborted),
            // Reserven/Gäste stammen aus dem Roh-Payload (Reprozessierung für die Anzeige).
            Parsed = TelemetryRaceParser.Parse(pending.SourcePayload).Result,
            // Faktor-Vorschlag aus der gemeldeten Distanz; ohne Distanzangabe bleibt es bei 100 %.
            SuggestedPointsPercent = RacePointsFactor.DeriveFromDistance(pending.TotalLaps, pending.CompletedLaps),
            CompletedPercent = RacePointsFactor.PercentCompleted(pending.TotalLaps, pending.CompletedLaps),
        };

        return View("~/Views/Admin/Telemetry/Detail.cshtml", vm);
    }

    // ── Accept / Reject ────────────────────────────────────────────────────────

    [HttpPost("/admin/telemetry/accept")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("forms")]
    [Authorize(Policy = "Admin.League.Races")]
    public async Task<IActionResult> Accept(TelemetryAcceptInput input)
    {
        var admin = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";

        if (input is null || string.IsNullOrWhiteSpace(input.Track) || string.IsNullOrWhiteSpace(input.LeagueId))
        {
            TempData["AdminMessage"] = "Liga und Strecke sind erforderlich.";
            return RedirectToAction(nameof(Detail), new { id = input?.PendingId ?? 0 });
        }

        var finishes = input.Finishes
            .Where(f => !string.IsNullOrWhiteSpace(f.Driver))
            .Select(f => new PendingRaceFinishEdit(
                f.Position, f.Driver.Trim(), f.IsDnf, f.RaceTimeMs, f.QualifyingPosition, f.FastestLap))
            .ToList();

        // Datums-Feld geleert (datetime-local liefert dann default) → auf „jetzt" ausweichen,
        // damit RaceResult.Date nie auf 0001-01-01 fällt. DateTime.Now (nicht UtcNow), weil
        // RaceResult.Date projektweit Wanduhrzeit ist — sonst trüge dieselbe Spalte zwei
        // Semantiken (siehe Docs/Features/Zeitzonen-Konvention.md).
        var date = input.Date == default(DateTime) ? DateTime.Now : input.Date;

        var result = await _promotion.PromoteAsync(
            input.PendingId, input.LeagueId, input.Track, date, input.Season,
            finishes, input.FastestLapDriver, admin,
            RacePointsFactor.Normalize(input.PointsPercent), HttpContext.RequestAborted);

        TempData["AdminMessage"] = result.Ok
            ? $"Ergebnis als Rennen übernommen (Race #{result.RaceResultId}). Tabellen neu berechnet."
            : result.Error ?? "Übernahme fehlgeschlagen.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/admin/telemetry/reject")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("forms")]
    [Authorize(Policy = "Admin.League.Races")]
    public async Task<IActionResult> Reject(int id, string? note)
    {
        var admin = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
        var (ok, error) = await _promotion.RejectAsync(id, note, admin, HttpContext.RequestAborted);

        TempData["AdminMessage"] = ok
            ? "Ergebnis-Entwurf abgelehnt."
            : error ?? "Ablehnung fehlgeschlagen.";
        return RedirectToAction(nameof(Index));
    }

    // ── Sende-Keys ─────────────────────────────────────────────────────────────

    [HttpGet("/admin/telemetry/keys")]
    public async Task<IActionResult> Keys()
    {
        ViewData["Title"] = "Sende-Keys";
        ViewData["Kicker"] = "Telemetrie";
        ViewData["HeroTitle"] = "Telemetrie-Sende-Keys";

        ViewBag.Keys = await _keys.ListAsync(HttpContext.RequestAborted);
        ViewBag.Profiles = await _db.DriverProfiles
            .AsNoTracking()
            .Include(p => p.GamerTags)
            .OrderBy(p => p.DiscordName)
            .ToListAsync(HttpContext.RequestAborted);

        // Einmalige Klartext-Anzeige (TempData ist exactly-once).
        ViewBag.PlainKey = TempData["TelemetryKeyPlain"] as string;
        ViewBag.PlainKeyOwner = TempData["TelemetryKeyOwner"] as string;

        return View("~/Views/Admin/Telemetry/Keys.cshtml");
    }

    [HttpPost("/admin/telemetry/keys/generate")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("forms")]
    [Authorize(Policy = "Admin.League.Races")]
    public async Task<IActionResult> GenerateKey(string discordId, string? description)
    {
        if (string.IsNullOrWhiteSpace(discordId))
        {
            TempData["AdminMessage"] = "Bitte einen Fahrer auswählen.";
            return RedirectToAction(nameof(Keys));
        }

        var result = await _keys.GenerateAsync(discordId.Trim(), description, HttpContext.RequestAborted);
        if (!result.Ok)
        {
            TempData["AdminMessage"] = result.Error ?? "Key-Erzeugung fehlgeschlagen.";
            return RedirectToAction(nameof(Keys));
        }

        TempData["TelemetryKeyPlain"] = result.PlainKey;
        TempData["TelemetryKeyOwner"] = discordId.Trim();
        return RedirectToAction(nameof(Keys));
    }

    [HttpPost("/admin/telemetry/keys/revoke")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("forms")]
    [Authorize(Policy = "Admin.League.Races")]
    public async Task<IActionResult> RevokeKey(int id)
    {
        var actor = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
        await _keys.RevokeAsync(id, actor, HttpContext.RequestAborted);
        TempData["AdminMessage"] = "Key gesperrt. Die App kann damit keine Ergebnisse mehr senden.";
        return RedirectToAction(nameof(Keys));
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<string?> ResolveProfileNameAsync(string? discordId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(discordId)) return null;
        var p = await _db.DriverProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DiscordId == discordId, ct);
        return p is null ? null : (p.DisplayName ?? p.DiscordName);
    }

    private async Task<string?> ResolveAdminNameAsync(string? discordId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(discordId)) return null;
        var a = await _db.AdminUsers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DiscordId == discordId, ct);
        return a?.DisplayName ?? discordId;
    }
}
