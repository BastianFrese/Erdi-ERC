using ClosedXML.Excel;
using Erdi_ERC.Data;
using Erdi_ERC.Helpers;
using Erdi_ERC.Models;
using Erdi_ERC.Options;
using Erdi_ERC.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Erdi_ERC.Controllers
{
    /// <summary>Statistik-Seiten: Ewige Liste, Racing Hub (Erdi10), Hall of Fame, Fahrer-Level und -Karten.</summary>
    public class StatsController : Controller
    {
        private const string DriverCardsCacheKey = "stats:driver-cards:v1";
        private static readonly TimeSpan DriverCardsCacheTtl = TimeSpan.FromMinutes(5);
        private const string DriverLevelsCacheKey = "stats:driver-levels:v1";
        private static readonly TimeSpan DriverLevelsCacheTtl = TimeSpan.FromMinutes(5);

        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationOptions _appOptions;
        private readonly ILogger<StatsController> _logger;
        private readonly IMemoryCache _cache;
        private readonly int[] _f1PointMap;

        public StatsController(
            AppDbContext db,
            IWebHostEnvironment env,
            IOptions<ApplicationOptions> appOptions,
            IOptions<F1ScoringOptions> f1Scoring,
            ILogger<StatsController> logger,
            IMemoryCache cache)
        {
            _db = db;
            _env = env;
            _cache = cache;
            _appOptions = appOptions.Value;
            _logger = logger;
            var configuredMap = f1Scoring.Value.PointMap;
            _f1PointMap = configuredMap is { Length: > 0 }
                ? configuredMap
                : new[] { 25, 21, 18, 16, 14, 12, 10, 8, 7, 6, 5, 4, 3, 2, 1, 0, 0, 0, 0, 0 };
        }

        [Microsoft.AspNetCore.OutputCaching.OutputCache(PolicyName = "public-2min")]
        public async Task<IActionResult> Erdi10()
        {
            var leagues = await _db.Leagues
                .AsNoTracking()
                .AsSplitQuery()
                .Include(l => l.Standings)
                .Include(l => l.Races).ThenInclude(r => r.Finishes)
                .Include(l => l.Races).ThenInclude(r => r.ReserveAssignments)
                .Include(l => l.Races).ThenInclude(r => r.GuestAssignments)
                .OrderBy(l => l.Name)
                .ToListAsync();

            foreach (var l in leagues)
            {
                l.Standings = l.Standings.OrderBy(s => s.Position).ToList();
                l.Races = l.Races.OrderByDescending(r => r.Date).ToList();
            }

            var upcomingLegs = await _db.RaceWeekendLegs
                .AsNoTracking()
                .Include(l => l.Weekend)
                .Where(l => l.Date >= DateTime.Today)
                .OrderBy(l => l.Date)
                .ToListAsync();

            ViewBag.UpcomingLegsByLeague = upcomingLegs
                .GroupBy(l => l.LeagueId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return View(new Erdi10ViewModel
            {
                TwitchChannel = _appOptions.TwitchChannel,
                Leagues = leagues
            });
        }

        [HttpGet]
        [Microsoft.AspNetCore.OutputCaching.OutputCache(PolicyName = "public-2min")]
        public async Task<IActionResult> EwigeListe()
        {
            var vm = new EwigeListeViewModel();
            vm.Sheets.AddRange(await BuildLiveEwigeSheetsAsync());

            // Legacy-XLSX-Pfad: nur ein optionales Add-on zu den Live-Daten. Wenn die Datei
            // fehlt oder nicht parsbar ist (ClosedXML/OpenXML werfen bei korrupten/0-Byte-Files
            // z.B. ArgumentOutOfRangeException in GetPartById), brechen wir NICHT die ganze
            // Seite ab — Live-Daten reichen. OperationCanceledException (Request-Abbruch)
            // lassen wir bewusst durch, damit Middleware sauber aufräumt.
            var filePath = EwigeWorkbookHelper.GetPath(_env);
            if (!System.IO.File.Exists(filePath))
            {
                var fallbackPath = Path.Combine(_env.ContentRootPath, "ERC Ewige Tabelle.xlsx");
                if (System.IO.File.Exists(fallbackPath))
                {
                    filePath = fallbackPath;
                }
            }

            var legacyWorkbookBroken = false;
            var legacyWorkbookBytes = 0L;

            if (System.IO.File.Exists(filePath))
            {
                try
                {
                    using var workbook = new XLWorkbook(filePath);
                    foreach (var ws in workbook.Worksheets)
                    {
                        var usedRange = ws.RangeUsed();
                        var sheetVm = new EwigeListeSheetViewModel { Name = ws.Name };

                        if (usedRange != null)
                        {
                            var firstRow = usedRange.RangeAddress.FirstAddress.RowNumber;
                            var lastRow = usedRange.RangeAddress.LastAddress.RowNumber;
                            var firstCol = usedRange.RangeAddress.FirstAddress.ColumnNumber;
                            var lastCol = usedRange.RangeAddress.LastAddress.ColumnNumber;

                            for (int r = firstRow; r <= lastRow; r++)
                            {
                                var row = new List<string>();
                                for (int c = firstCol; c <= lastCol; c++)
                                {
                                    row.Add(ws.Cell(r, c).GetFormattedString());
                                }
                                sheetVm.Rows.Add(row);
                            }
                        }

                        vm.Sheets.Add(sheetVm);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Korruptes/leeres/0-Byte-Workbook darf die Seite nicht mehr killen —
                    // Live-Daten sind die primäre Quelle. Sichtbarer Admin-Hinweis im UI,
                    // damit ein wiederkehrender Upload-Bug nicht im stillen Self-Heal versauert.
                    legacyWorkbookBroken = true;
                    try { legacyWorkbookBytes = new FileInfo(filePath).Length; } catch { /* ignore */ }
                    _logger.LogWarning(ex,
                        "[EwigeListe] Überspringe kaputte Legacy-Workbook '{Path}' ({Bytes} Byte). Live-Daten reichen.",
                        filePath, legacyWorkbookBytes);
                }
            }

            try
            {
                AppendGlobalDriverOverviewSheets(vm.Sheets);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Global-Overview ist ein optionales Add-on; ein Throw hier ist mit hoher
                // Wahrscheinlichkeit ein Programmierfehler (rein statisch, kein I/O), also
                // laut loggen — aber die Seite nicht abreißen, wenn Live-Sheets vorhanden sind.
                _logger.LogWarning(ex, "[EwigeListe] Überspringe GlobalDriverOverview.");
            }

            if (vm.Sheets.Count == 0)
            {
                vm.ErrorMessage = "Es wurden weder Live-Daten noch eine Excel-Daten für die Ewige Liste gefunden.";
            }
            else if (legacyWorkbookBroken)
            {
                // Sichtbarer Admin-Hinweis: Live-Daten rendern, aber die kaputte Legacy-XLSX
                // braucht einen Re-Upload. Nicht-modal, kein 500 — einfach Info-Banner oben.
                vm.WarningMessage =
                    $"Die hochgeladene Excel-Datei ({legacyWorkbookBytes} Byte) konnte nicht gelesen werden "
                    + "und wurde übersprungen. Bitte im Admin-Bereich eine neue Datei hochladen — "
                    + "die Live-Daten sind vollständig verfügbar.";
            }

            return View(vm);
        }

        /// <summary>
        /// Öffentliche, screenshot-fähige Saison-Kalender-Übersicht über alle Ligen.
        /// Wird im Admin-Bereich konfiguriert (Hintergrundbild, Saison-Titel).
        /// </summary>
        [HttpGet("/fahrerkarten")]
        public async Task<IActionResult> DriverCards()
        {
            if (_cache.TryGetValue<List<(Models.League, List<(Models.DriverProfile, Models.DriverDetailViewModel)>)>>(DriverCardsCacheKey, out var cachedCards) && cachedCards is not null)
            {
                return View(cachedCards);
            }

            var result = await BuildDriverCardsAsync();

            _cache.Set(DriverCardsCacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = DriverCardsCacheTtl,
                Priority = CacheItemPriority.Low,
                Size = 1
            });

            return View(result);
        }

        private async Task<List<(Models.League League, List<(Models.DriverProfile Profile, Models.DriverDetailViewModel Card)> Drivers)>> BuildDriverCardsAsync()
        {
            var leagues = await _db.Leagues
                .AsNoTracking()
                .AsSplitQuery()
                .Where(l => !l.IsArchived)
                .Include(l => l.Standings)
                .Include(l => l.Races).ThenInclude(r => r.Finishes)
                .OrderBy(l => l.SortOrder).ThenBy(l => l.Name)
                .ToListAsync();

            var profiles = await _db.DriverProfiles
                .AsNoTracking()
                .Include(p => p.GamerTags)
                .ToListAsync();

            var tagToProfile = new Dictionary<string, Models.DriverProfile>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in profiles)
            {
                foreach (var tag in p.GamerTags)
                    if (!string.IsNullOrWhiteSpace(tag.GamerTag))
                        tagToProfile.TryAdd(tag.GamerTag.Trim(), p);
                if (!string.IsNullOrWhiteSpace(p.DisplayName))
                    tagToProfile.TryAdd(p.DisplayName.Trim(), p);
                tagToProfile.TryAdd(p.DiscordName.Trim(), p);
            }

            var result = new List<(Models.League League, List<(Models.DriverProfile Profile, Models.DriverDetailViewModel Card)> Drivers)>();

            foreach (var league in leagues)
            {
                var drivers = new List<(Models.DriverProfile, Models.DriverDetailViewModel)>();
                var seenInLeague = new HashSet<string>();

                foreach (var standing in league.Standings.OrderBy(s => s.Position))
                {
                    if (string.IsNullOrWhiteSpace(standing.Driver)) continue;
                    if (!tagToProfile.TryGetValue(standing.Driver.Trim(), out var profile)) continue;
                    if (!seenInLeague.Add(profile.DiscordId)) continue;

                    var aliases = DriverAliasHelper.Build(profile);

                    int wins = 0, podiums = 0, fastest = 0;
                    var races = new List<Models.DriverRaceEntry>();

                    foreach (var r in league.Races)
                    {
                        var finish = r.Finishes.FirstOrDefault(f => aliases.Contains(f.Driver?.Trim() ?? ""));
                        if (finish is null) continue;
                        if (finish.Position == 1) wins++;
                        if (finish.Position is >= 1 and <= 3) podiums++;
                        if (finish.FastestLap) fastest++;
                        races.Add(new Models.DriverRaceEntry
                        {
                            RaceId = r.RowId, LeagueId = league.Id, Date = r.Date,
                            Track = r.Track, Position = finish.Position, Points = 0,
                            FastestLap = finish.FastestLap, Team = standing.Team
                        });
                    }

                    drivers.Add((profile, new Models.DriverDetailViewModel
                    {
                        Driver       = profile.DisplayName ?? profile.DiscordName,
                        Team         = standing.Team,
                        DriverNumber = standing.DriverNumber,
                        TotalPoints  = standing.Points,
                        Wins = wins, Podiums = podiums, FastestLaps = fastest,
                        BestFinish   = races.Where(r => r.Position > 0).Select(r => (int?)r.Position).DefaultIfEmpty(null).Min(),
                        Races        = races
                    }));
                }

                if (drivers.Count > 0)
                    result.Add((league, drivers));
            }

            return result;
        }

        [HttpGet]
        [Microsoft.AspNetCore.OutputCaching.OutputCache(PolicyName = "public-2min")]
        public async Task<IActionResult> HallOfFame()
        {
            var standings = await _db.DriverStandings.ToListAsync();
            var races = await _db.RaceResults
                .AsSplitQuery()
                .Include(x => x.Finishes)
                .Include(x => x.ReserveAssignments)
                .Include(x => x.GuestAssignments)
                .ToListAsync();

            // Liga-Stammfahrer (Haupt + Reserve) pro League. Wird genutzt, um Ghost-Einträge
            // (Driver-String ohne Liga-Standing) aus der globalen Aggregation zu filtern.
            var driversByLeague = standings
                .Where(s => !string.IsNullOrWhiteSpace(s.Driver))
                .GroupBy(s => s.LeagueId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => new HashSet<string>(
                        g.Select(s => s.Driver!.Trim()),
                        StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);

            // Mappt einen RaceFinish.Driver auf den Liga-Hauptfahrer, falls es ein
            // Cross-League-Gast mit gültiger Zuordnung ist; sonst bleibt der Originalname.
            // Sentinel "(kein Hauptfahrer)" und nicht-auflösbare Gäste lösen unten den
            // Ghost-Filter aus und tauchen NICHT in der globalen Aggregation auf.
            static string ResolveHostDriver(
                HashSet<string>? leagueDrivers,
                RaceResult race,
                string finishDriver)
            {
                if (string.IsNullOrWhiteSpace(finishDriver)) return string.Empty;

                var trimmed = finishDriver.Trim();

                var guestMain = race.GuestAssignments?
                    .FirstOrDefault(g => !string.IsNullOrWhiteSpace(g.GuestDriver)
                        && g.GuestDriver.Trim().Equals(trimmed, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(g.MainDriver)
                        && g.MainDriver != StatsService.GuestSentinelNoMain)?
                    .MainDriver?.Trim();

                if (guestMain is not null && leagueDrivers is not null && leagueDrivers.Contains(guestMain))
                {
                    return guestMain;
                }

                return trimmed;
            }

            var reserveWins = races
                .SelectMany(r => r.Finishes.Where(f => f.Position == 1).Select(f => new { Finish = f, Race = r }))
                .Where(x => x.Race.ReserveAssignments.Any(a => string.Equals(a.ReserveDriver, x.Finish.Driver, StringComparison.OrdinalIgnoreCase)))
                .GroupBy(x => x.Finish.Driver, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { Driver = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .FirstOrDefault();

            // Podiums-König: gemeinsame LINQ-Kette für Driver + Count, damit die Logik
            // nicht viermal parallel läuft (Performance + Lesbarkeit).
            // Cross-League-Gäste werden auf ihren Liga-Hauptfahrer gemappt; Ghosts (kein
            // Liga-Standing) fliegen raus.
            var topPodium = races
                .SelectMany(r => r.Finishes.Select(f => new
                {
                    Driver = ResolveHostDriver(driversByLeague.GetValueOrDefault(r.LeagueId), r, f.Driver),
                    Position = f.Position
                }))
                .Where(f => f.Position is >= 1 and <= 3)
                .Where(f => !string.IsNullOrEmpty(f.Driver))
                .Where(f => driversByLeague.Values.Any(set => set.Contains(f.Driver)))
                .GroupBy(f => f.Driver, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { Driver = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Driver)
                .FirstOrDefault() ?? new { Driver = "-", Count = 0 };

            var vm = new HallOfFamePageViewModel
            {
                Records = new List<HallOfFameRecordViewModel>()
                {
                    new()
                    {
                        Title = "Meiste Siege",
                        Driver = standings.OrderByDescending(x => x.Wins).ThenBy(x => x.Driver).FirstOrDefault()?.Driver ?? "-",
                        Value = standings.OrderByDescending(x => x.Wins).FirstOrDefault()?.Wins.ToString() ?? "0",
                        Subtitle = "Über alle aktiven Standings hinweg"
                    },
                    new()
                    {
                        Title = "Meiste Punkte",
                        Driver = standings.OrderByDescending(x => x.Points).ThenBy(x => x.Driver).FirstOrDefault()?.Driver ?? "-",
                        Value = standings.OrderByDescending(x => x.Points).FirstOrDefault()?.Points.ToString() ?? "0",
                        Subtitle = "Gesamtausbeute in der ERC"
                    },
                    new()
                    {
                        Title = "Beste Reservefahrer",
                        Driver = reserveWins?.Driver ?? "-",
                        Value = reserveWins?.Count.ToString() ?? "0",
                        Subtitle = "Siege als Reservefahrer"
                    },
                    new()
                    {
                        Title = "Meiste Podien",
                        Driver = topPodium.Driver,
                        Value = topPodium.Count.ToString(),
                        Subtitle = "Podestplätze insgesamt"
                    }
                }
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> DriverLevels()
        {
            if (_cache.TryGetValue<List<DriverLevelEntryViewModel>>(DriverLevelsCacheKey, out var cachedLevels) && cachedLevels is not null)
            {
                return View(new DriverLevelsPageViewModel { Entries = cachedLevels });
            }

            var entries = await BuildDriverLevelsAsync();

            _cache.Set(DriverLevelsCacheKey, entries, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = DriverLevelsCacheTtl,
                Priority = CacheItemPriority.Low,
                Size = 1
            });

            return View(new DriverLevelsPageViewModel { Entries = entries });
        }

        private async Task<List<DriverLevelEntryViewModel>> BuildDriverLevelsAsync()
        {
            var profiles = await _db.DriverProfiles
                .Include(x => x.GamerTags)
                .ToListAsync();
            var races = await _db.RaceResults
                .Include(x => x.Finishes)
                .ToListAsync();
            var setupComments = await _db.SetupComments.ToListAsync();
            var setupLikes = await _db.SetupLikes.ToListAsync();
            var wallMessages = await _db.ProfileWallMessages.ToListAsync();

            var entries = profiles.Select(profile =>
            {
                // GamerTags + DisplayName + DiscordName — dieselbe Namensliste wie auf /fahrerkarten.
                // Ohne den DiscordName zählten Finishes, die unter dem Discord-Namen eingetragen
                // wurden, hier nicht mit (Prod-Befund 2026-09-17, gleiche Klasse wie das leere Profil).
                var names = DriverAliasHelper.Build(profile);

                var profileRaces = races.SelectMany(r => r.Finishes)
                    .Where(f => names.Contains(f.Driver.Trim()))
                    .ToList();
                var raceCount = profileRaces.Count;
                var wins = profileRaces.Count(x => x.Position == 1);
                var podiums = profileRaces.Count(x => x.Position is >= 1 and <= 3);
                var communityScore = setupComments.Count(x => x.AuthorDiscordId == profile.DiscordId)
                    + setupLikes.Count(x => x.DiscordId == profile.DiscordId)
                    + wallMessages.Count(x => x.AuthorDiscordId == profile.DiscordId);
                var xp = (raceCount * 20) + (wins * 25) + (podiums * 10) + (communityScore * 5);
                var level = Math.Max(1, (xp / 100) + 1);
                var nextLevelThreshold = level * 100;
                return new DriverLevelEntryViewModel
                {
                    Driver = profile.DisplayName ?? profile.DiscordName,
                    DiscordId = profile.DiscordId,
                    Level = level,
                    Xp = xp,
                    XpToNext = Math.Max(0, nextLevelThreshold - xp),
                    Races = raceCount,
                    Wins = wins,
                    Podiums = podiums,
                    CommunityScore = communityScore
                };
            })
            .OrderByDescending(x => x.Level)
            .ThenByDescending(x => x.Xp)
            .ThenBy(x => x.Driver)
            .ToList();

            return entries;
        }

        private sealed class DriverAggregate
        {
            public string DisplayName { get; set; } = string.Empty;
            public string LastTeam { get; set; } = string.Empty;
            /// <summary>Summe über alle Saisons. Dezimal, weil abgebrochene Rennen Bruchteile vergeben.</summary>
            public decimal TotalPoints { get; set; }
            public int Entries { get; set; }
            public HashSet<string> Seasons { get; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private static void AppendGlobalDriverOverviewSheets(List<EwigeListeSheetViewModel> sheets)
        {
            if (sheets.Count == 0)
            {
                return;
            }

            var aggregateByDriver = new Dictionary<string, DriverAggregate>(StringComparer.OrdinalIgnoreCase);

            foreach (var sheet in sheets)
            {
                if (sheet.Rows.Count == 0)
                {
                    continue;
                }

                var header = sheet.Rows[0];
                var driverCol = FindColumnIndex(header, "Driver", "Fahrer");
                var pointsCol = FindColumnIndex(header, "Points", "Punkte", "Gesamtpunkte");
                var teamCol = FindColumnIndex(header, "Team");

                if (driverCol < 0 || pointsCol < 0)
                {
                    continue;
                }

                for (int r = 1; r < sheet.Rows.Count; r++)
                {
                    var row = sheet.Rows[r];
                    if (driverCol >= row.Count)
                    {
                        continue;
                    }

                    var driverName = (row[driverCol] ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(driverName))
                    {
                        continue;
                    }

                    var points = pointsCol < row.Count ? ParseDecimalCell(row[pointsCol]) : 0m;
                    var team = teamCol >= 0 && teamCol < row.Count
                        ? (row[teamCol] ?? string.Empty).Trim()
                        : string.Empty;

                    if (!aggregateByDriver.TryGetValue(driverName, out var aggregate))
                    {
                        aggregate = new DriverAggregate { DisplayName = driverName };
                        aggregateByDriver[driverName] = aggregate;
                    }

                    aggregate.TotalPoints += points;
                    aggregate.Entries += 1;
                    aggregate.Seasons.Add(sheet.Name);

                    if (!string.IsNullOrWhiteSpace(team))
                    {
                        aggregate.LastTeam = team;
                    }
                }
            }

            if (aggregateByDriver.Count == 0)
            {
                return;
            }

            var values = aggregateByDriver.Values.ToList();

            var uniqueSheet = new EwigeListeSheetViewModel
            {
                Name = "Alle Fahrer (gesamt)",
                Rows = new List<List<string>>
                {
                    new() { "Fahrer", "Letztes Team", "Saisons", "Einträge" }
                }
            };

            foreach (var item in values.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                uniqueSheet.Rows.Add(new List<string>
                {
                    item.DisplayName,
                    item.LastTeam,
                    item.Seasons.Count.ToString(),
                    item.Entries.ToString()
                });
            }

            var pointsSheet = new EwigeListeSheetViewModel
            {
                Name = "Fahrer Gesamtpunkte (alle Seasons)",
                Rows = new List<List<string>>
                {
                    new() { "Position", "Fahrer", "Gesamtpunkte", "Saisons", "Letztes Team" }
                }
            };

            var rank = 1;
            foreach (var item in values
                .OrderByDescending(x => x.TotalPoints)
                .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                pointsSheet.Rows.Add(new List<string>
                {
                    rank.ToString(),
                    item.DisplayName,
                    PointsFormatHelper.Format(item.TotalPoints),
                    item.Seasons.Count.ToString(),
                    item.LastTeam
                });
                rank++;
            }

            sheets.RemoveAll(s => string.Equals(s.Name, uniqueSheet.Name, StringComparison.OrdinalIgnoreCase)
                               || string.Equals(s.Name, pointsSheet.Name, StringComparison.OrdinalIgnoreCase));

            sheets.Insert(0, pointsSheet);
            sheets.Insert(0, uniqueSheet);
        }

        private static int FindColumnIndex(List<string> header, params string[] names)
        {
            for (int i = 0; i < header.Count; i++)
            {
                var value = (header[i] ?? string.Empty).Trim();
                for (int n = 0; n < names.Length; n++)
                {
                    if (string.Equals(value, names[n], StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Liest einen Punkte-Wert aus einer Tabellenzelle. Delegiert an
        /// <see cref="PointsFormatHelper.Parse"/>, damit Zell-Parsing und Formular-Parsing
        /// dieselbe Regel haben („12,5" und „12.5" → 12,5).
        /// </summary>
        private static decimal ParseDecimalCell(string? value) => PointsFormatHelper.Parse(value);

        private async Task<List<EwigeListeSheetViewModel>> BuildLiveEwigeSheetsAsync()
        {
            var result = new List<EwigeListeSheetViewModel>();

            var leagues = await _db.Leagues
                .AsSplitQuery()
                .Include(l => l.Standings)
                .Include(l => l.Races).ThenInclude(r => r.Finishes)
                .Include(l => l.Races).ThenInclude(r => r.ReserveAssignments)
                .OrderBy(l => l.Name)
                .ToListAsync();

            foreach (var league in leagues)
            {
                var standings = league.Standings
                    .Where(s => !string.IsNullOrWhiteSpace(s.Driver))
                    .ToList();

                if (standings.Count == 0) continue;

                var leagueLabel = league.IsArchived
                    ? (string.IsNullOrWhiteSpace(league.ArchivedName) ? league.Name : league.ArchivedName!)
                    : $"{league.Name} (Aktuell)";

                var orderedRaces = league.Races
                    .OrderBy(r => r.Date)
                    .ThenBy(r => r.RowId)
                    .ToList();

                var raceColumns = orderedRaces
                    .Select(r => new
                    {
                        r.RowId,
                        Abbr = TrackAbbrForEwige(r.Track)
                    })
                    .ToList();

                var driverRows = standings
                    .Select(s =>
                    {
                        var driverName = s.Driver.Trim();
                        var reserveFor = s.IsReserveDriver && !string.IsNullOrWhiteSpace(s.ReserveForDriver)
                            ? s.ReserveForDriver.Trim()
                            : "";

                        var reserveForTeam = !string.IsNullOrWhiteSpace(reserveFor)
                            ? standings.FirstOrDefault(x =>
                                !string.IsNullOrWhiteSpace(x.Driver) &&
                                x.Driver.Trim().Equals(reserveFor, StringComparison.OrdinalIgnoreCase))?.Team
                            : null;

                        var displayTeam = s.IsReserveDriver
                            ? ""
                            : (s.Team ?? "").Trim();

                        var finishesByRace = orderedRaces
                            .Select(r => r.Finishes.FirstOrDefault(f =>
                                !string.IsNullOrWhiteSpace(f.Driver) &&
                                f.Driver.Trim().Equals(driverName, StringComparison.OrdinalIgnoreCase)))
                            .ToList();

                        var p1 = finishesByRace.Count(f => f?.Position == 1);
                        var p2 = finishesByRace.Count(f => f?.Position == 2);
                        var p3 = finishesByRace.Count(f => f?.Position == 3);

                        var raceValues = finishesByRace
                            .Select(f =>
                            {
                                if (f is null) return "DNS";
                                if (f.Position <= 0) return "DNF";
                                return f.Position.ToString();
                            })
                            .ToList();

                        return new
                        {
                            Driver = s.Driver,
                            Team = displayTeam,
                            s.Points,
                            Wins = s.Wins,
                            Podiums = p1 + p2 + p3,
                            RaceValues = raceValues,
                            ReserveFor = reserveFor
                        };
                    })
                    .OrderByDescending(x => x.Points)
                    .ThenByDescending(x => x.Wins)
                    .ThenBy(x => x.Driver)
                    .ToList();

                var driverHeader = new List<string> { "Driver", "Team", "Reserve For" };
                driverHeader.AddRange(raceColumns.Select(x => x.Abbr));
                driverHeader.AddRange(new[] { "Points", "Podiums", "Wins" });

                var driverSheet = new EwigeListeSheetViewModel
                {
                    Name = $"{leagueLabel} - Fahrer",
                    Rows = new List<List<string>> { driverHeader }
                };

                foreach (var row in driverRows)
                {
                    var cells = new List<string>
                    {
                        row.Driver,
                        row.Team,
                        row.ReserveFor
                    };

                    cells.AddRange(row.RaceValues);
                    cells.Add(PointsFormatHelper.Format(row.Points));
                    cells.Add(row.Podiums.ToString());
                    cells.Add(row.Wins.ToString());
                    driverSheet.Rows.Add(cells);
                }

                result.Add(driverSheet);

                // „Ohne Team" wird hier nicht geführt: der Finish-Filter unten verlangt ein
                // nicht-leeres Team, die Zeile hätte also nie Zahlen (siehe ConstructorTeamHelper).
                var teamNames = standings
                    .Select(s => ConstructorTeamHelper.LabelFor(s.Team))
                    .Where(t => !ConstructorTeamHelper.IsNoTeamBucket(t))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToList();

                var teamRows = teamNames
                    .Select(teamName =>
                    {
                        var raceValues = orderedRaces
                            .Select(r =>
                            {
                                var finishes = r.Finishes
                                    .Where(f =>
                                    {
                                        var resolvedTeam = RaceTeamHelper.ResolveTeamForRaceDriver(standings, r, f.Driver);
                                        return !string.IsNullOrWhiteSpace(resolvedTeam)
                                            && resolvedTeam.Equals(teamName, StringComparison.OrdinalIgnoreCase);
                                    })
                                    .ToList();

                                if (finishes.Count == 0) return "DNS";

                                var bestPos = finishes
                                    .Where(f => f.Position > 0)
                                    .Select(f => f.Position)
                                    .DefaultIfEmpty(0)
                                    .Min();

                                if (bestPos <= 0) return "DNF";
                                return bestPos.ToString();
                            })
                            .ToList();

                        var allFinishes = orderedRaces
                            .SelectMany(r => r.Finishes.Select(f => new { Finish = f, Race = r }))
                            .Where(x =>
                            {
                                var resolvedTeam = RaceTeamHelper.ResolveTeamForRaceDriver(standings, x.Race, x.Finish.Driver);
                                return !string.IsNullOrWhiteSpace(resolvedTeam)
                                    && resolvedTeam.Equals(teamName, StringComparison.OrdinalIgnoreCase);
                            })
                            .Select(x => x.Finish)
                            .ToList();

                        var p1 = allFinishes.Count(f => f.Position == 1);
                        var p2 = allFinishes.Count(f => f.Position == 2);
                        var p3 = allFinishes.Count(f => f.Position == 3);

                        return new
                        {
                            Team = teamName,
                            Points = RaceTeamHelper.ComputeTeamPointsForLeague(league, teamName, _f1PointMap) ?? 0,
                            Wins = p1,
                            Podiums = p1 + p2 + p3,
                            RaceValues = raceValues
                        };
                    })
                    .OrderByDescending(x => x.Points)
                    .ThenByDescending(x => x.Wins)
                    .ThenBy(x => x.Team)
                    .ToList();

                var teamHeader = new List<string> { "Team" };
                teamHeader.AddRange(raceColumns.Select(x => x.Abbr));
                teamHeader.AddRange(new[] { "Points", "Podiums", "Wins" });

                var teamSheet = new EwigeListeSheetViewModel
                {
                    Name = $"{leagueLabel} - Teams",
                    Rows = new List<List<string>> { teamHeader }
                };

                foreach (var row in teamRows)
                {
                    var cells = new List<string> { row.Team };
                    cells.AddRange(row.RaceValues);
                    cells.Add(PointsFormatHelper.Format(row.Points));
                    cells.Add(row.Podiums.ToString());
                    cells.Add(row.Wins.ToString());
                    teamSheet.Rows.Add(cells);
                }

                result.Add(teamSheet);
            }

            return result;
        }

        private static string TrackAbbrForEwige(string? track)
        {
            if (string.IsNullOrWhiteSpace(track)) return "RND";

            var t = track.ToLowerInvariant();
            if (t.Contains("australi")) return "AUS";
            if (t.Contains("imola") || t.Contains("emilia")) return "IMO";
            if (t.Contains("silverstone") || t.Contains("britain") || t.Contains("großbritannien")) return "GBR";
            if (t.Contains("spa") || t.Contains("belg")) return "SPA";
            if (t.Contains("hungary") || t.Contains("ungarn")) return "HUN";
            if (t.Contains("japan") || t.Contains("suzuka")) return "JAP";
            if (t.Contains("qatar") || t.Contains("katar") || t.Contains("losail")) return "QAT";
            if (t.Contains("saudi") || t.Contains("jeddah") || t.Contains("ksa")) return "SAU";
            if (t.Contains("mexico") || t.Contains("mexiko")) return "MEX";
            if (t.Contains("brazil") || t.Contains("brasili") || t.Contains("interlagos")) return "BRA";

            var letters = new string(track.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            return letters.Length >= 3 ? letters[..3] : letters.PadRight(3, 'X');
        }
    }
}
