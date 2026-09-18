using Erdi_ERC.Data;
using Erdi_ERC.Helpers;
using Erdi_ERC.Models;
using Erdi_ERC.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Erdi_ERC.Services;

/// <summary>Eine Liga in <see cref="StandingsExport"/> (Key = leagueId der URLs, z. B. „pro").</summary>
public sealed record StandingsExportLiga(string Key, string Name, int SortOrder);

/// <summary>Saison-Bilanz eines Fahrers (feldkompatibel zum alten HTML-Scraper des Overlays).</summary>
public sealed record StandingsExportSaison(
    int Rennen, int Siege, int Podien, int Punkte, string? BestFinish, string? WinRate);

/// <summary>
/// Ein Fahrer einer Liga. Die Felder <c>Name/Ingame/Team/TeamLogo/Saison/Liga</c> sind
/// kompatibel zum erc-drivers.json des ERCTelemetry-Scrapers, damit die Overlay-Seiten
/// ohne Änderung weiterlaufen; <c>Position</c> ist die autoritative Tabellenposition.
/// </summary>
public sealed record StandingsExportFahrer(
    string Id, string Url, string Name, string? Discord,
    string? Ingame, string? Plattform,
    string Team, string? TeamLogo,
    string Liga, string LigaName,
    int Position, int? Nummer,
    bool IstReserve, string? ReserveFuer,
    StandingsExportSaison Saison);

/// <summary>Echte Konstrukteurswertung einer Liga (Reserve/Gast-Logik via RaceTeamHelper).</summary>
public sealed record StandingsExportTeam(
    string Liga, string Team, int Position, int Punkte, int Siege, int Podien);

/// <summary>Antwort von GET /api/telemetry/standings.</summary>
public sealed record StandingsExport(
    DateTime Geholt, string Quelle,
    IReadOnlyList<StandingsExportLiga> Ligen,
    IReadOnlyList<StandingsExportFahrer> Fahrer,
    IReadOnlyList<StandingsExportTeam> Teams);

public interface IStandingsExportService
{
    /// <summary>
    /// Fahrer- + Teamwertung aller aktiven Ligen aus der DB — dieselbe Quelle wie die
    /// Standings-Seiten (DriverStandings inkl. Punkte-Anpassungen/Saison-Filter, echte
    /// Team-Punkte via <see cref="RaceTeamHelper.ComputeTeamPointsForLeague"/>).
    /// </summary>
    Task<StandingsExport> HoleAsync(CancellationToken ct = default);
}

public class StandingsExportService : IStandingsExportService
{
    private const string BasisUrl = "https://erdi-erc.de";

    private readonly AppDbContext _db;
    private readonly int[] _f1PointMap;

    public StandingsExportService(AppDbContext db, IOptions<F1ScoringOptions> f1Scoring)
    {
        _db = db;
        var configuredMap = f1Scoring.Value.PointMap;
        _f1PointMap = configuredMap is { Length: > 0 }
            ? configuredMap
            : new[] { 25, 21, 18, 16, 14, 12, 10, 8, 7, 6, 5, 4, 3, 2, 1, 0, 0, 0, 0, 0 };
    }

    public async Task<StandingsExport> HoleAsync(CancellationToken ct = default)
    {
        var ligen = await _db.Leagues
            .AsNoTracking()
            .AsSplitQuery()
            .Where(l => !l.IsArchived)
            .Include(l => l.Standings)
            .Include(l => l.Races).ThenInclude(r => r.Finishes)
            .Include(l => l.Races).ThenInclude(r => r.ReserveAssignments)
            .Include(l => l.Races).ThenInclude(r => r.GuestAssignments)
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Name)
            .ToListAsync(ct);

        var profiles = await _db.DriverProfiles
            .AsNoTracking()
            .Include(p => p.GamerTags)
            .ToListAsync(ct);

