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
                    PreferredPlatform = app.Platform?.Trim(),
                    InputDevice = string.IsNullOrWhiteSpace(app.SimHardware) ? null : app.SimHardware.Trim(),
                    FavoriteTeam = string.IsNullOrWhiteSpace(app.PreferredTeam) ? null : app.PreferredTeam.Trim(),
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
                    profile.DisplayName = app.GamingName;
                if (string.IsNullOrWhiteSpace(profile.PreferredPlatform))
                    profile.PreferredPlatform = app.Platform?.Trim();
                // Hardware/Wunsch-Team aus der Bewerbung nur ergänzen, nie ein bestehendes
                // (vom Admin/Fahrer gepflegtes) Profilfeld überschreiben.
                if (string.IsNullOrWhiteSpace(profile.InputDevice) && !string.IsNullOrWhiteSpace(app.SimHardware))
                    profile.InputDevice = app.SimHardware.Trim();
                if (string.IsNullOrWhiteSpace(profile.FavoriteTeam) && !string.IsNullOrWhiteSpace(app.PreferredTeam))
                    profile.FavoriteTeam = app.PreferredTeam.Trim();
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

        public Task<int> RenameEaNameAsync(string discordId, string newName, string? actorDiscordId, CancellationToken ct = default)
            => RenameIngameNameAsync(discordId, newName, actorDiscordId, ct);

        public async Task<int> RenameIngameNameAsync(string discordId, string newName, string? actorDiscordId, CancellationToken ct = default)
        {
            var profile = await _db.DriverProfiles
                .AsTracking()
                .Include(p => p.GamerTags)
                .FirstOrDefaultAsync(p => p.DiscordId == discordId, ct)
                ?? throw new InvalidOperationException($"DriverProfile {discordId} not found.");

            var normalized = newName.Trim();
            var platform = !string.IsNullOrWhiteSpace(profile.PreferredPlatform)
                ? profile.PreferredPlatform.Trim()
                : "EA";

            var existingTag = profile.GamerTags
                .FirstOrDefault(t => string.Equals(t.Platform, platform, StringComparison.OrdinalIgnoreCase));

            // All names this driver may currently appear as in standings/finishes.
            // Every linked gamer tag counts — not only the preferred platform — plus the
            // display and Discord names, so a rename propagates regardless of which alias
            // a given result was originally entered under.
            var oldAliases = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tag in profile.GamerTags)
                if (!string.IsNullOrWhiteSpace(tag.GamerTag)) oldAliases.Add(tag.GamerTag.Trim());
            if (!string.IsNullOrWhiteSpace(profile.DisplayName)) oldAliases.Add(profile.DisplayName.Trim());
            if (!string.IsNullOrWhiteSpace(profile.DiscordName)) oldAliases.Add(profile.DiscordName.Trim());
            oldAliases.Remove(normalized);

            if (existingTag is null)
            {
                profile.GamerTags.Add(new DriverGamerTag
                {
                    DiscordId = discordId,
                    Platform = platform,
                    GamerTag = normalized,
                    IsPrimary = profile.GamerTags.Count == 0,
                    LinkedAt = DateTime.UtcNow,
                    LinkedByDiscordId = actorDiscordId
                });
            }
            else
            {
                existingTag.GamerTag = normalized;
                existingTag.LinkedAt = DateTime.UtcNow;
                existingTag.LinkedByDiscordId = actorDiscordId;
                existingTag.IsPrimary = true;
            }

            profile.DisplayName = normalized;
            if (string.IsNullOrWhiteSpace(profile.PreferredPlatform))
                profile.PreferredPlatform = platform;
            profile.UpdatedAt = DateTime.UtcNow;

            var changed = 0;
            foreach (var oldName in oldAliases)
                changed += await RenameReferencesAsync(profile, oldName, normalized, ct);

            await _db.SaveChangesAsync(ct);
            return changed;
        }

        private async Task<int> RenameReferencesAsync(DriverProfile profile, string oldName, string newName, CancellationToken ct)
        {
            var changed = 0;

            // Match trim- and case-insensitively: standings/results are often entered by
            // hand, so a stored name can differ from the profile alias only by casing or
            // stray whitespace. An exact (ordinal) compare would silently miss those rows
            // and leave the old name on the public results page.
            var oldLower = oldName.Trim().ToLowerInvariant();
            bool Matches(string? value) =>
                value != null && string.Equals(value.Trim(), oldName.Trim(), StringComparison.OrdinalIgnoreCase);

            var standings = await _db.DriverStandings.AsTracking()
                .Where(x => (x.Driver != null && x.Driver.Trim().ToLower() == oldLower)
                         || (x.ReserveForDriver != null && x.ReserveForDriver.Trim().ToLower() == oldLower))
                .ToListAsync(ct);
            foreach (var s in standings)
            {
                if (Matches(s.Driver))           { s.Driver = newName; changed++; }
                if (Matches(s.ReserveForDriver)) { s.ReserveForDriver = newName; changed++; }
            }

            var raceResults = await _db.RaceResults.AsTracking()
                .Where(x => (x.Winner != null && x.Winner.Trim().ToLower() == oldLower)
                         || (x.FastestLap != null && x.FastestLap.Trim().ToLower() == oldLower))
                .ToListAsync(ct);
            foreach (var r in raceResults)
            {
                if (Matches(r.Winner))     { r.Winner = newName; changed++; }
                if (Matches(r.FastestLap)) { r.FastestLap = newName; changed++; }
            }

            var finishes = await _db.RaceFinishes.AsTracking()
                .Where(x => x.Driver != null && x.Driver.Trim().ToLower() == oldLower)
                .ToListAsync(ct);
            foreach (var f in finishes) { f.Driver = newName; changed++; }

            var reserves = await _db.RaceReserveAssignments.AsTracking()
                .Where(x => (x.ReserveDriver != null && x.ReserveDriver.Trim().ToLower() == oldLower)
                         || (x.MainDriver != null && x.MainDriver.Trim().ToLower() == oldLower))
                .ToListAsync(ct);
            foreach (var a in reserves)
            {
                if (Matches(a.ReserveDriver)) { a.ReserveDriver = newName; changed++; }
                if (Matches(a.MainDriver))    { a.MainDriver = newName; changed++; }
            }

            var penalties = await _db.LeaguePenalties.AsTracking()
                .Where(x => x.Driver != null && x.Driver.Trim().ToLower() == oldLower)
                .ToListAsync(ct);
            foreach (var p in penalties) { p.Driver = newName; changed++; }

            var achievements = await _db.CustomAchievements.AsTracking()
                .Where(x => x.Driver != null && x.Driver.Trim().ToLower() == oldLower)
                .ToListAsync(ct);
            foreach (var a in achievements) { a.Driver = newName; changed++; }

            var applications = await _db.ApplicationForms.AsTracking()
                .Where(x => x.GamingName != null && x.GamingName.Trim().ToLower() == oldLower
                    && ((x.DiscordId != null && x.DiscordId == profile.DiscordId) || x.DiscordName == profile.DiscordName))
                .ToListAsync(ct);
            foreach (var a in applications) { a.GamingName = newName; changed++; }

            return changed;
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
