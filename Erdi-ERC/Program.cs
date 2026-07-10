using AspNet.Security.OAuth.Discord;
using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Options;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Serilog;
using Serilog.Events;
using System.IO.Compression;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// --- Serilog: strukturiertes Logging in Konsole + Rolling-File ---
var logsDir = Path.Combine(builder.Environment.ContentRootPath, "logs");
Directory.CreateDirectory(logsDir);

builder.Host.UseSerilog((context, services, loggerConfig) =>
{
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .WriteTo.Console()
        .WriteTo.File(
            path: Path.Combine(logsDir, "erdi-erc-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            fileSizeLimitBytes: 50 * 1024 * 1024,
            rollOnFileSizeLimit: true,
            shared: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}");
});

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    // CSRF-Schutz global für alle POST/PUT/DELETE/PATCH erzwingen.
    // Einzelne Actions können mit [IgnoreAntiforgeryToken] opt-out machen (z.B. Webhooks).
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
// Named Client für Webhook-Automation: knappes Timeout damit eine stockende Discord-API
// nicht den ganzen Request blockiert (Retry-Logic im Service fängt transiente Fehler).
builder.Services.AddHttpClient("WebhookAutomation", c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
});
// Named Client für Discord-OAuth/Member-Lookups (Bewerbung & Setup-Tier).
builder.Services.AddHttpClient("DiscordApi", c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
});
// Memory-Cache für Layout-Daten, statische Lookups, Endpoint-Caches.
// SizeLimit verhindert ungeleitete Speicherflut.
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024;
});
builder.Services.AddScoped<ILayoutDataService, LayoutDataService>();
builder.Services.AddScoped<IStaticDataCache, StaticDataCache>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<IAdminAuditService, AdminAuditService>();
builder.Services.AddScoped<IWebhookAutomationService, WebhookAutomationService>();
builder.Services.AddScoped<ISetupAccessService, SetupAccessService>();
builder.Services.AddScoped<ITrackSetupAccessPolicy, TrackSetupAccessPolicy>();
builder.Services.AddScoped<IDriverProfileService, DriverProfileService>();
builder.Services.AddScoped<IApplicationWorkflowService, ApplicationWorkflowService>();
builder.Services.AddScoped<IApplicationQueryService, ApplicationQueryService>();
builder.Services.AddScoped<IDiscordGuildService, DiscordGuildService>();
builder.Services.AddScoped<ICommunityContentService, CommunityContentService>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<ITrollService, TrollService>();
builder.Services.AddScoped<DatabaseTransactionHelper>();
builder.Services.Configure<DiscordSetupAccessOptions>(builder.Configuration.GetSection("Discord:SetupAccess"));
builder.Services.Configure<DiscordGuildOptions>(builder.Configuration.GetSection(DiscordGuildOptions.SectionName));
builder.Services.Configure<DriverMatchingOptions>(builder.Configuration.GetSection(DriverMatchingOptions.SectionName));
builder.Services.Configure<ApplicationOptions>(builder.Configuration.GetSection(ApplicationOptions.SectionName));
builder.Services.Configure<TrollOptions>(builder.Configuration.GetSection(TrollOptions.SectionName));
builder.Services.Configure<AuthCookieOptions>(builder.Configuration.GetSection(AuthCookieOptions.SectionName));
builder.Services.Configure<BackgroundMusicOptions>(builder.Configuration.GetSection(BackgroundMusicOptions.SectionName));
builder.Services.Configure<F1ScoringOptions>(builder.Configuration.GetSection(F1ScoringOptions.SectionName));
builder.Services.Configure<LeagueDivisionOptions>(builder.Configuration.GetSection(LeagueDivisionOptions.SectionName));
builder.Services.Configure<RateLimitingOptions>(builder.Configuration.GetSection(RateLimitingOptions.SectionName));
builder.Services.Configure<SerilogFileOptions>(builder.Configuration.GetSection(SerilogFileOptions.SectionName));
builder.Services.AddHealthChecks();

