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

        /// <summary>
        /// Sammel-Abgabe: die Seite sendet drei gleich lange Listen (awardId, leagueId,
        /// driver) — ein Tripel je Award-Karte. Leere Selects werden übersprungen, damit
        /// Teilabstimmen möglich ist und eine offene Karte ihre alte Stimme nicht löscht.
        /// Jede Zeile wird einzeln geprüft; ungültige Zeilen fallen raus, gültige werden
        /// gespeichert. Ein einzelnes Feldpaar bindet ebenfalls hierher (Kompatibilität).
        /// </summary>
        [Authorize]
        [HttpPost("/awards/nominate")]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("forms")]
        public async Task<IActionResult> Nominate(List<int>? awardId, List<string>? leagueId, List<string>? driver)
        {
            var discordId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(discordId)) return Forbid();

            var sentAwardIds = awardId ?? new List<int>();
            var sentLeagueIds = leagueId ?? new List<string>();
            var sentDrivers = driver ?? new List<string>();

            var rows = new List<AwardVoteRow>();
            var count = Math.Min(sentAwardIds.Count, Math.Min(sentLeagueIds.Count, sentDrivers.Count));
            for (var i = 0; i < count; i++)
            {
                // Unverändert lassen = kein Fahrer gewählt.
                var tidyDriver = sentDrivers[i]?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(tidyDriver)) continue;
                rows.Add(new AwardVoteRow(sentAwardIds[i], sentLeagueIds[i]?.Trim() ?? string.Empty, tidyDriver));
            }

            if (rows.Count == 0)
            {
                TempData["AwardMessage"] = "Keine Auswahl getroffen — es wurde nichts gespeichert.";
                return RedirectToAction(nameof(Index));
            }

            // Alles, was die Prüfungen brauchen, in je einer Abfrage.
            var wantedAwardIds = rows.Select(r => r.AwardId).Distinct().ToList();
            var wantedLeagueIds = rows.Select(r => r.LeagueId).Distinct(StringComparer.Ordinal).ToList();

            var awards = await _db.SeasonAwards.AsNoTracking()
                .Where(a => wantedAwardIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id);
            var leagues = await _db.Leagues.AsNoTracking()
                .Where(l => wantedLeagueIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, StringComparer.Ordinal);
            var roster = (await _db.DriverStandings.AsNoTracking()
                    .Where(s => wantedLeagueIds.Contains(s.LeagueId))
                    .Select(s => new { s.LeagueId, s.Driver })
                    .ToListAsync())
                .Select(s => (s.LeagueId, s.Driver))
                .ToHashSet();

            // AsTracking: der Context läuft global mit NoTrackingWithIdentityResolution (Program.cs).
            var mine = await _db.SeasonAwardNominations.AsTracking()
                .Where(n => n.DiscordId == discordId
                            && wantedAwardIds.Contains(n.AwardId)
                            && wantedLeagueIds.Contains(n.LeagueId))
                .ToListAsync();

            var displayName = User.Identity?.Name ?? "Community User";
            var saved = 0;
            var skipped = 0;
            string? skipReason = null;

            void Skip(string reason)
            {
                skipped++;
                skipReason ??= reason;
            }

            foreach (var row in rows)
            {
                if (!awards.TryGetValue(row.AwardId, out var award)) { Skip("Award nicht gefunden"); continue; }
                if (award.IsClosed) { Skip("Award bereits geschlossen"); continue; }
                if (!leagues.TryGetValue(row.LeagueId, out var league) || league.IsArchived)
                { Skip("Liga nimmt nicht mehr teil"); continue; }

                // Nur Fahrer aus dem Kader dieser Liga — der Wert kommt aus dem Select, aber
                // ein manipulierter POST darf keinen beliebigen Namen in die Auswertung schreiben.
                if (row.Driver.Length > 128 || !roster.Contains((league.Id, row.Driver)))
                { Skip("Fahrer nicht im Kader"); continue; }

                // Umentscheiden überschreibt die eigene Stimme.
                var existing = mine.FirstOrDefault(n => n.AwardId == award.Id && n.LeagueId == league.Id);
                if (existing is null)
                {
                    existing = new SeasonAwardNomination
                    {
                        AwardId = award.Id,
                        LeagueId = league.Id,
                        DiscordId = discordId
                    };
                    _db.SeasonAwardNominations.Add(existing);
                    mine.Add(existing);
                }

                existing.Driver = row.Driver;
                existing.DiscordName = displayName;
                existing.CreatedAt = DateTime.UtcNow;
                saved++;
            }

            if (saved > 0) await _db.SaveChangesAsync();

            var message = saved == 0
                ? "Nichts gespeichert — keine der gewählten Kombinationen war gültig."
                : $"{saved} {(saved == 1 ? "Stimme" : "Stimmen")} gespeichert.";
            if (skipped > 0)
                message += $" {skipped} nicht übernommen ({skipReason}).";

            TempData["AwardMessage"] = message;
            return RedirectToAction(nameof(Index));
        }

        /// <summary>Eine abgeschickte Award-Zeile (Award + Liga + Fahrer).</summary>
        private readonly record struct AwardVoteRow(int AwardId, string LeagueId, string Driver);
    }
}
