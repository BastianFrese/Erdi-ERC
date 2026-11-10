using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Erdi_ERC.Controllers
{
    /// <summary>
    /// Öffentliche Season-Awards-Seite. Admins legen Awards an und schließen sie
    /// (<see cref="AdminCommunityController"/>); hier nominieren die User je Award
    /// und Liga einen Fahrer. Nominieren ist Abstimmen. Stimmenzahlen werden
    /// während des Votings bewusst nicht angezeigt — erst nach dem Schließen.
    /// </summary>
    public class AwardsController : Controller
    {
        private readonly AppDbContext _db;

        public AwardsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("/awards")]
        public async Task<IActionResult> Index()
        {
            var leagues = await _db.Leagues
                .OrderBy(l => l.SortOrder)
                .ThenBy(l => l.Name)
                .ToListAsync();

            var awardIds = await _db.SeasonAwards.Select(a => a.Id).ToListAsync();
            var openAwards = await _db.SeasonAwards
                .Where(a => !a.IsClosed)
                .OrderBy(a => a.Name)
                .ToListAsync();
            var closedAwards = await _db.SeasonAwards
                .Where(a => a.IsClosed)
                .OrderByDescending(a => a.ClosedAt)
                .ThenBy(a => a.Name)
                .ToListAsync();

            var nominations = await _db.SeasonAwardNominations
                .Where(n => awardIds.Contains(n.AwardId))
                .ToListAsync();

            var activeLeagues = leagues.Where(l => !l.IsArchived).ToList();
            var leagueIds = activeLeagues.Select(l => l.Id).ToList();
            var standings = await _db.DriverStandings
                .Where(s => leagueIds.Contains(s.LeagueId))
                .OrderBy(s => s.Position)
                .ThenBy(s => s.Driver)
                .ToListAsync();

            var myDiscordId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var vm = new SeasonAwardsPageViewModel
            {
                IsAuthenticated = User.Identity?.IsAuthenticated == true,
                LeagueNames = leagues.ToDictionary(l => l.Id, l => l.Name, StringComparer.Ordinal)
            };

            // Offene Awards: eine Karte je Liga, Kader aus den Standings (aktuelle Saison).
            if (openAwards.Count > 0)
            {
                foreach (var league in activeLeagues)
                {
                    var roster = standings
                        .Where(s => s.LeagueId == league.Id)
                        .Select(s => new SeasonAwardRosterEntry(s.Driver, s.Team, s.IsReserveDriver))
                        .ToList();
                    if (roster.Count == 0) continue;

                    vm.OpenLeagues.Add(new SeasonAwardLeagueViewModel
                    {
                        League = league,
                        Awards = openAwards.Select(award => new SeasonAwardVoteCardViewModel
                        {
                            Award = award,
                            Roster = roster,
                            MyDriver = string.IsNullOrWhiteSpace(myDiscordId)
                                ? null
                                : nominations.FirstOrDefault(n =>
                                      n.AwardId == award.Id
                                      && n.LeagueId == league.Id
                                      && n.DiscordId == myDiscordId)?.Driver
                        }).ToList()
                    });
                }
            }

            // Geschlossene Awards: Gewinner je Liga (Gleichstand = mehrere Gewinner).
            foreach (var award in closedAwards)
            {
                var votes = nominations.Where(n => n.AwardId == award.Id).ToList();
                vm.Results.Add(new SeasonAwardResultViewModel
                {
                    Award = award,
                    Winners = SeasonAwardTally.Winners(votes),
                    TotalVotes = votes.Count
                });
            }

            return View(vm);
        }

        /// <summary>Sprungmarke der Award-Karte (Award + Liga), damit der Nutzer nach dem
        /// Absenden an genau der Karte landet, an der er war.</summary>
        private string Anchor(int awardId, string? leagueId = null)
            => Url.Action(nameof(Index)) + "#award-" + awardId
               + (string.IsNullOrWhiteSpace(leagueId) ? string.Empty : "-" + leagueId);

        [Authorize]
        [HttpPost("/awards/nominate")]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("forms")]
        public async Task<IActionResult> Nominate(int awardId, string leagueId, string driver)
        {
            var discordId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(discordId)) return Forbid();

            var award = await _db.SeasonAwards.AsNoTracking().FirstOrDefaultAsync(a => a.Id == awardId);
            if (award is null)
            {
                TempData["AwardMessage"] = "Diesen Award gibt es nicht (mehr).";
                return RedirectToAction(nameof(Index));
            }

            if (award.IsClosed)
            {
                TempData["AwardMessage"] = $"„{award.Name}\" ist bereits geschlossen — die Gewinner stehen unten.";
                return Redirect(Anchor(awardId, leagueId));
            }

            var league = await _db.Leagues.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == leagueId);
            if (league is null || league.IsArchived)
            {
                TempData["AwardMessage"] = "Diese Liga nimmt nicht mehr an den Awards teil.";
                return Redirect(Anchor(awardId, leagueId));
            }

            var normalizedDriver = driver?.Trim() ?? string.Empty;
            if (normalizedDriver.Length == 0)
            {
                TempData["AwardMessage"] = "Bitte einen Fahrer auswählen.";
                return Redirect(Anchor(awardId, leagueId));
            }

            // Nur Fahrer aus dem Kader dieser Liga — der Wert kommt aus dem Select, aber
            // ein manipulierter POST darf keinen beliebigen Namen in die Auswertung schreiben.
            var inRoster = await _db.DriverStandings
                .AnyAsync(s => s.LeagueId == league.Id && s.Driver == normalizedDriver);
            if (!inRoster)
            {
                TempData["AwardMessage"] = "Dieser Fahrer steht nicht im Kader der Liga.";
                return Redirect(Anchor(awardId, leagueId));
            }

            var displayName = User.Identity?.Name ?? "Community User";

            // Umentscheiden überschreibt die eigene Stimme (AsTracking: der Context läuft
            // global mit NoTrackingWithIdentityResolution, siehe Program.cs).
            var existing = await _db.SeasonAwardNominations.AsTracking()
                .FirstOrDefaultAsync(n => n.AwardId == awardId
                                          && n.LeagueId == league.Id
                                          && n.DiscordId == discordId);
            if (existing is null)
            {
                _db.SeasonAwardNominations.Add(new SeasonAwardNomination
                {
                    AwardId = awardId,
                    LeagueId = league.Id,
                    Driver = normalizedDriver,
                    DiscordId = discordId,
                    DiscordName = displayName,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.Driver = normalizedDriver;
                existing.DiscordName = displayName;
                existing.CreatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            TempData["AwardMessage"] = $"Stimme für „{award.Name}\" gespeichert.";
            return Redirect(Anchor(awardId, leagueId));
        }
    }
}