// Output-Cache: für rein öffentliche, nicht user-spezifische Endpunkte (Highlights, BG-Music, etc.).
// Wichtig: Pages mit Auth-Status im Layout (Navbar zeigt User-Name) dürfen NICHT pauschal hier landen,
// daher gibt es nur eine konservative Policy für API-Endpunkte ohne Auth-Abhängigkeit.
builder.Services.AddOutputCache(options =>
{
    // Base-Policy ist explizit nicht cachen (Default für alle Endpunkte ohne [OutputCache]),
    // nur opt-in über die "public-*"-Policies unten.
    options.AddPolicy("public-30s", builder => builder
        .Expire(TimeSpan.FromSeconds(30))
        .SetVaryByQuery("*")
        .Tag("public"));

    options.AddPolicy("public-2min", builder => builder
        .Expire(TimeSpan.FromMinutes(2))
        .SetVaryByQuery("*")
        .Tag("public"));
});

// Response-Compression: Brotli (besser) + Gzip (Fallback) für alle Text-Responses inkl. HTTPS.
// HTML/CSS/JS/JSON werden um ~70-80% reduziert.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/font-woff2",
        "application/manifest+json",
        "application/wasm",
        "image/svg+xml",
        "image/x-icon",
        "text/plain"
    });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

// HSTS für Production: 1 Jahr + Subdomains + Preload
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

// Rate Limiting – globaler Default + strenger Auth-Bucket
var rlOpts = builder.Configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rlOpts.Global.PermitLimit,
                Window = TimeSpan.FromSeconds(rlOpts.Global.WindowSeconds),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rlOpts.Auth.PermitLimit,
                Window = TimeSpan.FromSeconds(rlOpts.Auth.WindowSeconds),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

    // "forms"-Policy für schreibende Community-Endpunkte (Bewerbung, Highlight, Wall, Vote…)
    options.AddPolicy("forms", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rlOpts.Forms.PermitLimit,
                Window = TimeSpan.FromSeconds(rlOpts.Forms.WindowSeconds),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
});

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod
        | HttpLoggingFields.RequestPath
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.Duration;
});

if (builder.Environment.IsProduction())
{
    var missingSettings = new List<string>();

    if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Default")))
    {
        missingSettings.Add("ConnectionStrings:Default");
    }

    if (string.IsNullOrWhiteSpace(builder.Configuration["Discord:ClientId"]))
    {
        missingSettings.Add("Discord:ClientId");
    }

    if (string.IsNullOrWhiteSpace(builder.Configuration["Discord:ClientSecret"]))
    {
        missingSettings.Add("Discord:ClientSecret");
    }

    if (missingSettings.Count > 0)
    {
        throw new InvalidOperationException($"Fehlende Production-Konfiguration: {string.Join(", ", missingSettings)}");
    }
}

// --- Database ---
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default fehlt in der Konfiguration.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
           // NoTracking als Default: spart pro Read-Query ChangeTracker-Overhead + Memory.
           // Schreibpfade müssen .AsTracking() oder Add/Update/Remove explizit verwenden.
           // IdentityResolution sorgt dafür, dass Include()-Ketten konsistente Objektidentität haben.
           .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution));

// TODO(production): add central exception logging sink (e.g. Serilog + file/ELK).

