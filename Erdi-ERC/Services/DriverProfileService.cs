using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace <OWNER_HANDLE>_ERC.Services
{
    public class DriverProfileService : IDriverProfileService
    {
        private readonly AppDbContext _db;
        private readonly DriverMatchingOptions _options;

        public DriverProfileService(AppDbContext db, IOptions<DriverMatchingOptions> options)
        {
            _db = db;
            _options = options.Value;
        }

        public async Task<DriverProfile> LinkApplicationAsync(ApplicationForm app, string? actorDiscordId, CancellationToken ct = default)
        {
            // Bewerbungen tragen aktuell keine Discord-Id mit. Wir nutzen den Discord-Namen als
            // stabilen Schlüssel-Fallback (Format "name" / "name***REMOVED***1234") für ältere Bewerbungen.
            // Sobald die Bewerbung eine echte DiscordId hat, wird die genutzt.
            var discordId = !string.IsNullOrWhiteSpace(app.DiscordId)
                ? app.DiscordId.Trim()
                : "name:" + app.DiscordName.Trim();

            var profile = await _db.DriverProfiles
                .AsTracking()
                .Include(p => p.GamerTags)
                .FirstOrDefaultAsync(p => p.DiscordId == discordId, ct);

            if (profile is null)
            {
                profile = new DriverProfile
                {
                    DiscordId = discordId,
                    DiscordName = app.DiscordName,
                    DisplayName = app.GamingName,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.DriverProfiles.Add(profile);
            }
            else
            {
                profile.DiscordName = app.DiscordName;
                profile.UpdatedAt = DateTime.UtcNow;
                if (string.IsNullOrWhiteSpace(profile.DisplayName))
                {
                    profile.DisplayName = app.GamingName;
                }
            }

            if (!string.IsNullOrWhiteSpace(app.Platform) && !string.IsNullOrWhiteSpace(app.GamingName))
            {
                var platform = app.Platform.Trim();
                var tag = app.GamingName.Trim();

                var existing = profile.GamerTags
                    .FirstOrDefault(t => string.Equals(t.Platform, platform, StringComparison.OrdinalIgnoreCase));

                if (existing is null)
                {
                    profile.GamerTags.Add(new DriverGamerTag
                    {
                        DiscordId = discordId,
                        Platform = platform,
                        GamerTag = tag,
                        IsPrimary = profile.GamerTags.Count == 0,
                        LinkedAt = DateTime.UtcNow,
                        LinkedByDiscordId = actorDiscordId
                    });
                }
                else
                {
                    existing.GamerTag = tag;
                    existing.LinkedAt = DateTime.UtcNow;
                    existing.LinkedByDiscordId = actorDiscordId;
                }
            }

            await _db.SaveChangesAsync(ct);
            return profile;
        }

        public async Task<IReadOnlyList<DriverNameSuggestion>> SuggestAsync(string query, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < _options.MinQueryLength)
            {
                return Array.Empty<DriverNameSuggestion>();
            }

            var q = query.Trim();
            var tags = await _db.DriverGamerTags
                .Join(_db.DriverProfiles, t => t.DiscordId, p => p.DiscordId, (t, p) => new { t, p })
                .ToListAsync(ct);

            var scored = tags
                .Select(x =>
                {
                    var dist = Levenshtein(x.t.GamerTag, q);
                    var exact = string.Equals(x.t.GamerTag, q, StringComparison.OrdinalIgnoreCase);
                    return new DriverNameSuggestion(
                        x.t.DiscordId,
                        x.t.Platform,
                        x.t.GamerTag,
                        x.p.DiscordName,
                        dist,
                        exact);
                })
                .Where(s => s.ExactMatch
                    || s.GamerTag.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || s.Distance <= _options.MaxLevenshteinDistance)
                .OrderByDescending(s => s.ExactMatch)
                .ThenBy(s => s.Distance)
                .ThenBy(s => s.GamerTag, StringComparer.OrdinalIgnoreCase)
                .Take(_options.MaxSuggestions)
                .ToList();

            return scored;
        }

        public async Task<DriverNameMatch> ResolveAsync(string input, CancellationToken ct = default)
        {
            var trimmed = input?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return new DriverNameMatch(string.Empty, null, null, false, Array.Empty<DriverNameSuggestion>());
            }

            var suggestions = await SuggestAsync(trimmed, ct);
            var exact = suggestions.FirstOrDefault(s => s.ExactMatch);
            if (exact is not null)
            {
                return new DriverNameMatch(exact.GamerTag, exact.DiscordId, exact.Platform, true, suggestions);
            }

            return new DriverNameMatch(trimmed, null, null, false, suggestions);
        }

        public async Task<DriverProfile?> GetByDiscordIdAsync(string discordId, CancellationToken ct = default)
        {
            return await _db.DriverProfiles
                .Include(p => p.GamerTags)
                .FirstOrDefaultAsync(p => p.DiscordId == discordId, ct);
        }

        public async Task<DriverProfile?> FindByDriverNameAsync(string driverName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(driverName)) return null;
            var n = driverName.Trim();

            var byTag = await _db.DriverGamerTags
                .FirstOrDefaultAsync(t => t.GamerTag == n, ct);
            if (byTag is not null)
            {
                return await GetByDiscordIdAsync(byTag.DiscordId, ct);
            }

            return await _db.DriverProfiles
                .Include(p => p.GamerTags)
                .FirstOrDefaultAsync(p => p.DisplayName == n, ct);
        }

        private static int Levenshtein(string a, string b)
        {
            a ??= string.Empty;
            b ??= string.Empty;
            if (a.Length == 0) return b.Length;
            if (b.Length == 0) return a.Length;

            // case-insensitiver Vergleich
            a = a.ToLowerInvariant();
            b = b.ToLowerInvariant();

            var prev = new int[b.Length + 1];
            var curr = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) prev[j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                curr[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(
                        Math.Min(curr[j - 1] + 1, prev[j] + 1),
                        prev[j - 1] + cost);
                }
                (prev, curr) = (curr, prev);
            }

            return prev[b.Length];
        }
    }
}
