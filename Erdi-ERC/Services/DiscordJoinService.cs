using Erdi_ERC.Options;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Erdi_ERC.Services
{
    /// <summary>Ausgang eines automatischen Server-Beitritts.</summary>
    public enum DiscordJoinOutcome
    {
        /// <summary>Der Nutzer ist jetzt in allen konfigurierten Servern.</summary>
        Joined,

        /// <summary>Der Nutzer war bereits überall Mitglied — nichts zu tun.</summary>
        AlreadyMember,

        /// <summary>Kein Bot-Token oder kein Access-Token: nichts versucht.</summary>
        Skipped,

        /// <summary>Mindestens ein Beitritt ist fehlgeschlagen (Grund in <see cref="DiscordJoinResult.Detail"/>).</summary>
        Failed
    }

    public record DiscordJoinResult(DiscordJoinOutcome Outcome, string? Detail)
    {
        public static DiscordJoinResult Skipped(string reason) => new(DiscordJoinOutcome.Skipped, reason);
    }

    /// <summary>
    /// Holt einen Bewerber per Discord-API automatisch in beide Server
    /// (<c>PUT /guilds/{id}/members/{user}</c>). Voraussetzungen:
    /// Bot im Server mit <c>CREATE_INSTANT_INVITE</c>, Bot-Token als <c>Discord:BotToken</c>,
    /// und der OAuth-Token des Nutzers braucht den Scope <c>guilds.join</c>.
    /// </summary>
    public interface IDiscordJoinService
    {
        Task<DiscordJoinResult> EnsureJoinedAsync(string discordId, string? accessToken, CancellationToken ct = default);
    }

    public class DiscordJoinService : IDiscordJoinService
    {
        private const string EndpointTemplate = "https://discord.com/api/v10/guilds/{0}/members/{1}";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ApplicationOptions _appOptions;
        private readonly DiscordGuildOptions _guildOptions;
        private readonly ILogger<DiscordJoinService> _logger;
        private readonly string? _botToken;

        public DiscordJoinService(
            IHttpClientFactory httpClientFactory,
            IOptions<ApplicationOptions> appOptions,
            IOptions<DiscordGuildOptions> guildOptions,
            IConfiguration configuration,
            ILogger<DiscordJoinService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _appOptions = appOptions.Value;
            _guildOptions = guildOptions.Value;
            _logger = logger;
            _botToken = configuration["Discord:BotToken"];
        }

        public async Task<DiscordJoinResult> EnsureJoinedAsync(string discordId, string? accessToken, CancellationToken ct = default)
        {
            // Ohne Bot-Token oder ohne guilds.join-Token gibt es nichts zu tun. Bewerbung
            // läuft weiter — der Beitritt ist Komfort, keine Bedingung.
            if (string.IsNullOrWhiteSpace(_botToken))
                return DiscordJoinResult.Skipped("kein Bot-Token konfiguriert");
            if (string.IsNullOrWhiteSpace(accessToken))
                return DiscordJoinResult.Skipped("kein Access-Token der Sitzung");
            if (string.IsNullOrWhiteSpace(discordId))
                return DiscordJoinResult.Skipped("keine Discord-Id");

            var guilds = new[] { _guildOptions.CommunityGuildId, _guildOptions.LeagueGuildId }
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct()
                .ToArray();

            var client = _httpClientFactory.CreateClient("DiscordApi");
            var joinedAny = false;
            var failures = new List<string>();

            foreach (var guildId in guilds)
            {
                using var req = new HttpRequestMessage(
                    HttpMethod.Put, string.Format(EndpointTemplate, guildId, discordId));
                req.Headers.Authorization = new AuthenticationHeaderValue("Bot", _botToken);
                req.Headers.UserAgent.Add(new ProductInfoHeaderValue(_appOptions.UserAgentName, _appOptions.UserAgentVersion));
                req.Content = new StringContent(
                    JsonSerializer.Serialize(new { access_token = accessToken }),
                    Encoding.UTF8,
                    "application/json");

                HttpResponseMessage response;
                try
                {
                    response = await client.SendAsync(req, ct);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    _logger.LogWarning(ex, "Discord auto-join: Netzwerkfehler für Guild {GuildId}", guildId);
                    failures.Add($"{guildId}: Netzwerkfehler");
                    continue;
                }

                switch ((int)response.StatusCode)
                {
                    case 201:
                        joinedAny = true;
                        _logger.LogInformation("Discord auto-join: {DiscordId} in {GuildId} aufgenommen", discordId, guildId);
                        break;
                    case 204:
                        // Schon Mitglied — PUT ist idempotent.
                        break;
                    default:
                        var body = await response.Content.ReadAsStringAsync(ct);
                        _logger.LogWarning("Discord auto-join fehlgeschlagen für Guild {GuildId}: {Status} {Body}",
                            guildId, (int)response.StatusCode, body);
                        failures.Add($"{guildId}: HTTP {(int)response.StatusCode}");
                        break;
                }
            }

            if (failures.Count > 0)
                return new DiscordJoinResult(DiscordJoinOutcome.Failed, string.Join(", ", failures));
            return new DiscordJoinResult(joinedAny ? DiscordJoinOutcome.Joined : DiscordJoinOutcome.AlreadyMember, null);
        }
    }
}