// --- Authentication: Cookie + Discord OAuth ---
var cookieOpts = builder.Configuration.GetSection(AuthCookieOptions.SectionName).Get<AuthCookieOptions>() ?? new();
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = DiscordAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    // Kurze Lebensdauer + Sliding: ein inaktiver User wird nach ExpireHours ausgeloggt,
    // sodass beim nächsten Login Discord-Mitgliedschaft & Sub-Rollen frisch geprüft werden.
    options.ExpireTimeSpan = TimeSpan.FromHours(cookieOpts.ExpireHours);
    options.SlidingExpiration = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;

    options.Events = new CookieAuthenticationEvents
    {
        OnValidatePrincipal = async context =>
        {
            var syncClaim = context.Principal?.FindFirst("erdi:setup-sync-at")?.Value;
            var mustRefresh = true;

            if (DateTimeOffset.TryParse(syncClaim, out var lastSync))
            {
                mustRefresh = DateTimeOffset.UtcNow - lastSync >= TimeSpan.FromMinutes(cookieOpts.RefreshDiscordMembershipMinutes);
            }

            if (!mustRefresh)
            {
                return;
            }

            var setupAccessService = context.HttpContext.RequestServices.GetRequiredService<ISetupAccessService>();

            string? accessToken = null;
            if (context.Properties?.Items.TryGetValue(".Token.access_token", out var tokenValue) == true)
            {
                accessToken = tokenValue;
            }

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                // Ohne Discord-Token kann nicht mehr verifiziert werden -> ausloggen, damit der User neu authentifiziert.
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var resolution = await setupAccessService.ResolveSetupAccessAsync(accessToken, context.HttpContext.RequestAborted);

            if (!resolution.Success)
            {
                if (resolution.IsTransientError)
                {
                    // Discord-API war nicht erreichbar (Netzwerkfehler/Timeout).
                    // Existierende Claims behalten, nächsten Sync-Versuch auf in 5 Min. schieben.
                    var identity2 = context.Principal?.Identity as ClaimsIdentity;
                    if (identity2 is not null)
                    {
                        var old = identity2.FindFirst("erdi:setup-sync-at");
                        if (old is not null) identity2.RemoveClaim(old);
                        identity2.AddClaim(new Claim("erdi:setup-sync-at",
                            DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(10)).ToString("O")));
                        context.ShouldRenew = true;
                    }
                    return;
                }

                // Discord-API hat eindeutig geantwortet: Token ungültig oder User nicht mehr auf Guild.
                // Ausloggen, damit beim nächsten Login frisch geprüft wird.
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var identity = context.Principal?.Identity as ClaimsIdentity;
            if (identity is null)
            {
                return;
            }

            static void RemoveClaim(ClaimsIdentity identity, string claimType)
            {
                var claims = identity.FindAll(claimType).ToList();
                foreach (var claim in claims)
                {
                    identity.RemoveClaim(claim);
                }
            }

            RemoveClaim(identity, "erdi:setup-tier");
            RemoveClaim(identity, "erdi:setup-role");
            RemoveClaim(identity, "erdi:on-community-guild");
            RemoveClaim(identity, "erdi:guild-joined-at");
            RemoveClaim(identity, "erdi:tenure-pending");
            RemoveClaim(identity, "erdi:setup-sync-at");
            RemoveClaim(identity, "erdi:admin");
            RemoveClaim(identity, "erdi:superadmin");
            RemoveClaim(identity, "erdi:perm");

            identity.AddClaim(new Claim("erdi:setup-tier", resolution.Tier.ToString()));
            if (!string.IsNullOrWhiteSpace(resolution.RoleLabel))
            {
                identity.AddClaim(new Claim("erdi:setup-role", resolution.RoleLabel));
            }
            identity.AddClaim(new Claim("erdi:on-community-guild", resolution.IsOnCommunityGuild ? "true" : "false"));
            if (resolution.GuildJoinedAtUtc.HasValue)
            {
                identity.AddClaim(new Claim("erdi:guild-joined-at", resolution.GuildJoinedAtUtc.Value.ToString("O")));
            }
            identity.AddClaim(new Claim("erdi:tenure-pending", resolution.IsPendingTenure ? "true" : "false"));
            identity.AddClaim(new Claim("erdi:setup-sync-at", DateTimeOffset.UtcNow.ToString("O")));

            // Admin-Check: prüfen ob die Discord-ID in der AdminUsers-Tabelle steht
            var discordId = identity.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(discordId))
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<<OWNER_HANDLE>_ERC.Data.AppDbContext>();
                var adminUser = await db.AdminUsers
                    .Include(a => a.Permissions)
                    .FirstOrDefaultAsync(a => a.DiscordId == discordId);

                if (adminUser is not null)
                {
                    identity.AddClaim(new Claim("erdi:admin", "true"));

                    if (adminUser.IsSuperAdmin)
                    {
                        identity.AddClaim(new Claim("erdi:superadmin", "true"));
                        // Superadmin bekommt alle Berechtigungen implizit
                        foreach (var perm in <OWNER_HANDLE>_ERC.Models.AdminPermissions.All)
                        {
                            identity.AddClaim(new Claim("erdi:perm", perm.Key));
                        }
                    }
                    else
                    {
                        foreach (var perm in adminUser.Permissions)
                        {
                            identity.AddClaim(new Claim("erdi:perm", perm.Permission));
                        }
                    }
                }
            }

            context.ShouldRenew = true;
        }
    };
})
.AddDiscord(options =>
{
    options.ClientId = builder.Configuration["Discord:ClientId"] ?? "missing";
    options.ClientSecret = builder.Configuration["Discord:ClientSecret"] ?? "missing";
    options.SaveTokens = true;
    options.Scope.Add("identify");
    options.Scope.Add("guilds");
    options.Scope.Add("guilds.members.read");
    options.CallbackPath = "/signin-discord";
    // Correlation-Cookie muss über den Proxy-Redirect mitgenommen werden
    options.CorrelationCookie.SameSite = SameSiteMode.Lax;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddAuthorization(options =>
{
    // Basis: jeder in der AdminUsers-Tabelle
    options.AddPolicy("Admin", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("erdi:admin", "true"));

    // Bereichsspezifische Policies – Superadmins erhalten alle Berechtigungen automatisch
    // Helper: prüft ob ein User einen bestimmten granularen Key ODER den Legacy-Gruppen-Key besitzt
    static bool HasPerm(System.Security.Claims.ClaimsPrincipal user, string key, string legacyGroup)
        => user.HasClaim("erdi:superadmin", "true")
        || user.Claims.Any(c => c.Type == "erdi:perm" && (c.Value == key || c.Value == legacyGroup));

    // ── Bewerbungen ──────────────────────────────────────────────────────────────
    options.AddPolicy("Admin.Applications", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx =>
                  HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.ApplicationsView,    <OWNER_HANDLE>_ERC.Models.AdminPermissions.Applications) ||
                  HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.ApplicationsManage,  <OWNER_HANDLE>_ERC.Models.AdminPermissions.Applications) ||
                  HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.ApplicationsMetrics, <OWNER_HANDLE>_ERC.Models.AdminPermissions.Applications)));

    options.AddPolicy("Admin.Applications.View", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx => HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.ApplicationsView, <OWNER_HANDLE>_ERC.Models.AdminPermissions.Applications)));

    options.AddPolicy("Admin.Applications.Manage", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx => HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.ApplicationsManage, <OWNER_HANDLE>_ERC.Models.AdminPermissions.Applications)));

    options.AddPolicy("Admin.Applications.Metrics", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx => HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.ApplicationsMetrics, <OWNER_HANDLE>_ERC.Models.AdminPermissions.Applications)));

    // ── Liga ─────────────────────────────────────────────────────────────────────
    options.AddPolicy("Admin.League", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx =>
                  HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.LeagueStandings, <OWNER_HANDLE>_ERC.Models.AdminPermissions.System) ||
                  HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.LeagueRaces,     <OWNER_HANDLE>_ERC.Models.AdminPermissions.System)));

    options.AddPolicy("Admin.League.Standings", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx => HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.LeagueStandings, <OWNER_HANDLE>_ERC.Models.AdminPermissions.System)));

    options.AddPolicy("Admin.League.Races", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx => HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.LeagueRaces, <OWNER_HANDLE>_ERC.Models.AdminPermissions.System)));

    // ── Community ────────────────────────────────────────────────────────────────
    options.AddPolicy("Admin.Community", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx =>
                  HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.CommunityHub,        <OWNER_HANDLE>_ERC.Models.AdminPermissions.Community) ||
                  HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.CommunityEvents,     <OWNER_HANDLE>_ERC.Models.AdminPermissions.Community) ||
                  HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.CommunityStreams,    <OWNER_HANDLE>_ERC.Models.AdminPermissions.Community) ||
                  HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.CommunityStewarding, <OWNER_HANDLE>_ERC.Models.AdminPermissions.Community)));

    options.AddPolicy("Admin.Community.Hub", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx => HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.CommunityHub, <OWNER_HANDLE>_ERC.Models.AdminPermissions.Community)));

    options.AddPolicy("Admin.Community.Events", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx => HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.CommunityEvents, <OWNER_HANDLE>_ERC.Models.AdminPermissions.Community)));

    options.AddPolicy("Admin.Community.Streams", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx => HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.CommunityStreams, <OWNER_HANDLE>_ERC.Models.AdminPermissions.Community)));

    options.AddPolicy("Admin.Community.Stewarding", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx => HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.CommunityStewarding, <OWNER_HANDLE>_ERC.Models.AdminPermissions.Community)));

    // ── Fahrer ───────────────────────────────────────────────────────────────────
    options.AddPolicy("Admin.Drivers", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx =>
                  HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.DriversAchievements, <OWNER_HANDLE>_ERC.Models.AdminPermissions.Drivers) ||
                  HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.DriversDefinitions,  <OWNER_HANDLE>_ERC.Models.AdminPermissions.Drivers) ||
                  HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.DriversCards,        <OWNER_HANDLE>_ERC.Models.AdminPermissions.Drivers)));

    options.AddPolicy("Admin.Drivers.Achievements", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx => HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.DriversAchievements, <OWNER_HANDLE>_ERC.Models.AdminPermissions.Drivers)));

    options.AddPolicy("Admin.Drivers.Definitions", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx => HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.DriversDefinitions, <OWNER_HANDLE>_ERC.Models.AdminPermissions.Drivers)));

    options.AddPolicy("Admin.Drivers.Cards", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx => HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.DriversCards, <OWNER_HANDLE>_ERC.Models.AdminPermissions.Drivers)));

    // ── System ───────────────────────────────────────────────────────────────────
    options.AddPolicy("Admin.System", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx =>
                  HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.SystemMusic,     <OWNER_HANDLE>_ERC.Models.AdminPermissions.System) ||
                  HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.SystemSetups,    <OWNER_HANDLE>_ERC.Models.AdminPermissions.System) ||
                  HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.SystemAuditLogs, <OWNER_HANDLE>_ERC.Models.AdminPermissions.System) ||
                  HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.SystemWebhooks,  <OWNER_HANDLE>_ERC.Models.AdminPermissions.System) ||
                  HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.SystemAboutMe,   <OWNER_HANDLE>_ERC.Models.AdminPermissions.System) ||
                  HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.LeagueStandings, <OWNER_HANDLE>_ERC.Models.AdminPermissions.System) ||
                  HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.LeagueRaces,     <OWNER_HANDLE>_ERC.Models.AdminPermissions.System) ||
                  HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.SystemTroll,     <OWNER_HANDLE>_ERC.Models.AdminPermissions.System)));

    options.AddPolicy("Admin.System.Music", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx => HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.SystemMusic, <OWNER_HANDLE>_ERC.Models.AdminPermissions.System)));

    options.AddPolicy("Admin.System.Setups", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx => HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.SystemSetups, <OWNER_HANDLE>_ERC.Models.AdminPermissions.System)));

    options.AddPolicy("Admin.System.AuditLogs", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx => HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.SystemAuditLogs, <OWNER_HANDLE>_ERC.Models.AdminPermissions.System)));

    options.AddPolicy("Admin.System.Webhooks", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx => HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.SystemWebhooks, <OWNER_HANDLE>_ERC.Models.AdminPermissions.System)));

    options.AddPolicy("Admin.System.AboutMe", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("erdi:admin", "true")
              .RequireClaim("erdi:superadmin", "true"));

    options.AddPolicy("Admin.System.Regelwerk", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx => HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.SystemRegelwerk, <OWNER_HANDLE>_ERC.Models.AdminPermissions.System)));

    options.AddPolicy("Admin.System.Troll", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("erdi:admin", "true")
              .RequireAssertion(ctx => HasPerm(ctx.User, <OWNER_HANDLE>_ERC.Models.AdminPermissions.SystemTroll, <OWNER_HANDLE>_ERC.Models.AdminPermissions.System)));
});
var app = builder.Build();

