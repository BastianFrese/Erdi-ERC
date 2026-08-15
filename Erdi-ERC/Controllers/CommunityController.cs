using Erdi_ERC.Data;
using Erdi_ERC.Helpers;
using Erdi_ERC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erdi_ERC.Controllers
{
    /// <summary>Community-Seiten: Events, News, Votes, Highlights, Teams, Reservefahrer-Börse.</summary>
    public class CommunityController : Controller
    {
        private readonly AppDbContext _db;

        public CommunityController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Events()
        {
            var events = await _db.RealLifeEvents
                .OrderByDescending(e => e.IsUpcoming)
                .ThenBy(e => e.IsUpcoming ? e.Date : DateTime.MaxValue)
                .ThenByDescending(e => e.Date)
                .ToListAsync();
            return View(events);
        }

        public async Task<IActionResult> EventDetail(int id)
        {
            var ev = await _db.RealLifeEvents
                .Include(e => e.Images.OrderBy(i => i.UploadedAt))
                .FirstOrDefaultAsync(e => e.Id == id);
            if (ev is null) return NotFound();
            return View(ev);
        }

        [HttpGet]
        public async Task<IActionResult> CommunityNews()
        {
            var vm = new CommunityNewsViewModel
            {
                Posts = await _db.CommunityNewsPosts
                    .Where(x => x.IsPublished)
                    .OrderByDescending(x => x.IsPinned)
                    .ThenByDescending(x => x.PublishedAt)
                    .ToListAsync()
            };
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Teams()
        {
            var standings = await _db.DriverStandings
                .Where(x => !string.IsNullOrWhiteSpace(x.Team))
                .ToListAsync();

            var races = await _db.RaceResults
                .Include(x => x.Finishes)
                .ToListAsync();

            var vm = new TeamListPageViewModel
            {
                Teams = standings
                    .GroupBy(x => x.Team.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g =>
                    {
                        var teamInfo = F1TeamsHelper.GetTeamByName(g.Key);
                        var drivers = g.Where(x => !string.IsNullOrWhiteSpace(x.Driver))
                            .Select(x => x.Driver.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
                        var podiums = races.Sum(r => r.Finishes.Count(f => f.Position is >= 1 and <= 3 && drivers.Contains(f.Driver.Trim())));
                        return new TeamSummaryViewModel
                        {
                            TeamName = g.Key,
                            CssKey = teamInfo?.CssKey,
                            PrimaryColor = teamInfo?.PrimaryColor ?? "#e10600",
                            SecondaryColor = teamInfo?.SecondaryColor ?? "#ffffff",
                            Drivers = drivers.Count,
                            Points = g.Sum(x => x.Points),
                            Wins = g.Sum(x => x.Wins),
                            Podiums = podiums
                        };
                    })
                    .OrderByDescending(x => x.Points)
                    .ThenBy(x => x.TeamName)
                    .ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Team(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return NotFound();
            var normalized = name.Trim();

            var standings = await _db.DriverStandings
                .Where(x => x.Team == normalized)
                .OrderByDescending(x => x.Points)
                .ThenByDescending(x => x.Wins)
                .ToListAsync();
            if (standings.Count == 0) return NotFound();

            var profiles = await _db.DriverProfiles
                .Include(x => x.GamerTags)
                .ToListAsync();
            var races = await _db.RaceResults
                .Include(x => x.Finishes)
                .Where(x => x.Finishes.Any())
                .OrderByDescending(x => x.Date)
                .Take(8)
                .ToListAsync();

            var teamInfo = F1TeamsHelper.GetTeamByName(normalized);
            var driverNames = standings.Select(x => x.Driver.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var vm = new TeamDetailPageViewModel
            {
                TeamName = normalized,
                CssKey = teamInfo?.CssKey,
                PrimaryColor = teamInfo?.PrimaryColor ?? "#e10600",
                SecondaryColor = teamInfo?.SecondaryColor ?? "#ffffff",
                TotalPoints = standings.Sum(x => x.Points),
                TotalWins = standings.Sum(x => x.Wins),
                TotalPodiums = races.Sum(r => r.Finishes.Count(f => f.Position is >= 1 and <= 3 && driverNames.Contains(f.Driver.Trim()))),
                Drivers = standings.Select(s => new TeamDriverCardViewModel
                {
                    Driver = s.Driver,
                    DiscordId = profiles.FirstOrDefault(p => string.Equals(p.DisplayName, s.Driver, StringComparison.OrdinalIgnoreCase)
                        || p.GamerTags.Any(t => string.Equals(t.GamerTag, s.Driver, StringComparison.OrdinalIgnoreCase)))?.DiscordId,
                    Points = s.Points,
                    Wins = s.Wins,
                    IsReserveDriver = s.IsReserveDriver,
                    ReserveForDriver = s.ReserveForDriver
                }).ToList(),
                RecentRaces = races.Select(r => new TeamRaceCardViewModel
                {
                    Date = r.Date,
                    LeagueId = r.LeagueId,
                    Track = r.Track,
                    Winner = r.Winner,
                    TeamFinishesInTop10 = r.Finishes.Count(f => f.Position is >= 1 and <= 10 && driverNames.Contains(f.Driver.Trim()))
                }).ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ReserveExchange(string? leagueId = null, string? platform = null, string? pace = null)
        {
            var profiles = await _db.DriverProfiles
                .Include(x => x.GamerTags)
                .ToListAsync();
            var standings = await _db.DriverStandings.ToListAsync();

            var entries = standings
                .Where(x => x.IsReserveDriver || !string.IsNullOrWhiteSpace(x.ReserveForDriver))
                .Select(s =>
                {
                    var profile = profiles.FirstOrDefault(p => string.Equals(p.DisplayName, s.Driver, StringComparison.OrdinalIgnoreCase)
                        || p.GamerTags.Any(t => string.Equals(t.GamerTag, s.Driver, StringComparison.OrdinalIgnoreCase)));
                    var profilePlatform = profile?.PreferredPlatform
                        ?? profile?.GamerTags.FirstOrDefault(t => t.IsPrimary)?.Platform
                        ?? profile?.GamerTags.FirstOrDefault()?.Platform
                        ?? "Unbekannt";
                    var paceBucket = s.Points >= 100 ? "Schnell" : s.Points >= 40 ? "Mittel" : "Entwicklung";
                    return new ReserveExchangeEntryViewModel
                    {
                        Driver = s.Driver,
                        LeagueId = s.LeagueId,
                        Team = s.Team,
                        ReserveForDriver = s.ReserveForDriver,
                        Platform = profilePlatform,
                        InputDevice = profile?.InputDevice ?? "Unbekannt",
                        FavoriteTrack = profile?.FavoriteTrack ?? "-",
                        Bio = profile?.Bio ?? string.Empty,
                        Points = s.Points,
                        Wins = s.Wins,
                        PaceBucket = paceBucket,
                        ProfileDiscordId = profile?.DiscordId
                    };
                })
                .ToList();

            if (!string.IsNullOrWhiteSpace(leagueId)) entries = entries.Where(x => x.LeagueId == leagueId).ToList();
            if (!string.IsNullOrWhiteSpace(platform)) entries = entries.Where(x => x.Platform == platform).ToList();
            if (!string.IsNullOrWhiteSpace(pace)) entries = entries.Where(x => x.PaceBucket == pace).ToList();

            var vm = new ReserveExchangePageViewModel
            {
                SelectedLeagueId = leagueId,
                SelectedPlatform = platform,
                SelectedPace = pace,
                LeagueIds = standings.Select(x => x.LeagueId).Distinct().OrderBy(x => x).ToList(),
                Platforms = profiles.Select(x => x.PreferredPlatform)
                    .Concat(profiles.SelectMany(x => x.GamerTags.Select(t => t.Platform)))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToList()!,
                Entries = entries.OrderByDescending(x => x.Points).ThenBy(x => x.Driver).ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> CommunityVotes()
        {
            var polls = await _db.CommunityVotePolls
                .Include(x => x.Options)
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
            var pollIds = polls.Select(x => x.Id).ToList();
            var responses = await _db.CommunityVoteResponses
                .Where(x => pollIds.Contains(x.PollId))
                .ToListAsync();
            var currentDiscordId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

            var vm = new CommunityVotesPageViewModel
            {
                Polls = polls.Select(p => new CommunityVotePollCardViewModel
                {
                    Poll = p,
                    VoteCounts = responses.Where(x => x.PollId == p.Id)
                        .GroupBy(x => x.OptionId)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    TotalVotes = responses.Count(x => x.PollId == p.Id),
                    CurrentOptionId = responses.FirstOrDefault(x => x.PollId == p.Id && x.DiscordId == currentDiscordId)?.OptionId
                }).ToList()
            };

            return View(vm);
        }

        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("forms")]
        public async Task<IActionResult> VoteCommunityPoll(int pollId, int optionId)
        {
            var discordId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var discordName = User.Identity?.Name ?? "Community User";
            if (string.IsNullOrWhiteSpace(discordId)) return Forbid();

            var existing = await _db.CommunityVoteResponses.AsTracking()
                .FirstOrDefaultAsync(x => x.PollId == pollId && x.DiscordId == discordId);
            if (existing is null)
            {
                _db.CommunityVoteResponses.Add(new CommunityVoteResponse
                {
                    PollId = pollId,
                    OptionId = optionId,
                    DiscordId = discordId,
                    DiscordName = discordName,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.OptionId = optionId;
                existing.DiscordName = discordName;
                existing.CreatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(CommunityVotes));
        }

        [HttpGet]
        public async Task<IActionResult> Highlights()
        {
            var vm = new HighlightsPageViewModel
            {
                Highlights = await _db.RaceHighlightClips
                    .Where(x => x.IsApproved)
                    .OrderByDescending(x => x.CreatedAt)
                    .ToListAsync()
            };
            return View(vm);
        }

        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("forms")]
        public async Task<IActionResult> SubmitHighlight(string title, string url, string? category, string? raceLabel)
        {
            var discordId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var discordName = User.Identity?.Name ?? "Community User";
            if (string.IsNullOrWhiteSpace(discordId)) return Forbid();

            var normalizedTitle = title?.Trim() ?? string.Empty;
            var normalizedUrl = url?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalizedTitle) && !string.IsNullOrWhiteSpace(normalizedUrl))
            {
                _db.RaceHighlightClips.Add(new RaceHighlightClip
                {
                    Title = normalizedTitle.Length > 160 ? normalizedTitle[..160] : normalizedTitle,
                    Url = normalizedUrl.Length > 512 ? normalizedUrl[..512] : normalizedUrl,
                    Category = string.IsNullOrWhiteSpace(category) ? "Highlight" : category.Trim()[..Math.Min(category.Trim().Length, 96)],
                    RaceLabel = string.IsNullOrWhiteSpace(raceLabel) ? null : raceLabel.Trim()[..Math.Min(raceLabel.Trim().Length, 160)],
                    SubmittedByDiscordId = discordId,
                    SubmittedByName = discordName,
                    IsApproved = true,
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Highlights));
        }
    }
}
