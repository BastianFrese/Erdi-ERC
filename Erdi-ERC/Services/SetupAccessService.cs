using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace <OWNER_HANDLE>_ERC.Services
{
    public sealed class SetupAccessService : ISetupAccessService
    {
        private readonly DiscordSetupAccessOptions _setupAccessOptions;
        private readonly AppDbContext _db;
        private readonly ILogger<SetupAccessService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public SetupAccessService(
            IOptions<DiscordSetupAccessOptions> setupAccessOptions,
            AppDbContext db,
            ILogger<SetupAccessService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _setupAccessOptions = setupAccessOptions.Value;
            _db = db;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<SetupAccessResolution> ResolveSetupAccessAsync(string? accessToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return new SetupAccessResolution { Tier = 1, Success = false };
            }

            var guildId = _setupAccessOptions.GuildId;
            if (string.IsNullOrWhiteSpace(guildId))
            {
                return new SetupAccessResolution { Tier = 1, Success = false };
            }

            var mappings = await _db.SetupAccessRoleMappings
                .Where(x => x.Tier >= 3 && x.Tier <= 5)
                .OrderByDescending(x => x.Tier)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

            if (mappings.Count == 0)
            {
                // Keine Rollen-Mappings konfiguriert -> kein Setup-Zugang (Tier 0), aber der
                // User bleibt eingeloggt. Success=false würde in OnValidatePrincipal zum
                // RejectPrincipal/SignOut führen (Logout bzw. Login-Loop).
                return new SetupAccessResolution { Tier = 0, Success = true, IsOnCommunityGuild = false };
            }

            try
            {
                // IHttpClientFactory: gepoolter Client, kein Socket-Leak unter Last.
                var client = _httpClientFactory.CreateClient("DiscordApi");
                using var guildsReq = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v10/users/@me/guilds");
                guildsReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                guildsReq.Headers.UserAgent.Add(new ProductInfoHeaderValue("<OWNER_HANDLE>-ERC", "1.0"));

                var guildsResponse = await client.SendAsync(guildsReq, cancellationToken);
                if (!guildsResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Discord guild lookup failed with status code {StatusCode}", guildsResponse.StatusCode);
                    return new SetupAccessResolution { Tier = 1, Success = false };
                }

                var guildsJson = await guildsResponse.Content.ReadAsStringAsync(cancellationToken);
                using var guildsDoc = JsonDocument.Parse(guildsJson);
                var isInConfiguredGuild = guildsDoc.RootElement
                    .EnumerateArray()
                    .Any(x => x.TryGetProperty("id", out var id) && id.GetString() == guildId);

                if (!isInConfiguredGuild)
                {
                    return new SetupAccessResolution { Tier = 0, Success = true, IsOnCommunityGuild = false };
                }

                // Auth-Header pro Request (Factory-Client hat keine DefaultHeaders).
                using var memberReq = new HttpRequestMessage(HttpMethod.Get, $"https://discord.com/api/v10/users/@me/guilds/{guildId}/member");
                memberReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                memberReq.Headers.UserAgent.Add(new ProductInfoHeaderValue("<OWNER_HANDLE>-ERC", "1.0"));
                var memberResponse = await client.SendAsync(memberReq, cancellationToken);
                if (!memberResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Discord member lookup failed with status code {StatusCode}", memberResponse.StatusCode);
                    return new SetupAccessResolution { Tier = 1, Success = false, IsOnCommunityGuild = true };
                }

                var memberJson = await memberResponse.Content.ReadAsStringAsync(cancellationToken);
                using var memberDoc = JsonDocument.Parse(memberJson);

                DateTimeOffset? joinedAt = null;
                if (memberDoc.RootElement.TryGetProperty("joined_at", out var joinedAtElement)
                    && joinedAtElement.ValueKind == JsonValueKind.String
                    && DateTimeOffset.TryParse(joinedAtElement.GetString(), out var parsedJoinedAt))
                {
                    joinedAt = parsedJoinedAt.ToUniversalTime();
                }

                // Tenure-Check: User muss mindestens N Tage auf der Guild sein, sonst Tier=0
                // (er ist zwar drin, aber Setup-Zugang ist gesperrt). Verhindert Drive-by-Joiner.
                var tenureRequired = TimeSpan.FromDays(Math.Max(0, _setupAccessOptions.MinGuildTenureDays));
                var tenureMet = tenureRequired <= TimeSpan.Zero
                    || (joinedAt.HasValue && DateTimeOffset.UtcNow - joinedAt.Value >= tenureRequired);

                int ResolvedTier(int baseTier) => tenureMet ? baseTier : 0;

                if (!memberDoc.RootElement.TryGetProperty("roles", out var rolesElement) || rolesElement.ValueKind != JsonValueKind.Array)
                {
                    return new SetupAccessResolution
                    {
                        Tier = ResolvedTier(1),
                        Success = true,
                        IsOnCommunityGuild = true,
                        GuildJoinedAtUtc = joinedAt,
                        IsPendingTenure = !tenureMet
                    };
                }

                var roleIds = rolesElement
                    .EnumerateArray()
                    .Select(x => x.GetString())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToHashSet(StringComparer.Ordinal);

                var bestMatch = mappings.FirstOrDefault(m => roleIds.Contains(m.RoleId));
                if (bestMatch is null)
                {
                    return new SetupAccessResolution
                    {
                        Tier = ResolvedTier(1),
                        Success = true,
                        IsOnCommunityGuild = true,
                        GuildJoinedAtUtc = joinedAt,
                        IsPendingTenure = !tenureMet
                    };
                }

                var label = string.IsNullOrWhiteSpace(bestMatch.Label) ? null : bestMatch.Label.Trim();
                return new SetupAccessResolution
                {
                    Tier = ResolvedTier(bestMatch.Tier),
                    RoleLabel = tenureMet ? label : null,
                    Success = true,
                    IsOnCommunityGuild = true,
                    GuildJoinedAtUtc = joinedAt,
                    IsPendingTenure = !tenureMet
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Resolving setup access via Discord failed (transient error – keeping existing claims)");
                return new SetupAccessResolution { Tier = 1, Success = false, IsTransientError = true };
            }
        }
    }
}