// --- DB initialisieren + Seed ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Wenn die DB bereits existiert (via EnsureCreated angelegt), aber noch keine
    // Migrations-History hat, Initial-Migration als angewendet markieren,
    // damit Migrate() nicht versucht bereits vorhandene Tabellen neu anzulegen.
    try
    {
        var applied = db.Database.GetAppliedMigrations().ToList();
        if (!applied.Any())
        {
            // __EFMigrationsHistory Tabelle ggf. anlegen
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
                    `MigrationId` varchar(150) NOT NULL,
                    `ProductVersion` varchar(32) NOT NULL,
                    PRIMARY KEY (`MigrationId`)
                ) CHARACTER SET=utf8mb4;");

            // Prüfen ob die DB bereits Tabellen aus EnsureCreated hat
            var tables = db.Database
                .SqlQueryRaw<string>("SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Leagues'")
                .ToList();

            if (tables.Any())
            {
                // DB existiert bereits – InitialCreate als angewendet markieren
                db.Database.ExecuteSqlRaw(
                    "INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ({0}, {1})",
                    "20260420183736_InitialCreate",
                    "9.0.0");
            }
        }
    }
    catch { /* Erste Inbetriebnahme – DB existiert noch nicht, Migrate() legt alles an */ }

    // Alle ausstehenden Migrationen anwenden (inkl. neue Tabellen)
    db.Database.Migrate();

    // Bestehende Admins ohne IsSuperAdmin=true → als SuperAdmin markieren (Upgrade-Pfad)
    var legacyAdmins = db.AdminUsers.AsTracking().Where(a => !a.IsSuperAdmin).ToList();
    if (legacyAdmins.Any(a => !db.AdminUserPermissions.Any(p => p.DiscordId == a.DiscordId)))
    {
        foreach (var admin in legacyAdmins)
        {
            if (!db.AdminUserPermissions.Any(p => p.DiscordId == admin.DiscordId))
            {
                admin.IsSuperAdmin = true;
            }
        }
        db.SaveChanges();
    }

    DataSeeder.Seed(db, app.Configuration);
}

