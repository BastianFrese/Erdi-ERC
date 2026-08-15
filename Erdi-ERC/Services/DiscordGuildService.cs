using Erdi_ERC.Options;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Erdi_ERC.Services
{
    /// <summary>
    /// Guild-Verifikation gegen die Discord-API. Nutzt den gepoolten Named-Client
    /// "DiscordApi" (IHttpClientFactory verhindert Socket-Exhaustion unter Last).
    /// </summary>
    public class DiscordGuildService : IDiscordGuildService
    {
        private const string GuildsEndpoint = "https://discord.com/api/v10/users/@me/guilds";
        private const string LoginExpiredMessage = "Discord-Login abgelaufen. Bitte erneut anmelden.";
        private const string UnavailableMessage = "Discord-Server konnte nicht erreicht werden. Bitte versuche es später erneut.";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ApplicationOptions _appOptions;
        private readonly DiscordGuildOptions _guildOptions;
        private readonly ILogger<DiscordGuildService> _logger;

        public DiscordGuildService(
            IHttpClientFactory httpClientFactory,
            IOptions<ApplicationOptions> appOptions,
            IOptions<DiscordGuildOptions> guildOptions,
            ILogger<DiscordGuildService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _appOptions = appOptions.Value;
            _guildOptions = guildOptions.Value;
            _logger = logger;
        }

        public async Task<DiscordGuildCheckResult> CheckMembershipAsync(string? accessToken, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(accessToken))
                return new(DiscordGuildCheckStatus.LoginExpired, false, false, LoginExpiredMessage);

            var client = _httpClientFactory.CreateClient("DiscordApi");
            using var req = new HttpRequestMessage(HttpMethod.Get, GuildsEndpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue(_appOptions.UserAgentName, _appOptions.UserAgentVersion));

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(req, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Discord guild lookup network error");
                return new(DiscordGuildCheckStatus.Unavailable, false, false, UnavailableMessage);
            }

            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                return new(DiscordGuildCheckStatus.LoginExpired, false, false, LoginExpiredMessage);

            if (!response.IsSuccessStatusCode)
                return new(DiscordGuildCheckStatus.Unavailable, false, false, UnavailableMessage);

            var content = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(content);
            bool community = false, league = false;
            foreach (var guild in doc.RootElement.EnumerateArray())
            {
                var id = guild.GetProperty("id").GetString();
                if (id == _guildOptions.CommunityGuildId) community = true;
                if (id == _guildOptions.LeagueGuildId) league = true;
            }

            return new(DiscordGuildCheckStatus.Ok, community, league, null);
        }
    }
}
