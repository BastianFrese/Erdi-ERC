using Erdi_ERC.Models;
using Erdi_ERC.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Erdi_ERC.Data
{
    public static class DataSeeder
    {
        public static void Seed(AppDbContext db, IConfiguration config)
        {
            // Bootstrap-Admins aus appsettings.json
            var bootstrap = config.GetSection("BootstrapAdmins").Get<List<AdminUser>>() ?? new();
            foreach (var a in bootstrap)
            {
                if (string.IsNullOrWhiteSpace(a.DiscordId)) continue;

                var existing = db.AdminUsers.AsTracking().FirstOrDefault(x => x.DiscordId == a.DiscordId);
                if (existing is null)
                {
                    db.AdminUsers.Add(new AdminUser
                    {
                        DiscordId = a.DiscordId,
                        DisplayName = a.DisplayName,
                        AddedAt = DateTime.UtcNow
                    });
                }
                else if (!string.IsNullOrWhiteSpace(a.DisplayName) &&
                         existing.DisplayName != a.DisplayName)
                {
                    // DisplayName aus appsettings.json ist die Source-of-Truth – synchronisieren
                    existing.DisplayName = a.DisplayName;
                }
            }
            db.SaveChanges();

            SeedAchievementDefinitions(db);

            if (db.Leagues.Any()) return;

            var pro = new League
            {
                Id = "pro",
                Name = "ERC Pro League",
                Description = "Die Königsklasse der Erdi's Racing Community – F1 25, 100% Renndistanz, volle Sim-Einstellungen.",
                Standings = new()
                {
                    new() { LeagueId = "pro", Position = 1, Driver = "Erdi10",     Team = "Ferrari",       Points = 287, Wins = 8 },
                    new() { LeagueId = "pro", Position = 2, Driver = "SpeedyMax",  Team = "Red Bull",      Points = 254, Wins = 5 },
                    new() { LeagueId = "pro", Position = 3, Driver = "LewisFan44", Team = "Mercedes",      Points = 231, Wins = 3 },
                    new() { LeagueId = "pro", Position = 4, Driver = "ApexHunter", Team = "McLaren",       Points = 198, Wins = 2 },
                    new() { LeagueId = "pro", Position = 5, Driver = "DRSking",    Team = "Aston Martin",  Points = 176, Wins = 1 },
                },
                Races = new()
                {
                    new() { LeagueId = "pro", Date = DateTime.Today.AddDays(-7),  Track = "Spa-Francorchamps", Winner = "Erdi10",    FastestLap = "SpeedyMax" },
                    new() { LeagueId = "pro", Date = DateTime.Today.AddDays(-14), Track = "Monza",             Winner = "SpeedyMax", FastestLap = "Erdi10" },
                    new() { LeagueId = "pro", Date = DateTime.Today.AddDays(-21), Track = "Silverstone",       Winner = "Erdi10",    FastestLap = "LewisFan44" },
                }
            };

            var am = new League
            {
                Id = "am",
                Name = "ERC Amateur League",
                Description = "Einstiegsliga – 50% Renndistanz, faires Racing, perfekt zum Lernen.",
                Standings = new()
                {
                    new() { LeagueId = "am", Position = 1, Driver = "RookieRacer",  Team = "Williams", Points = 142, Wins = 4 },
                    new() { LeagueId = "am", Position = 2, Driver = "TurboTommy",   Team = "Alpine",   Points = 128, Wins = 3 },
                    new() { LeagueId = "am", Position = 3, Driver = "PitStopPete",  Team = "Haas",     Points = 110, Wins = 2 },
                    new() { LeagueId = "am", Position = 4, Driver = "BrakeLateBob", Team = "Sauber",   Points = 95,  Wins = 1 },
                },
                Races = new()
                {
                    new() { LeagueId = "am", Date = DateTime.Today.AddDays(-5),  Track = "Bahrain", Winner = "RookieRacer", FastestLap = "TurboTommy" },
                    new() { LeagueId = "am", Date = DateTime.Today.AddDays(-12), Track = "Imola",   Winner = "TurboTommy",  FastestLap = "RookieRacer" },
                }
            };

            var fun = new League
            {
                Id = "fun",
                Name = "ERC Fun League",
                Description = "Locker und mit Spaßfaktor – 25% Distanz, Reverse Grids und Wildcards.",
                Standings = new()
                {
                    new() { LeagueId = "fun", Position = 1, Driver = "ChaosKarl",    Team = "RB",     Points = 88, Wins = 3 },
                    new() { LeagueId = "fun", Position = 2, Driver = "DriftDani",    Team = "Alpine", Points = 72, Wins = 2 },
                    new() { LeagueId = "fun", Position = 3, Driver = "WildcardWill", Team = "Haas",   Points = 65, Wins = 2 },
                },
                Races = new()
                {
                    new() { LeagueId = "fun", Date = DateTime.Today.AddDays(-3),  Track = "Las Vegas", Winner = "ChaosKarl", FastestLap = "DriftDani" },
                    new() { LeagueId = "fun", Date = DateTime.Today.AddDays(-10), Track = "Miami",     Winner = "DriftDani", FastestLap = "ChaosKarl" },
                }
            };

            db.Leagues.AddRange(pro, am, fun);
            db.SaveChanges();

            SeedRaceWeekends(db);
            SeedRealLifeEvents(db);
        }

        private static void SeedRaceWeekends(AppDbContext db)
        {
            if (db.RaceWeekends.Any()) return;

            db.RaceWeekends.AddRange(
                new RaceWeekend
                {
                    Order = 1, Track = "Australien", DistancePercent = 100,
                    Legs = new()
                    {
                        new() { LeagueId = "pro", Date = DateTime.Today.AddDays(3).AddHours(21) },
                        new() { LeagueId = "am",  Date = DateTime.Today.AddDays(5).AddHours(21) },
                        new() { LeagueId = "fun", Date = DateTime.Today.AddDays(7).AddHours(20) },
                    }
                },
                new RaceWeekend
                {
                    Order = 2, Track = "Japan", DistancePercent = 50,
                    Legs = new()
                    {
                        new() { LeagueId = "pro", Date = DateTime.Today.AddDays(10).AddHours(21) },
                        new() { LeagueId = "am",  Date = DateTime.Today.AddDays(12).AddHours(21) },
                        new() { LeagueId = "fun", Date = DateTime.Today.AddDays(14).AddHours(20) },
                    }
                }
            );
            db.SaveChanges();
        }

        private static void SeedRealLifeEvents(AppDbContext db)
        {
            if (db.RealLifeEvents.Any()) return;

            var events = new List<RealLifeEvent>
            {
                new() {
                    Title = "KART-EVENT 1 · Kartbahn Werther",
                    Date = new DateTime(2021, 7, 1),
                    Location = "Kartbahn Werther",
                    IsUpcoming = false,
                    Description = "Hiermit endet das erste Twitch-Community-Kart-Event. Ich bedanke mich bei Fly2, MaikF1, Sairajjin, Grete, Phil, Beatmixer, Hctaw, Andiex, KevinGo, Sasuke, Inifinity, Leadx, Nikesfreundin, Kleriker, Ares, Leopard, Paranoid, Swarley & Towelie für das Erlebnis. Im Laufe der nächsten Tage wird dazu noch ein VLOG folgen. Falls ihr für\u2018s nächste mal mit dabei sein möchtet – im Discord wird\u2019s neue Infos geben!"
                },
                new() {
                    Title = "KART-EVENT 2 · Highway Kartracing Dortmund",
                    Date = new DateTime(2021, 8, 1),
                    Location = "Highway Kartracing, Dortmund",
                    IsUpcoming = false,
                    Description = "Hiermit endet das zweite Twitch-Community-Kart-Event. Ich bedanke mich bei meiner Community für das wiedermal geile Erlebnis. Für mich war nach einem katastrophalen Qualy nicht mehr als P4 drin. Gz an Fly, Metten und Towelie für die ersten 3 Plätze und die Pokale 🏆 Falls ihr für\u2018s nächste Mal mit dabei sein möchtet – im Discord wird\u2019s neue Infos geben! Habt ihr Vorschläge für eine mögliche neue Location?"
                },
                new() {
                    Title = "KART-EVENT 3 · Michael-Schumacher-Kartbahn Kerpen",
                    Date = new DateTime(2021, 9, 1),
                    Location = "Michael-Schumacher-Kartbahn, Kerpen",
                    IsUpcoming = false,
                    Description = "Community Treffen und DA IST DER SIEG – 1 Stunde pures Racing – was ein Kopf-an-Kopf-Rennen mit @sauerbratentwitch, wir waren beide ZEITGLEICH. Liebe & Kuss geht raus an alle. #ERC #kart #michaelschumacherkartbahn #kartbahn #twitch #f12021 #racing #airborne"
                },
                new() {
                    Title = "KART-EVENT 4 · Ralf-Schumacher-Kartbahn Bispingen",
                    Date = new DateTime(2022, 4, 1),
                    Location = "Ralf-Schumacher-Kartbahn, Bispingen",
                    IsUpcoming = false,
                    Description = "SOOOOO Erdi's Racing Community | ERC hat das mittlerweile 4. Deutschland-Kart-Event hinter sich. Nach Werther, Dortmund & Kerpen war diesmal die Ralf-Schumacher-Kartbahn in Bispingen dran. 14 Fahrer, 20 Zuschauer und es hat auf der langen Outdoor-Bahn wieder richtig Spaß gemacht zu racen!\n\nFür mich sprang P3 im Qualy, P2 im Rennen, P4 im Reverse Grid und INDOOR Platz 1 raus."
                },
                new() {
                    Title = "KART-EVENT 5 · Motorsportarena Oppenrod",
                    Date = new DateTime(2022, 6, 1),
                    Location = "Motorsportarena Oppenrod, Buseck",
                    IsUpcoming = false,
                    Description = "Das war das 5. nationale Kart-Event der ERC und hier habt ihr erste Eindrücke wie super geil das sowohl renntechnisch als auch menschlich abgelaufen ist.\n\nNach Werther, Dortmund, Kerpen, Bispingen waren wir diesmal im Süden auf der Motorsportarena Oppenrod.\n\nFür mich sprang P1 im Qualy raus. Wir starteten dann im Reverse Grid von hinten und mussten direkt am Anfang voll in die Eisen gehen und ja… dann kam der SPIN! Am Ende fehlten knapp 2,7 Sekunden zum Sieger. Das zweite Rennen wurde dann in normaler Reihenfolge gestartet und das war ein atemberaubender 3er-Kampf mit Dennis & Sören – am Ende haben wir uns den SIEG geschnappt 🙂"
                },
                new() {
                    Title = "KART-EVENT 6 · Michael-Schumacher-Kartbahn",
                    Date = new DateTime(2022, 8, 1),
                    Location = "Michael-Schumacher-Kartbahn, Kerpen",
                    IsUpcoming = false,
                    Description = "Das war das 6. nationale Kart-Event der ERC und wiedermal sind viele neue Leute am Start gewesen. Nach Werther, Dortmund, Kerpen, Bispingen, Oppenrod war für das letzte Mal in diesem Jahr nochmal die Michael-Schumacher-Kartbahn dran, die einen geilen Abschluss gefunden hat.\n\nExontic fuhr das Rennen mit einer kranken Pace von 53,4 verdient auf Platz 1. Mein Bruder Dennis mit dem absoluten Driver of the Day auf Platz 2 bei 53,6 – STARKER 2. Platz! Carl & ich lieferten uns einen spannenden Kopf-an-Kopf-Fight rundenlang, aber er mit 54,125 hauchdünn vor mir 54,159! Dahinter waren auch richtig schöne, geile Fights – alle waren auf einer Rennpace.\n\nDie Fotos hat die Liebe @lin.d.a.aa gemacht. Ihr findet alle anderen 500+ Fotos in der Dropbox im Discord."
                },
                new() {
                    Title = "LAN-EVENT 1 · eSports Factory Osnabrück",
                    Date = new DateTime(2022, 12, 1),
                    Location = "eSports Factory, Osnabrück",
                    IsUpcoming = false,
                    Description = "Das war das ERC-3-Tage-LAN-Event in der @esportfactory in Osnabrück. Damit endet das Jahr mit einem 6. (!) EVENT in 2022!\n\nDanke an jeden einzelnen, danke fürs Verwirklichen meines Traums/Hobbys. Die verbleibenden Goals werden in den nächsten Tagen natürlich noch ausgeschüttet.\n\nAuf in ein weiteres neues Jahr."
                },
                new() {
                    Title = "KART-EVENT 7 · Ralf-Schumacher-Kartcenter Bispingen",
                    Date = new DateTime(2023, 4, 1),
                    Location = "Ralf-Schumacher-Kartcenter, Bispingen",
                    IsUpcoming = false,
                    Description = "Erdi's Racing Community #ERC – Was ein wunderschöner Tag, das war das 7. Kart-Event mit einem rasanten Rennen wie man es von dieser geilen Strecke @ralf_schumacher_kartcenter gewohnt ist. Glückwunsch an @trim.pluss, der das DING in Max-Verstappen-Niveau sowas von easy geholt hat 🙂 Meinen zweiten Platz nehm ich aber auf jeden Fall hautnah vor Mercy ✌️"
                },
                new() {
                    Title = "KART-EVENT 8 · Motorsportarena Oppenrod",
                    Date = new DateTime(2024, 7, 20, 13, 0, 0),
                    Location = "Motorsportarena Oppenrod, Buseck (PLZ 35418)",
                    IsUpcoming = true,
                    Description = "Es ist wieder soweit – das 8. KART-EVENT findet statt in OPPENROD 2024.\n\nWANN? Samstag, den 20.7. ab 13:00 Uhr\nWO?   Motorsportarena Oppenrod in Buseck, PLZ 35418\n\nInfos: https://www.motorsportarena-oppenrod.de/faq"
                },
                new() {
                    Title = "KART-EVENT 9 · Michael-Schumacher Outdoor-Kartbahn Kerpen",
                    Date = new DateTime(DateTime.Today.Year + (DateTime.Today.Month > 5 ? 1 : 0), 5, 3, 18, 0, 0),
                    Location = "Michael-Schumacher Outdoor-Kartbahn, Kerpen",
                    IsUpcoming = true,
                    Description = "Das nächste (9.) nationale KART-EVENT findet wieder in NRW statt – geisteskrank mittlerweile SO VIELE, danke euch ❤️\n\nAuf der Michael-Schumacher Outdoor-Kartbahn in KERPEN.\nSamstag, den 3.5. ab 18:00 Uhr."
                },
            };

            db.RealLifeEvents.AddRange(events);
            db.SaveChanges();
        }

        private static void SeedAchievementDefinitions(AppDbContext db)
        {
            var defaults = DriverAchievementsHelper.GetDefaultDefinitions();
            var existingKeys = db.AchievementDefinitions.Select(x => x.Key).ToHashSet();
            var added = false;
            foreach (var d in defaults)
            {
                if (existingKeys.Contains(d.Key)) continue;
                db.AchievementDefinitions.Add(new AchievementDefinition
                {
                    Key = d.Key,
                    Title = d.Title,
                    Description = d.Description,
                    Icon = d.Icon,
                    Tone = d.Tone,
                    Tier = d.Tier,
                    Category = d.Category,
                    Metric = d.Metric,
                    Target = d.Target,
                    IsActive = d.IsActive,
                    IsBuiltIn = true,
                    SortOrder = d.SortOrder
                });
                added = true;
            }
            if (added) db.SaveChanges();
        }
    }
}