// Configure the HTTP request pipeline.

// Reverse-Proxy-Header weiterleiten (Nginx Proxy Manager vor LXC)
// KnownIPNetworks/KnownProxies leeren → alle vorgelagerten Proxies werden vertraut
// (sicher, solange der Container nicht direkt aus dem Internet reachable ist)
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor
                     | ForwardedHeaders.XForwardedProto
                     | ForwardedHeaders.XForwardedHost
};
forwardedOptions.KnownIPNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

if (!app.Environment.IsDevelopment())
{
    // Unbehandelte Exceptions loggen, bevor zur Error-Page weitergeleitet wird.
    app.UseExceptionHandler(errApp =>
    {
        errApp.Run(async context =>
        {
            var exFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
            if (exFeature?.Error is not null)
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(exFeature.Error,
                    "Unhandled exception on {Method} {Path}",
                    context.Request.Method,
                    context.Request.Path);
            }
            context.Response.Redirect("/Home/Error");
        });
    });
    app.UseHsts();
}

// HTTPS-Redirect nur lokal – NPM terminiert TLS extern, intern läuft die App über HTTP
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseHttpLogging();

// Serilog request logging mit Method/Path/Status/Dauer + ClientIp
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString() ?? "-");
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
    };
});

// --- Security Headers (vor Static Files, damit auch für Assets gesetzt) ---
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "SAMEORIGIN";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), interest-cohort=()";
    headers["Cross-Origin-Opener-Policy"] = "same-origin";

    if (!headers.ContainsKey("Content-Security-Policy"))
    {
        // Hinweis: 'unsafe-inline' bleibt vorerst aktiv, da Razor-Views/Bootstrap
        // Inline-Styles nutzen. Falls später Nonces eingeführt werden, hier ersetzen.
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "base-uri 'self'; " +
            "object-src 'none'; " +
            "img-src 'self' data: blob: https:; " +
            "media-src 'self' blob: https:; " +
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdn.jsdelivr.net; " +
            "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
            "font-src 'self' data: https://fonts.gstatic.com https://cdn.jsdelivr.net; " +
            "connect-src 'self' https:; " +
            "frame-src 'self' https://player.twitch.tv https://www.twitch.tv https://embed.twitch.tv https://discord.com https://www.youtube.com https://www.youtube-nocookie.com; " +
            "form-action 'self' https://discord.com; " +
            "frame-ancestors 'self'; " +
            "upgrade-insecure-requests;";
    }

    await next();
});

