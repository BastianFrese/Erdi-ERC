using System.Net;
using Erdi_ERC.Services;

namespace Erdi_ERC.Middleware
{
    /// <summary>
    /// Wartungsmodus: Ist das Flag aktiv, sehen alle Nicht-Admins eine schlanke,
    /// self-contained Wartungsseite (HTTP 503). Admins (Claim "erdi:admin") arbeiten
    /// normal weiter, damit der Toggle im Admin-Dashboard erreichbar bleibt.
    ///
    /// Die Seite ist bewusst OHNE Layout und OHNE DB gebaut — der Wartungsmodus
    /// funktioniert also auch, wenn die Datenbank down ist (häufigster Grund).
    /// Statische Assets (CSS/JS/Bilder) werden weiter ausgeliefert, da die
    /// Static-Files-Middleware vor dieser Middleware läuft.
    ///
    /// Registriert in Program.cs NACH UseAuthentication (braucht den User-Claim),
    /// VOR UseAuthorization.
    /// </summary>
    public class MaintenanceModeMiddleware
    {
        private readonly RequestDelegate _next;

        public MaintenanceModeMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ISiteSettingsService siteSettings)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            var isAdmin = context.User.HasClaim("erdi:admin", "true");

            if (!ShouldBlock(path, isAdmin, siteSettings.IsMaintenanceMode()))
            {
                await _next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "text/html; charset=utf-8";
            // Nie cachen: nach dem Ende des Wartungsmodus darf kein Browser/CDN die 503-Seite weiter ausliefern.
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsync(BuildPage(siteSettings.GetMaintenanceMessage()));
        }

        /// <summary>
        /// Entscheidungslogik (statisch + testbar): blockiert nur, wenn der Wartungsmodus
        /// aktiv ist, der User kein Admin ist und der Pfad nicht auf der Durchlass-Liste steht.
        /// </summary>
        public static bool ShouldBlock(string path, bool isAdmin, bool maintenanceActive)
        {
            if (!maintenanceActive) return false;
            if (isAdmin) return false;

            // Trailing-Slash normalisieren (ASP.NET toleriert "/Account/Login/").
            var normalized = path.TrimEnd('/');

            // Health-Checks müssen für Monitoring/Load-Balancer erreichbar bleiben.
            if (normalized.StartsWith("/health/", StringComparison.OrdinalIgnoreCase)) return false;

            // Login-Flow (3 Hops) + OAuth-Callback dürfen nie blockiert werden: ein ausgeloggter
            // Admin muss sich sonst nicht mehr einloggen können, um den Modus zu beenden.
            //   1) /Account/Login          → Discord-Challenge
            //   2) /signin-discord         → OAuth-Callback (Cookie wird erst im nächsten Hop gesetzt)
            //   3) /Account/LoginCallback → SignInAsync setzt das Cookie hier
            if (normalized.Equals("/Account/Login", StringComparison.OrdinalIgnoreCase)) return false;
            if (normalized.Equals("/Account/LoginCallback", StringComparison.OrdinalIgnoreCase)) return false;
            if (normalized.Equals("/signin-discord", StringComparison.OrdinalIgnoreCase)) return false;
            if (normalized.StartsWith("/signin-discord?", StringComparison.OrdinalIgnoreCase)) return false;

            // Logout erlauben, damit ein User die Session beenden kann, auch wenn die Seite gesperrt ist.
            if (normalized.Equals("/Account/Logout", StringComparison.OrdinalIgnoreCase)) return false;

            // Telemetrie-Ingest: Rennergebnisse der Telemetrie-App müssen auch im
            // Wartungsbetrieb ankommen (Datenverlust-Prävention), Key-Auth liegt im Handler.
            if (normalized.StartsWith("/api/telemetry", StringComparison.OrdinalIgnoreCase)) return false;

            return true;
        }

        private static string BuildPage(string message)
        {
            var text = string.IsNullOrWhiteSpace(message)
                ? "Wir arbeiten gerade an der Strecke. Schau in ein paar Minuten wieder vorbei — wir sind gleich wieder da. Wiener hat großes problem, Wiener hat kaka gebaut :c"
                : message;

            return $@"<!doctype html>
<html lang='de'>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width, initial-scale=1'>
<title>Wartungsarbeiten · Erdi's Racing Community</title>
<style>
  :root {{
    --bg-0:#030407; --surface-1:rgba(12,16,24,.86); --stroke-1:rgba(255,255,255,.12);
    --ink:#f4f6fb; --ink-2:rgba(244,246,251,.74);
    --accent:#e10600; --accent-2:#ff3b2f;
    --ff-display:'Inter','Segoe UI',system-ui,sans-serif;
    --ff-tech:'Orbitron','Inter',system-ui,sans-serif;
  }}
  * {{ box-sizing:border-box; margin:0; padding:0; }}
  html,body {{ height:100%; }}
  body {{
    background:var(--bg-0);
    background-image:radial-gradient(55% 75% at 50% 0%, rgba(225,6,0,.10), transparent 65%);
    color:var(--ink); font-family:var(--ff-display);
    display:flex; align-items:center; justify-content:center; padding:1.5rem;
  }}
  .panel {{
    max-width:520px; width:100%; background:var(--surface-1);
    border:1px solid var(--stroke-1); border-radius:12px;
    padding:2.5rem 2rem; text-align:center;
    box-shadow:0 1px 0 rgba(255,255,255,.05) inset, 0 20px 60px -20px rgba(0,0,0,.65);
  }}
  .flag {{ width:64px; height:8px; border-radius:4px; margin:0 auto 1.5rem;
    background:linear-gradient(90deg, var(--accent-2), var(--accent)); }}
  .kicker {{ font-family:var(--ff-tech); font-size:.72rem; letter-spacing:.28em;
    text-transform:uppercase; color:var(--accent-2); margin-bottom:.75rem; }}
  h1 {{ font-size:1.5rem; font-weight:700; margin-bottom:.75rem; }}
  p {{ color:var(--ink-2); line-height:1.6; font-size:.95rem; }}
  .status {{ display:inline-flex; align-items:center; gap:.5rem; margin-top:1.5rem;
    padding:.4rem .9rem; border:1px solid var(--stroke-1); border-radius:999px;
    font-size:.78rem; color:var(--ink-2); }}
  .dot {{ width:8px; height:8px; border-radius:50%; background:var(--accent-2);
    animation:pulse 1.6s ease-in-out infinite; }}
  @keyframes pulse {{ 0%,100% {{ opacity:1; }} 50% {{ opacity:.35; }} }}
</style>
</head>
<body>
  <div class='panel'>
    <div class='flag'></div>
    <div class='kicker'>Erdi's Racing Community</div>
    <h1>Wartungsarbeiten</h1>
    <p>{WebUtility.HtmlEncode(text)}</p>
    <div class='status'><span class='dot'></span>Wir sind gleich wieder da</div>
  </div>
</body>
</html>";
        }
    }
}
