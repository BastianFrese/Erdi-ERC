using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace <OWNER_HANDLE>_ERC.Controllers
{
    /// <summary>
    /// Öffentlicher Bewerbungs-Flow: Formular (mit server-seitiger Discord-Guild-
    /// Verifikation) und Self-Service-Status. Das Formular bindet an
    /// <see cref="ApplyViewModel"/> — die Entity wird ausschließlich server-seitig
    /// aufgebaut, Workflow-Felder sind damit nicht overpostbar.
    /// </summary>
    public class ApplicationController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IDiscordGuildService _discordGuilds;
        private readonly IApplicationQueryService _queries;
        private readonly IWebhookAutomationService _webhookAuto;
        private readonly ILogger<ApplicationController> _logger;

        public ApplicationController(
            AppDbContext db,
            IDiscordGuildService discordGuilds,
            IApplicationQueryService queries,
            IWebhookAutomationService webhookAuto,
            ILogger<ApplicationController> logger)
        {
            _db = db;
            _discordGuilds = discordGuilds;
            _queries = queries;
            _webhookAuto = webhookAuto;
            _logger = logger;
        }

        private string? CurrentDiscordId =>
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        /// <summary>Ligen, die aktuell Bewerbungen annehmen (Admin-Schalter, nicht archiviert).</summary>
        private Task<List<League>> GetLeaguesOpenForApplicationsAsync() =>
            _db.Leagues
                .AsNoTracking()
                .Where(l => !l.IsArchived && l.IsOpenForApplications)
                .OrderBy(l => l.SortOrder).ThenBy(l => l.Name)
                .ToListAsync();

        // ── Formular ─────────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> Apply()
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                ViewBag.ReturnUrl = Url.Action(nameof(Apply));
                return View("LoginRequired");
            }

            // Kapazitäts-Zeilen statt nackter Ligen: die Karten im Formular zeigen
            // damit freie Plätze bzw. Warteliste direkt an.
            ViewBag.OpenLeagues = await _queries.GetLeagueCapacitiesAsync();

            var model = new ApplyViewModel
            {
                DiscordName = User.Identity?.Name ?? ""
            };

            // Status einer evtl. vorhandenen Bewerbung ermitteln, damit der Nutzer nicht
            // blind erneut absendet (Doppelbewerbungen vermeiden / Re-Bewerbung klar kommunizieren).
            var discordId = CurrentDiscordId;
            if (!string.IsNullOrWhiteSpace(discordId))
            {
                var existing = await _db.ApplicationForms
                    .AsNoTracking()
                    .Where(a => a.DiscordId == discordId)
                    .OrderByDescending(a => a.SubmittedAt)
                    .FirstOrDefaultAsync();

                if (existing is not null)
                {
                    if (existing.IsAccepted)
                        ViewBag.ExistingApplicationInfo = "Du hast bereits eine angenommene Bewerbung. Bei Fragen melde dich bei einem Admin auf Discord.";
                    else if (!existing.IsRejected)
                        ViewBag.ExistingApplicationInfo = "Du hast bereits eine offene Bewerbung. Sobald sie bearbeitet wurde, wirst du über Discord benachrichtigt — du musst dich nicht erneut bewerben.";
                    else
                        ViewBag.ReapplyHint = "Deine vorherige Bewerbung wurde abgelehnt. Du kannst dich erneut bewerben.";
                }
            }

            // Skip Discord re-check when landing from a successful submission — avoids a
            // confusing "success + Discord warning" state that causes users to resubmit.
            bool comingFromSuccessfulSubmit = TempData.Peek("SuccessMessage") != null;
            if (!comingFromSuccessfulSubmit)
            {
                var check = await _discordGuilds.CheckMembershipAsync(await GetAccessTokenAsync());

                if (check.Status == DiscordGuildCheckStatus.LoginExpired)
                {
                    await HttpContext.SignOutAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
                    return Challenge(
                        new AuthenticationProperties
                        {
                            RedirectUri = Url.Action("LoginCallback", "Account", new { returnUrl = Url.Action(nameof(Apply)) })
                        }, "Discord");
                }

                if (!check.IsOk)
                {
                    ViewBag.DiscordWarning = check.ErrorMessage;
                    return View(model);
                }

                model.JoinedCommunityDiscord = check.JoinedCommunity;
                model.JoinedLeagueDiscord = check.JoinedLeague;

                var missing = MissingGuildNames(check);
                if (missing.Count > 0)
                    ViewBag.DiscordWarning = $"Bitte trete noch folgenden Servern bei: {string.Join(" und ", missing)}.";
            }

            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("forms")]
        public async Task<IActionResult> Apply(ApplyViewModel model)
        {
            // Identität kommt IMMER aus dem Login, nie aus dem Formular.
            var discordName = User.Identity?.Name ?? "Unknown";
            var discordId = CurrentDiscordId;
            model.DiscordName = discordName;

            ViewBag.OpenLeagues = await _queries.GetLeagueCapacitiesAsync();

            // Beworbene Liga server-seitig gegen die offenen Ligen validieren —
            // niemand soll sich auf geschlossene oder archivierte Ligen bewerben können.
            var openLeagues = await GetLeaguesOpenForApplicationsAsync();
            var appliedLeague = openLeagues.FirstOrDefault(l => l.Id == model.AppliedLeagueId);
            if (appliedLeague is null)
                ModelState.AddModelError(nameof(model.AppliedLeagueId), "Bitte wähle eine Liga aus.");

            // Eingaben normalisieren; Wunsch-Team auf den kanonischen F1-Teamnamen
            // mappen, damit Team-Logos auf der Fahrer-Karte sicher greifen.
            model.GamingName = model.GamingName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(model.GamingName))
                ModelState.AddModelError(nameof(model.GamingName), "Bitte gib deinen Ingame-Namen an.");

            model.SimHardware = string.IsNullOrWhiteSpace(model.SimHardware) ? null : model.SimHardware.Trim();
            model.PaceReference = string.IsNullOrWhiteSpace(model.PaceReference) ? null : model.PaceReference.Trim();
            if (string.IsNullOrWhiteSpace(model.PreferredTeam))
            {
                model.PreferredTeam = null;
            }
            else
            {
                var matchedTeam = Helpers.F1TeamsHelper.GetTeamByName(model.PreferredTeam.Trim());
                model.PreferredTeam = matchedTeam?.Name ?? model.PreferredTeam.Trim();
            }

            // Server-seitige Re-Verifikation: vom Client gemeldete Guild-Flags ignorieren
            // und immer frisch gegen die Discord-API prüfen.
            var check = await _discordGuilds.CheckMembershipAsync(await GetAccessTokenAsync());
            if (!check.IsOk)
            {
                ViewBag.DiscordWarning = check.ErrorMessage;
                model.JoinedCommunityDiscord = false;
                model.JoinedLeagueDiscord = false;
                return View(model);
            }

            model.JoinedCommunityDiscord = check.JoinedCommunity;
            model.JoinedLeagueDiscord = check.JoinedLeague;

            var missing = MissingGuildNames(check);
            if (missing.Count > 0)
            {
                ModelState.AddModelError(string.Empty,
                    $"Bewerbung nicht möglich. Bitte trete zuerst folgenden Servern bei: {string.Join(" und ", missing)}.");
                ViewBag.DiscordWarning = $"Bitte trete noch folgenden Servern bei: {string.Join(" und ", missing)}.";
                return View(model);
            }

            if (!ModelState.IsValid)
                return View(model);

            // Doppelbewerbungen abfangen (der DB-Unique-Index bleibt das Sicherheitsnetz).
            if (!string.IsNullOrWhiteSpace(discordId))
            {
                var alreadyApplied = await _db.ApplicationForms
                    .AnyAsync(a => a.DiscordId == discordId && a.Status != ApplicationStatus.Rejected);
                if (alreadyApplied)
                {
                    ModelState.AddModelError(string.Empty,
                        "Du hast bereits eine aktive Bewerbung. Sobald sie bearbeitet wurde, wirst du über Discord benachrichtigt.");
                    return View(model);
                }
            }

            // Entity ausschließlich server-seitig aufbauen — Status- und Workflow-Felder
            // kommen nie aus dem Request.
            var application = new ApplicationForm
            {
                Age = model.Age!.Value,
                DiscordName = discordName,
                DiscordId = discordId,
                Role = model.Role,
                JoinedCommunityDiscord = check.JoinedCommunity,
                JoinedLeagueDiscord = check.JoinedLeague,
                GamingName = model.GamingName,
                SimHardware = model.SimHardware,
                PreferredNumber = model.PreferredNumber,
                PreferredTeam = model.PreferredTeam,
                PaceReference = model.PaceReference,
                Platform = model.Platform,
                AiLevel = model.AiLevel,
                AppliedLeagueId = appliedLeague!.Id,
                // Division als Name-Snapshot der Liga (Anzeige & Sortierung).
                Division = appliedLeague.Name,
                Status = ApplicationStatus.Open,
                SubmittedAt = DateTime.UtcNow
            };

            _db.ApplicationForms.Add(application);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
            {
                // Greift, wenn zwei Requests gleichzeitig durch den Vorab-Check rutschen:
                // der Unique-Index auf der aktiven Bewerbung lässt nur einen Insert zu.
                _logger.LogWarning(dbEx, "Doppelte Bewerbung abgefangen für {DiscordId}", discordId);
                ModelState.AddModelError(string.Empty,
                    "Du hast bereits eine aktive Bewerbung. Sobald sie bearbeitet wurde, wirst du über Discord benachrichtigt.");
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Speichern der Bewerbung für {DiscordId}", discordId);
                ModelState.AddModelError(string.Empty, "Die Bewerbung konnte leider nicht gespeichert werden. Bitte versuche es in wenigen Minuten erneut.");
                return View(model);
            }

            TempData["SuccessMessage"] = "Danke! Deine Anmeldung wurde erfolgreich gesendet.";
            await _webhookAuto.FireAsync(WebhookEvents.ApplicationReceived, new()
            {
                ["DiscordName"] = application.DiscordName,
                ["DiscordId"]   = application.DiscordId ?? "",
                ["GamingName"]  = application.GamingName,
                ["Role"]        = application.Role,
                ["Division"]    = application.Division,
                ["SubmittedAt"] = application.SubmittedAt.ToString("dd.MM.yyyy HH:mm"),
            });
            return RedirectToAction(nameof(Apply));
        }

        // ── Self-Service-Status ──────────────────────────────────────────────────

        /// <summary>Zeigt dem eingeloggten Bewerber den Status seiner letzten Bewerbung.</summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> MyApplication()
        {
            var discordId = CurrentDiscordId;
            if (string.IsNullOrWhiteSpace(discordId))
                return RedirectToAction(nameof(Apply));

            var app = await _db.ApplicationForms
                .AsNoTracking()
                .Where(a => a.DiscordId == discordId)
                .OrderByDescending(a => a.SubmittedAt)
                .FirstOrDefaultAsync();

            return View(app);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private Task<string?> GetAccessTokenAsync() =>
            HttpContext.GetTokenAsync(
                Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme,
                "access_token");

        private static List<string> MissingGuildNames(DiscordGuildCheckResult check)
        {
            var missing = new List<string>();
            if (!check.JoinedCommunity) missing.Add("Community Discord");
            if (!check.JoinedLeague) missing.Add("Liga Discord");
            return missing;
        }
    }
}