app.UseRateLimiter();

// OutputCache nach Auth, damit user-spezifische Inhalte nicht zwischen Sessions geleakt werden.
// (Wir nutzen ihn aktuell nur für klar öffentliche Endpunkte via [OutputCache]-Attribut.)
app.UseOutputCache();

// Response-Compression MUSS vor UseStaticFiles laufen, damit auch CSS/JS/SVG komprimiert werden.
app.UseResponseCompression();

// Configure serving static files - BEFORE routing.
// Aggressives Caching für /css, /js, /lib, /images, /videos – asp-append-version setzt einen Hash-?v=,
// und favicon/manifest haben stabile URLs, weshalb max-age=1y + immutable sicher ist.
// Uploads + Backgroundmusic werden weiter unten separat behandelt.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value;
        if (string.IsNullOrEmpty(path)) return;

        var isCacheable =
            path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/images/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/videos/", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".woff", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".webmanifest", StringComparison.OrdinalIgnoreCase);

        if (isCacheable)
        {
            ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        }
    }
});

// Serve background music files from project root folder
var backgroundMusicPath = Path.Combine(app.Environment.ContentRootPath, "backgroundmusic");
Directory.CreateDirectory(backgroundMusicPath);

var musicContentTypeProvider = new FileExtensionContentTypeProvider();
musicContentTypeProvider.Mappings[".mp3"] = "audio/mpeg";
musicContentTypeProvider.Mappings[".wav"] = "audio/wav";
musicContentTypeProvider.Mappings[".ogg"] = "audio/ogg";

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(backgroundMusicPath),
    RequestPath = "/backgroundmusik",
    ContentTypeProvider = musicContentTypeProvider,
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.CacheControl = "public, max-age=86400";
    }
});

