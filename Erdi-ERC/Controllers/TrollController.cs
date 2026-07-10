using <OWNER_HANDLE>_ERC.Models.Troll;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace <OWNER_HANDLE>_ERC.Controllers;

/// <summary>
/// Rendert die Login-Pranks („Gate") und nimmt Lösungen entgegen. Wird nur betreten,
/// wenn <see cref="AccountController"/>.LoginCallback einen Troll ausgewürfelt und die
/// nötigen Daten in TempData abgelegt hat. Ohne diese Daten → zurück zur Startseite,
/// damit ein Direktaufruf/Reload nie hängen bleibt.
/// </summary>
[Authorize]
public sealed class TrollController : Controller
{
    private readonly ITrollService _troll;

    // TempData-Keys – zentral, damit Account- und Troll-Controller exakt dieselben nutzen.
    public const string TkGag = "troll:gag";
    public const string TkPrompt = "troll:prompt";
    public const string TkAnswer = "troll:answer";
    public const string TkAttempts = "troll:attempts";
    public const string TkReturnUrl = "troll:returnUrl";

    public TrollController(ITrollService troll) => _troll = troll;

    [HttpGet]
    public IActionResult Gate()
    {
        var gag = _troll.FindGag(TempData.Peek(TkGag) as string);
        if (gag is null)
        {
            return RedirectToAction("Index", "Home");
        }

        // Alle Werte für den nächsten POST (Solve) bzw. Continue behalten.
        KeepTrollData();

        var attempts = ReadAttempts();
        var vm = new TrollGateViewModel
        {
            Gag = gag,
            Prompt = TempData.Peek(TkPrompt) as string,
            // Bei admin-erstellten Gags die Inhalte (Titel/Text/Optionen) fürs generische Partial nachladen.
            Custom = gag.PartialName == "Gags/_Custom" ? _troll.FindCustomGag(gag.Key) : null,
            Attempts = attempts,
            MercyOffered = gag.IsBlocking && attempts >= _troll.MercyAfterAttempts
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Solve(string? answer)
    {
        var gag = _troll.FindGag(TempData.Peek(TkGag) as string);
        if (gag is null || !gag.IsBlocking)
        {
            // Nichts (mehr) zu lösen → wie ein Continue behandeln.
            return RedirectToReturn();
        }

        if (_troll.IsAnswerCorrect(TempData.Peek(TkAnswer) as string, answer))
        {
            return RedirectToReturn();
        }

        // Falsch: Fehlversuch hochzählen und per PRG zurück auf die Gate-Seite,
        // die ab dem ersten Fehler „<OWNER_HANDLE> schüttelt den Kopf" und ab Mercy den Durchlass zeigt.
        TempData[TkAttempts] = (ReadAttempts() + 1).ToString();
        KeepTrollData();
        return RedirectToAction(nameof(Gate));
    }

    [HttpGet]
    public IActionResult Continue() => RedirectToReturn();

    private int ReadAttempts()
        => int.TryParse(TempData.Peek(TkAttempts) as string, out var n) ? n : 0;

    private void KeepTrollData()
    {
        TempData.Keep(TkGag);
        TempData.Keep(TkPrompt);
        TempData.Keep(TkAnswer);
        TempData.Keep(TkAttempts);
        TempData.Keep(TkReturnUrl);
    }

    private IActionResult RedirectToReturn()
    {
        var url = TempData.Peek(TkReturnUrl) as string;
        ClearTroll();
        // Open-Redirect-Schutz: nur lokale Ziele zulassen (gleiches Muster wie LoginCallback).
        if (!string.IsNullOrEmpty(url) && Url.IsLocalUrl(url))
        {
            return LocalRedirect(url);
        }
        return Redirect("/");
    }

    private void ClearTroll()
    {
        TempData.Remove(TkGag);
        TempData.Remove(TkPrompt);
        TempData.Remove(TkAnswer);
        TempData.Remove(TkAttempts);
        TempData.Remove(TkReturnUrl);
    }
}
