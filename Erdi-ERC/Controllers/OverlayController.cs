using Erdi_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erdi_ERC.Controllers;

/// <summary>
/// Öffentliche Browser-Source für Streamlabs: HTML-Seite und JSON-Snapshot.
/// Kein Login, keine Cookies; die Seite pollt den Endpunkt alle zwei Sekunden.
/// </summary>
[AllowAnonymous]
[Route("overlay/dotd")]
public class OverlayController : Controller
{
    private readonly IDotdVoteService _dotd;

    public OverlayController(IDotdVoteService dotd)
    {
        _dotd = dotd;
    }

    [HttpGet("")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult DriverOfDay()
        => View("~/Views/Overlay/DriverOfDay.cshtml");

    [HttpGet("data")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Data(CancellationToken ct)
        => Json(await _dotd.GetSnapshotAsync(ct));
}