// Additionally configure uploads directory to be served with proper cache headers
var uploadsPath = Path.Combine(app.Environment.WebRootPath, "uploads");
// Ensure uploads directory exists so we can register a StaticFiles middleware for it
Directory.CreateDirectory(uploadsPath);
{
    var provider = new FileExtensionContentTypeProvider();
    // Ensure common image types are included
    provider.Mappings[".png"] = "image/png";
    provider.Mappings[".jpg"] = "image/jpeg";
    provider.Mappings[".jpeg"] = "image/jpeg";
    provider.Mappings[".gif"] = "image/gif";
    provider.Mappings[".webp"] = "image/webp";
    provider.Mappings[".bmp"] = "image/bmp";
    provider.Mappings[".svg"] = "image/svg+xml";
    provider.Mappings[".tif"] = "image/tiff";
    provider.Mappings[".tiff"] = "image/tiff";
    provider.Mappings[".ico"] = "image/x-icon";
    provider.Mappings[".avif"] = "image/avif";

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsPath),
        RequestPath = "/uploads",
        ContentTypeProvider = provider,
        OnPrepareResponse = ctx =>
        {
            // Cache uploaded images for 7 days (they have unique names based on Guid)
            ctx.Context.Response.Headers.CacheControl = "public, max-age=604800";
        }
    });
}