        // Standings-Driver → Profil (GamerTags + DisplayName + DiscordName), wie /fahrerkarten.
        var tagToProfile = new Dictionary<string, DriverProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in profiles)
        {
            foreach (var alias in DriverAliasHelper.Build(p))
                tagToProfile.TryAdd(alias, p);
        }

        var fahrer = new List<StandingsExportFahrer>();
        var teams = new List<StandingsExportTeam>();
        var ligaInfos = new List<StandingsExportLiga>();

        foreach (var liga in ligen)
        {
            ligaInfos.Add(new StandingsExportLiga(liga.Id, liga.Name, liga.SortOrder));

            // Rennen der aktuellen Saison zählen, wenn die Liga eine gesetzt hat.
            var races = liga.Races
                .Where(r => string.IsNullOrWhiteSpace(liga.CurrentSeason) || r.Season == liga.CurrentSeason)
                .OrderBy(r => r.Date).ThenBy(r => r.RowId)
                .ToList();

            foreach (var standing in liga.Standings.Where(s => !string.IsNullOrWhiteSpace(s.Driver)).OrderBy(s => s.Position))
            {
                var driverName = standing.Driver.Trim();
                var profile = tagToProfile.TryGetValue(driverName, out var p) ? p : null;
                var aliases = profile is not null ? DriverAliasHelper.Build(profile) : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { driverName };

                var finishes = races
                    .Select(r => r.Finishes.FirstOrDefault(f =>
                        !string.IsNullOrWhiteSpace(f.Driver) &&
                        (aliases.Contains(f.Driver.Trim()) || f.Driver.Trim().Equals(driverName, StringComparison.OrdinalIgnoreCase))))
                    .Where(f => f is not null)
                    .Select(f => f!)
                    .ToList();

                var rennen = finishes.Count;
                var siege = finishes.Count(f => f.Position == 1);
                var podien = finishes.Count(f => f.Position is >= 1 and <= 3);
                var bestPos = finishes.Where(f => f.Position > 0).Select(f => f.Position).DefaultIfEmpty(0).Min();

                var ingameTag = profile?.GamerTags.FirstOrDefault(t => t.IsPrimary) ?? profile?.GamerTags.FirstOrDefault();

                fahrer.Add(new StandingsExportFahrer(
                    Id: profile?.DiscordId ?? string.Empty,
                    Url: profile is not null ? $"{BasisUrl}/Profile/{profile.DiscordId}" : string.Empty,
                    Name: profile?.DisplayName ?? profile?.DiscordName ?? driverName,
                    Discord: profile?.DiscordName,
                    Ingame: ingameTag?.GamerTag,
                    Plattform: ingameTag?.Platform,
                    Team: (standing.Team ?? string.Empty).Trim(),
                    TeamLogo: LogoUrlFuer(standing.Team),
                    Liga: liga.Id,
                    LigaName: liga.Name,
                    Position: standing.Position,
                    Nummer: standing.DriverNumber,
                    IstReserve: standing.IsReserveDriver,
                    ReserveFuer: standing.ReserveForDriver,
                    Saison: new StandingsExportSaison(
                        Rennen: rennen,
                        Siege: siege,
                        Podien: podien,
                        Punkte: standing.Points,
                        BestFinish: bestPos > 0 ? "P" + bestPos : null,
                        WinRate: rennen > 0 ? Math.Round(siege * 100.0 / rennen, 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%" : null)));
            }

            // Teamwertung: exakt dieselbe Berechnung wie die LeagueResults-Seite —
            // Team-Auflösung pro Finish (Reserve/Gast-Logik), leere Auflösung → „Ohne Team",
            // Punkte aus der F1Scoring-Map. ComputeTeamPointsForLeague entfällt bewusst:
            // sie überspringt „Ohne Team", die Seite zählt es mit.
            var teamNames = liga.Standings
                .Select(s => string.IsNullOrWhiteSpace(s.Team) ? "Ohne Team" : s.Team.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var teamZeilen = teamNames
                .Select(teamName =>
                {
                    var allFinishes = liga.Races
                        .SelectMany(r => r.Finishes.Select(f => new { Finish = f, Race = r }))
                        .Where(x => string.Equals(
                            RaceTeamHelper.ResolveTeamForRaceDriver(liga.Standings, x.Race, x.Finish.Driver) ?? "Ohne Team",
                            teamName, StringComparison.OrdinalIgnoreCase))
                        .Select(x => x.Finish)
                        .ToList();

                    var punkte = 0;
                    foreach (var f in allFinishes)
                    {
                        if (f.Position <= 0) continue;
                        var idx = f.Position - 1;
                        if (idx < _f1PointMap.Length) punkte += _f1PointMap[idx];
                    }

                    var p1 = allFinishes.Count(f => f.Position == 1);
                    var p2 = allFinishes.Count(f => f.Position == 2);
                    var p3 = allFinishes.Count(f => f.Position == 3);

                    return new
                    {
                        Team = teamName,
                        Punkte = punkte,
                        Siege = p1,
                        Podien = p1 + p2 + p3
                    };
                })
                .OrderByDescending(x => x.Punkte)
                .ThenByDescending(x => x.Siege)
                .ThenBy(x => x.Team)
                .ToList();

            var position = 1;
            foreach (var t in teamZeilen)
            {
                teams.Add(new StandingsExportTeam(liga.Id, t.Team, position++, t.Punkte, t.Siege, t.Podien));
            }
        }

        // Profile ohne Standing (Ex-Fahrer etc.) trotzdem ausliefern — die Overlay-
        // Namensauflösung (erc-names.js) matcht Ingame-Namen gegen ALLE Profile.
        var verbraucht = fahrer
            .Where(f => !string.IsNullOrEmpty(f.Id))
            .Select(f => f.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var p in profiles.Where(p => !verbraucht.Contains(p.DiscordId)).OrderBy(p => p.DiscordName, StringComparer.OrdinalIgnoreCase))
        {
            var ingameTag = p.GamerTags.FirstOrDefault(t => t.IsPrimary) ?? p.GamerTags.FirstOrDefault();
            fahrer.Add(new StandingsExportFahrer(
                Id: p.DiscordId,
                Url: $"{BasisUrl}/Profile/{p.DiscordId}",
                Name: p.DisplayName ?? p.DiscordName,
                Discord: p.DiscordName,
                Ingame: ingameTag?.GamerTag,
                Plattform: ingameTag?.Platform,
                Team: string.Empty,
                TeamLogo: null,
                Liga: string.Empty,
                LigaName: null,
                Position: 0,
                Nummer: null,
                IstReserve: false,
                ReserveFuer: null,
                Saison: new StandingsExportSaison(0, 0, 0, 0, null, null)));
        }

        return new StandingsExport(
            Geholt: DateTime.UtcNow,
            Quelle: BasisUrl + "/api/telemetry/standings",
            Ligen: ligaInfos,
            Fahrer: fahrer,
            Teams: teams);
    }

    /// <summary>Logo-URL eines Teams (F1TeamsHelper-Alias-Map, z. B. Sauber → audi.svg).</summary>
    private static string? LogoUrlFuer(string? teamName)
    {
        var team = F1TeamsHelper.GetTeamByName(teamName);
        return team is null ? null : $"{BasisUrl}/images/teams/{team.LogoKey}.svg";
    }
}