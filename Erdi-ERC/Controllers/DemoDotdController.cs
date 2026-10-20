using System.Diagnostics;
using Erdi_ERC.Data;
using Erdi_ERC.Helpers;
using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erdi_ERC.Controllers;

/// <summary>
/// Nur für die lokale Entwicklung: öffnet eine DOTD-Wahl mit Dummy-Stimmen, damit
/// das Overlay in Streamlabs/OBS ohne echten Chat geprüft werden kann.
/// In Production antworten alle Routen mit 404.
/// </summary>
[AllowAnonymous]
[Route("overlay/dotd/demo")]
public class DemoDotdController : Controller
{
    private static readonly int[] Weights = { 10, 7, 5, 4, 3, 2, 1, 1 };

    private readonly AppDbContext _db;
    private readonly IDotdVoteService _dotd;
    private readonly IWebHostEnvironment _environment;

    public DemoDotdController(
        AppDbContext db, IDotdVoteService dotd, IWebHostEnvironment environment)
    {
        _db = db;
        _dotd = dotd;
        _environment = environment;
    }

    /// <summary>Öffnet eine Demo-Wahl und verteilt gewichtete Dummy-Stimmen.</summary>
    [HttpGet("")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Seed(CancellationToken ct)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var (league, drivers) = await LoadLeagueAsync(8, ct);
        if (league is null)
        {
            return Content("Keine Liga mit Fahrern in der Dev-DB gefunden.");
        }

        if (await _dotd.GetActiveAsync(ct) is not null)
        {
            await _dotd.DiscardAsync(ct);
        }

        var open = await _dotd.OpenAsync(league.Id, "demo", ct);
        if (!open.Ok)
        {
            return Content(open.Error ?? "Wahl konnte nicht geöffnet werden.");
        }

        var total = 0;
        for (var i = 0; i < drivers.Count && i < Weights.Length; i++)
        {
            for (var vote = 0; vote < Weights[i]; vote++)
            {
                await _dotd.RegisterVoteAsync(
                    $"demo-{i}-{vote}", drivers[i], "Demo", ct);
                total++;
            }
        }

        return Json(new
        {
            ok = true,
            league = league.Name,
            drivers = drivers.Select(DriverNameHelper.Display),
            totalVotes = total,
            overlay = "/overlay/dotd"
        });
    }

    /// <summary>
    /// Lasttest: öffnet eine frische Wahl und registriert <paramref name="count"/>
    /// Stimmen (Standard 120) über die echte Service-/DB-Kette.
    /// </summary>
    [HttpGet("burst")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Burst(int count = 120, CancellationToken ct = default)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        count = Math.Clamp(count, 1, 1000);

        var (league, drivers) = await LoadLeagueAsync(40, ct);
        if (league is null || drivers.Count == 0)
        {
            return Content("Keine Liga mit Fahrern in der Dev-DB gefunden.");
        }

        if (await _dotd.GetActiveAsync(ct) is not null)
        {
            await _dotd.DiscardAsync(ct);
        }

        var open = await _dotd.OpenAsync(league.Id, "demo", ct);
        if (!open.Ok)
        {
            return Content(open.Error ?? "Wahl konnte nicht geöffnet werden.");
        }

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < count; i++)
        {
            await _dotd.RegisterVoteAsync(
                $"burst-{i}", drivers[i % drivers.Count], $"Zuschauer{i + 1}", ct);
        }
        stopwatch.Stop();

        return Json(new
        {
            ok = true,
            league = league.Name,
            votes = count,
            drivers = drivers.Count,
            elapsedMs = stopwatch.ElapsedMilliseconds,
            perVoteMs = Math.Round(
                stopwatch.Elapsed.TotalMilliseconds / count, 2),
            overlay = "/overlay/dotd"
        });
    }

    /// <summary>Schließt die Demo-Wahl und zeigt die Auflösung.</summary>
    [HttpGet("close")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Close(CancellationToken ct)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var result = await _dotd.CloseAsync(ct);
        return Json(new { ok = result.Ok, error = result.Error, details = result.Details });
    }

    private async Task<(League? League, List<string> Drivers)> LoadLeagueAsync(
        int take, CancellationToken ct)
    {
        var league = await _db.Leagues.AsNoTracking()
            .Where(l => !l.IsArchived
                        && _db.DriverStandings.Any(s => s.LeagueId == l.Id))
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Name)
            .FirstOrDefaultAsync(ct);

        if (league is null)
        {
            return (null, new List<string>());
        }

        var drivers = await _db.DriverStandings.AsNoTracking()
            .Where(s => s.LeagueId == league.Id)
            .OrderBy(s => s.Position)
            .ThenBy(s => s.Driver)
            .Select(s => s.Driver)
            .Distinct()
            .Take(take)
            .ToListAsync(ct);

        return (league, drivers);
    }
}