// --- 301-Redirects für den Controller-Split (Juli 2026) ---
// Alte /Home/...- und /Admin/...-URLs (Discord-Links, Bookmarks) permanent auf die neuen
// Controller (Races/Stats/Setups/Community/Application bzw. AdminLeagueManagement/
// AdminPermissions/AdminSetups) umleiten. Query-String und Rest-Pfad (z.B. /EventDetail/5)
// bleiben erhalten. Nur GET — Formulare posten bereits auf die neuen Controller.
var movedRoutes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["Home/Results"] = "/Races/Results",
    ["Home/LeagueResults"] = "/Races/LeagueResults",
    ["Home/RaceDetail"] = "/Races/RaceDetail",
    ["Home/AllRaces"] = "/Races/AllRaces",
    ["Home/RaceCalendar"] = "/Races/RaceCalendar",
    ["Home/DriverDetail"] = "/Races/DriverDetail",
    ["Home/<OWNER_HANDLE>10"] = "/Stats/<OWNER_HANDLE>10",
    ["Home/EwigeListe"] = "/Stats/EwigeListe",
    ["Home/HallOfFame"] = "/Stats/HallOfFame",
    ["Home/DriverLevels"] = "/Stats/DriverLevels",
    ["Home/DriverCards"] = "/fahrerkarten",
    ["Home/TrackSetups"] = "/Setups/TrackSetups",
    ["Home/SetupSandbox"] = "/Setups/SetupSandbox",
    ["Home/Events"] = "/Community/Events",
    ["Home/EventDetail"] = "/Community/EventDetail",
    ["Home/CommunityNews"] = "/Community/CommunityNews",
    ["Home/CommunityVotes"] = "/Community/CommunityVotes",
    ["Home/Highlights"] = "/Community/Highlights",
    ["Home/Teams"] = "/Community/Teams",
    ["Home/Team"] = "/Community/Team",
    ["Home/ReserveExchange"] = "/Community/ReserveExchange",
    ["Home/Apply"] = "/Application/Apply",
    ["Home/MyApplication"] = "/Application/MyApplication",
    ["Admin/EditLeague"] = "/AdminLeagueManagement/EditLeague",
    ["Admin/Admins"] = "/AdminPermissions/Admins",
    ["Admin/TrackSetups"] = "/AdminSetups/TrackSetups",
    ["Admin/TrackSetupStrategy"] = "/AdminSetups/TrackSetupStrategy",
};

app.Use(async (context, next) =>
{
    if (HttpMethods.IsGet(context.Request.Method))
    {
        var segments = (context.Request.Path.Value ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2 && movedRoutes.TryGetValue($"{segments[0]}/{segments[1]}", out var target))
        {
            var rest = segments.Length > 2 ? "/" + string.Join('/', segments[2..]) : string.Empty;
            context.Response.Redirect(target + rest + context.Request.QueryString, permanent: true);
            return;
        }
    }

    await next();
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapGet("/health/ready", async (AppDbContext db, CancellationToken cancellationToken) =>
{
    var canConnect = await db.Database.CanConnectAsync(cancellationToken);
    if (!canConnect)
    {
        return Results.Problem(
            title: "Database not reachable",
            detail: "Die Datenbank ist aktuell nicht erreichbar.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok(new
    {
        status = "ready",
        database = "ok",
        utc = DateTimeOffset.UtcNow
    });
}).AllowAnonymous();

app.MapControllerRoute(
    name: "uber-mich",
    pattern: "uber-mich",
    defaults: new { controller = "Home", action = "About" });

app.MapControllerRoute(
    name: "about",
    pattern: "about/{slug?}",
    defaults: new { controller = "Home", action = "About" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
