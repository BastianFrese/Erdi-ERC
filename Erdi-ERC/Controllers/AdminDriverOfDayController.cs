using System.Security.Claims;
using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erdi_ERC.Controllers;

/// <summary>
/// Verwaltung der Driver-of-the-Day-Wahl: öffnen, schließen, verwerfen.
/// Nur Admins mit Community-Hub-Recht; die Overlay-Seite selbst ist öffentlich.
/// </summary>
[Authorize(Policy = "Admin.Community.Hub")]
[Route("admin/driver-of-day")]
public class AdminDriverOfDayController : Controller
{
    private readonly AppDbContext _db;
    private readonly IDotdVoteService _dotd;
    private readonly IAdminAuditService _audit;

    public AdminDriverOfDayController(
        AppDbContext db, IDotdVoteService dotd, IAdminAuditService audit)
    {
        _db = db;
        _dotd = dotd;
        _audit = audit;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var leagues = await _db.Leagues.AsNoTracking()
            .OrderBy(league => league.SortOrder)
            .ThenBy(league => league.Name)
            .ToListAsync();

        var history = await _db.DotdVotes.AsNoTracking()
            .Where(vote => vote.ClosedAt != null)
            .OrderByDescending(vote => vote.ClosedAt)
            .ThenByDescending(vote => vote.Id)
            .Take(10)
            .ToListAsync();

        var model = new DotdAdminViewModel
        {
            Leagues = leagues,
            Active = await _dotd.GetActiveAsync(),
            History = history,
            Rejections = _dotd.RecentRejections
        };

        return View("~/Views/Admin/DriverOfDay.cshtml", model);
    }

    [HttpPost("open")]
    public async Task<IActionResult> Open(string leagueId)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _dotd.OpenAsync(leagueId, adminId);

        if (result.Ok)
        {
            await _audit.LogAndSaveAsync(
                "DotdVoteOpen", "DotdVote", result.VoteId?.ToString() ?? string.Empty,
                result.Details ?? string.Empty);
            TempData["AdminMessage"] = "Wahl geöffnet.";
        }
        else
        {
            TempData["AdminMessage"] = result.Error;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("close")]
    public async Task<IActionResult> Close()
    {
        var result = await _dotd.CloseAsync();

        if (result.Ok)
        {
            await _audit.LogAndSaveAsync(
                "DotdVoteClose", "DotdVote", result.VoteId?.ToString() ?? string.Empty,
                result.Details ?? string.Empty);
            TempData["AdminMessage"] = "Wahl geschlossen.";
        }
        else
        {
            TempData["AdminMessage"] = result.Error;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("discard")]
    public async Task<IActionResult> Discard()
    {
        var result = await _dotd.DiscardAsync();

        if (result.Ok)
        {
            await _audit.LogAndSaveAsync(
                "DotdVoteVoid", "DotdVote", result.VoteId?.ToString() ?? string.Empty,
                result.Details ?? string.Empty);
            TempData["AdminMessage"] = "Wahl verworfen.";
        }
        else
        {
            TempData["AdminMessage"] = result.Error;
        }

        return RedirectToAction(nameof(Index));
    }
}
