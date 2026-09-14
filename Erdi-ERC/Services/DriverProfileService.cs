using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Erdi_ERC.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Erdi_ERC.Services
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
            var nLower = n.ToLowerInvariant();

            var byTag = await _db.DriverGamerTags
                .FirstOrDefaultAsync(t => t.GamerTag != null && t.GamerTag.Trim().ToLower() == nLower, ct);
            if (byTag is not null)
            {
                return await GetByDiscordIdAsync(byTag.DiscordId, ct);
            }

            // DisplayName ODER DiscordName — explizites ToLower für SQLite-Determinismus.
            // Schließt die Lücke, wenn ein Liga-Name dem Discord-Namen entspricht.
            return await _db.DriverProfiles
                .Include(p => p.GamerTags)
                .FirstOrDefaultAsync(p =>
                    (p.DisplayName != null && p.DisplayName.Trim().ToLower() == nLower)
                 || (p.DiscordName != null && p.DiscordName.Trim().ToLower() == nLower), ct);
        }

        /// <summary>
        /// Umbenennung eines Fahrers über die Ligaverwaltung (SaveAllStandings):
        /// Nur wenn der alte Name zu einem DriverProfile aufgelöst werden kann, wird
        /// systemweit propagiert (Fahrerkarte/Profil, andere Ligen, Renn-Ergebnisse).
        /// Ohne Profil-Match passiert nichts — kein Risiko, einen gleichnamigen
        /// Fremdfahrer in einer anderen Liga umzubenennen.
        /// </summary>
        public async Task<int> RenameStandingDriverAsync(string oldName, string newName, string? actorDiscordId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(oldName)) return 0;
            var normalizedNew = newName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedNew)) return 0;
            if (string.Equals(oldName.Trim(), normalizedNew, StringComparison.OrdinalIgnoreCase)) return 0;

            var profile = await FindByDriverNameAsync(oldName, ct);
            if (profile is null) return 0;

            return await RenameIngameNameAsync(profile.DiscordId, normalizedNew, actorDiscordId, ct);
        }

        public Task<int> RenameEaNameAsync(string discordId, string newName, string? actorDiscordId, CancellationToken ct = default)
            => RenameIngameNameAsync(discordId, newName, actorDiscordId, ct);

        private static readonly System.Text.RegularExpressions.Regex HexColorRegex = new(
            "^#[0-9A-Fa-f]{6}$",
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        public string ResolveDriverNumberColor(DriverProfile profile)
        {
            if (profile is null) return "#e10600";

            var explicitColor = profile.DriverNumberColor?.Trim();
            if (!string.IsNullOrWhiteSpace(explicitColor) && HexColorRegex.IsMatch(explicitColor))
                return explicitColor;

            var team = Erdi_ERC.Helpers.F1TeamsHelper.GetTeamByName(profile.FavoriteTeam);
            if (!string.IsNullOrWhiteSpace(team?.PrimaryColor))
                return team.PrimaryColor.Trim();

            return "#e10600";
        }

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

        public async Task<IReadOnlyList<UnlinkedDriver>> GetUnlinkedDriversAsync(CancellationToken ct = default)
        {
            // 1) Vollstaendige Alias-Menge aufbauen: alle GamerTags, DisplayNames,
            //    DiscordNames aller Profile. Trim + OrdinalIgnoreCase.
            var profiles = await _db.DriverProfiles
                .AsNoTracking()
                .Include(p => p.GamerTags)
                .ToListAsync(ct);

            var aliasKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in profiles)
            {
                foreach (var tag in p.GamerTags)
                {
                    if (!string.IsNullOrWhiteSpace(tag.GamerTag))
                        aliasKeys.Add(tag.GamerTag.Trim());
                }
                if (!string.IsNullOrWhiteSpace(p.DisplayName))
                    aliasKeys.Add(p.DisplayName.Trim());
                if (!string.IsNullOrWhiteSpace(p.DiscordName))
                    aliasKeys.Add(p.DiscordName.Trim());
            }

            // 2) DriverStandings aggregieren, alles ohne Alias-Match.
            var standings = await _db.DriverStandings
                .AsNoTracking()
                .Where(s => s.Driver != null && s.Driver != "")
                .Select(s => new { s.Driver, s.LeagueId, s.DriverNumber, s.Team })
                .ToListAsync(ct);

            // 3) RaceCount pro Name (Finishes, optional ReserveMatches ueber
            //    RaceFinishes.Driver-Match). Distinct counts.
            var allNames = standings
                .Select(s => s.Driver!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var unlinkedNames = allNames
                .Where(n => !aliasKeys.Contains(n))
                .ToList();

            if (unlinkedNames.Count == 0)
                return Array.Empty<UnlinkedDriver>();

            var lower = unlinkedNames
                .Select(n => n.ToLowerInvariant())
                .Distinct()
                .ToList();

            var raceCounts = await _db.RaceFinishes
                .AsNoTracking()
                .Where(f => f.Driver != null && lower.Contains(f.Driver.Trim().ToLower()))
                .GroupBy(f => f.Driver!.Trim().ToLower())
                .Select(g => new { NameLower = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            var raceCountMap = raceCounts.ToDictionary(x => x.NameLower, x => x.Count, StringComparer.OrdinalIgnoreCase);

            // 4) Aggregation pro Name.
            var result = new List<UnlinkedDriver>();
            foreach (var name in unlinkedNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                var matches = standings
                    .Where(s => string.Equals(s.Driver!.Trim(), name, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (matches.Count == 0) continue;

                var leagues = matches
                    .Select(m => m.LeagueId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var driverNumber = matches
                    .Where(m => m.DriverNumber.HasValue)
                    .Select(m => m.DriverNumber!.Value)
                    .Cast<int?>()
                    .FirstOrDefault();
                var team = matches
                    .Where(m => !string.IsNullOrWhiteSpace(m.Team))
                    .Select(m => m.Team!.Trim())
                    .FirstOrDefault();

                raceCountMap.TryGetValue(name.ToLowerInvariant(), out var rc);

                result.Add(new UnlinkedDriver(
                    Name:           name,
                    OccurrenceCount: matches.Count,
                    DriverNumber:   driverNumber,
                    Team:           team,
                    LeagueIds:      leagues,
                    RaceCount:      rc));
            }

            return result;
        }

        public async Task<(bool Created, string? ExistingDiscordId, string Message)> LinkDriverAsync(
            string driverName, string discordId, string discordName,
            string? platform, string? actorDiscordId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(driverName))
                return (false, null, "Fahrername fehlt.");
            if (string.IsNullOrWhiteSpace(discordId))
                return (false, null, "Discord-ID fehlt.");
            if (string.IsNullOrWhiteSpace(discordName))
                return (false, null, "Discord-Name fehlt.");

            var name = driverName.Trim();
            var dId  = discordId.Trim();
            var dName = discordName.Trim();
            var platformNorm = string.IsNullOrWhiteSpace(platform) ? "EA" : platform.Trim();

            // 1) Existiert bereits ein DriverProfile mit dieser DiscordId?
            //    AsTracking: DbContext laeuft per Default auf NoTracking — ohne
            //    explizites Tracking wuerden Tag-Add/DisplayName-Aenderungen
            //    still verschluckt (siehe UnlinkDriverAsync).
            var existing = await _db.DriverProfiles
                .AsTracking()
                .Include(p => p.GamerTags)
                .FirstOrDefaultAsync(p => p.DiscordId == dId, ct);
            if (existing is not null)
            {
                // Wenn der angefragte Name schon ein Alias dieses Profils ist: no-op.
                var already = existing.GamerTags.Any(t =>
                    string.Equals(t.GamerTag.Trim(), name, StringComparison.OrdinalIgnoreCase))
                    || string.Equals(existing.DisplayName?.Trim() ?? "", name, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(existing.DiscordName.Trim(), name, StringComparison.OrdinalIgnoreCase);

                if (already)
                {
                    return (false, dId, $"\"{name}\" ist bereits mit {dId} verknuepft.");
                }

                // Andernfalls: DriverGamerTag an das bestehende Profil anhaengen.
                // Unique-Index (DiscordId, Platform): nur EIN Tag pro Plattform.
                // Belegt ein vorhandener Tag diese Plattform schon, registrieren
                // wir den Alias stattdessen ueber DisplayName (falls frei) —
                // niemals einen zweiten Tag anhaengen (sonst DbUpdateException/500).
                var platformTag = existing.GamerTags.FirstOrDefault(t =>
                    string.Equals(t.Platform, platformNorm, StringComparison.OrdinalIgnoreCase));

                if (platformTag is null)
                {
                    existing.GamerTags.Add(new DriverGamerTag
                    {
                        DiscordId         = dId,
                        Platform          = platformNorm,
                        GamerTag          = name,
                        IsPrimary         = existing.GamerTags.Count == 0,
                        LinkedAt          = DateTime.UtcNow,
                        LinkedByDiscordId = actorDiscordId
                    });
                    existing.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync(ct);
                    return (true, dId, $"Bestehender DriverProfile {dId} um Alias \"{name}\" erweitert.");
                }

                if (string.IsNullOrWhiteSpace(existing.DisplayName))
                {
                    existing.DisplayName = name;
                    existing.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync(ct);
                    return (true, dId,
                        $"\"{name}\" als DisplayName an Profil {dId} ({existing.DiscordName}, " +
                        $"Tag \"{platformTag.GamerTag}\" auf {platformNorm}) verknuepft.");
                }

                return (false, dId,
                    $"Profil {dId} ({existing.DiscordName}) hat \"{platformTag.GamerTag}\" bereits auf {platformNorm}, " +
                    $"und DisplayName \"{existing.DisplayName}\" ist belegt. Andere Plattform waehlen oder Ingame-Namen anpassen.");
            }

            // 2) Existiert bereits ein Profil mit einem matchenden Alias?
            //    In dem Fall leiten wir um, statt ein zweites Profil anzulegen.
            var aliasMatch = await _db.DriverGamerTags
                .FirstOrDefaultAsync(t => t.GamerTag == name, ct);
            if (aliasMatch is not null)
            {
                return (false, aliasMatch.DiscordId,
                    $"\"{name}\" ist bereits mit Discord-ID {aliasMatch.DiscordId} verknuepft. Erst Unlink durchfuehren.");
            }

            // 3) Frisch anlegen.
            var profile = new DriverProfile
            {
                DiscordId         = dId,
                DiscordName       = dName,
                DisplayName       = name,
                PreferredPlatform = platformNorm,
                CreatedAt         = DateTime.UtcNow,
                UpdatedAt         = DateTime.UtcNow,
                GamerTags = new List<DriverGamerTag>
                {
                    new()
                    {
                        DiscordId         = dId,
                        Platform          = platformNorm,
                        GamerTag          = name,
                        IsPrimary         = true,
                        LinkedAt          = DateTime.UtcNow,
                        LinkedByDiscordId = actorDiscordId
                    }
                }
            };
            _db.DriverProfiles.Add(profile);
            await _db.SaveChangesAsync(ct);
            return (true, dId, $"Neuer DriverProfile fuer \"{name}\" angelegt (Discord {dId}).");
        }

        public async Task<int> UnlinkDriverAsync(string discordId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(discordId)) return -1;

            // AsTracking erzwingen: DbContext laeuft per Default auf
            // NoTrackingWithIdentityResolution (siehe Program.cs und
            // SqliteTestContext); ohne explizites Tracking wuerde EF die
            // bereits aus dem Store geladenen GamerTags beim Remove() als
            // "andere Instanz mit gleichem Schluessel" zurueckweisen.
            var profile = await _db.DriverProfiles
                .AsTracking()
                .Include(p => p.GamerTags)
                .FirstOrDefaultAsync(p => p.DiscordId == discordId, ct);
            if (profile is null) return 0;

            var tagCount = profile.GamerTags.Count;
            if (tagCount == 0)
            {
                _db.DriverProfiles.Remove(profile);
                await _db.SaveChangesAsync(ct);
                return 0;
            }

            // Default: nur den ersten GamerTag loeschen, Profil bleibt.
            // Wenn danach 0 Tags uebrig sind, loeschen wir das Profil ebenfalls.
            var firstTag = profile.GamerTags.OrderBy(t => t.LinkedAt).First();
            _db.DriverGamerTags.Remove(firstTag);
            await _db.SaveChangesAsync(ct);

            if (profile.GamerTags.Count == 0)
            {
                _db.DriverProfiles.Remove(profile);
                await _db.SaveChangesAsync(ct);
            }
            return 1;
        }
    }
}
