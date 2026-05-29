using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

***REMOVED***nullable disable

namespace <OWNER_HANDLE>_ERC.Migrations
{
    /// <inheritdoc />
    public partial class AddRealLifeEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RealLifeEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Title = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Location = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ImageFileName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsUpcoming = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RealLifeEvents", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // ── Seed: alle Real-Life-Events aus der Community-Geschichte ──────────────
            migrationBuilder.InsertData(
                table: "RealLifeEvents",
                columns: new[] { "Title", "Date", "Location", "Description", "ImageFileName", "IsUpcoming" },
                values: new object[,]
                {
                    {
                        "KART-EVENT 1 · Kartbahn Werther",
                        new DateTime(2021, 7, 1),
                        "Kartbahn Werther",
                        "Hiermit endet das erste Twitch-Community-Kart-Event. Ich bedanke mich bei Fly2, MaikF1, Sairajjin, Grete, Phil, Beatmixer, Hctaw, Andiex, KevinGo, Sasuke, Inifinity, Leadx, Nikesfreundin, Kleriker, Ares, Leopard, Paranoid, Swarley & Towelie für das Erlebnis. Im Laufe der nächsten Tage wird dazu noch ein VLOG folgen. Falls ihr für\u2019s nächste Mal mit dabei sein möchtet – im Discord wird\u2019s neue Infos geben!",
                        null,
                        false
                    },
                    {
                        "KART-EVENT 2 · Highway Kartracing Dortmund",
                        new DateTime(2021, 8, 1),
                        "Highway Kartracing, Dortmund",
                        "Hiermit endet das zweite Twitch-Community-Kart-Event. Ich bedanke mich bei meiner Community für das wiedermal geile Erlebnis. Für mich war nach einem katastrophalen Qualy nicht mehr als P4 drin. Gz an Fly, Metten und Towelie für die ersten 3 Plätze und die Pokale 🏆\nFalls ihr für\u2019s nächste Mal mit dabei sein möchtet – im Discord wird\u2019s neue Infos geben! Habt ihr Vorschläge für eine mögliche neue Location?",
                        null,
                        false
                    },
                    {
                        "KART-EVENT 3 · Michael-Schumacher-Kartbahn Kerpen",
                        new DateTime(2021, 9, 1),
                        "Michael-Schumacher-Kartbahn, Kerpen",
                        "Community Treffen und DA IST DER SIEG – 1 Stunde pures Racing – was ein Kopf-an-Kopf-Rennen mit @sauerbratentwitch, wir waren beide ZEITGLEICH. Liebe & Kuss geht raus an alle. ***REMOVED***ERC ***REMOVED***kart ***REMOVED***michaelschumacherkartbahn ***REMOVED***kartbahn ***REMOVED***twitch ***REMOVED***f12021 ***REMOVED***racing ***REMOVED***airborne",
                        null,
                        false
                    },
                    {
                        "KART-EVENT 4 · Ralf-Schumacher-Kartbahn Bispingen",
                        new DateTime(2022, 4, 1),
                        "Ralf-Schumacher-Kartbahn, Bispingen",
                        "SOOOOO <OWNER_HANDLE>\u2019s Racing Community | ERC hat das mittlerweile 4. Deutschland-Kart-Event hinter sich. Nach Werther, Dortmund & Kerpen war diesmal die Ralf-Schumacher-Kartbahn in Bispingen dran. 14 Fahrer, 20 Zuschauer und es hat auf der langen Outdoor-Bahn wieder richtig Spaß gemacht zu racen!\n\nFür mich sprang P3 im Qualy, P2 im Rennen, P4 im Reverse Grid und INDOOR Platz 1 raus.",
                        null,
                        false
                    },
                    {
                        "KART-EVENT 5 · Motorsportarena Oppenrod",
                        new DateTime(2022, 6, 1),
                        "Motorsportarena Oppenrod, Buseck",
                        "Das war das 5. nationale Kart-Event der ERC. Nach Werther, Dortmund, Kerpen, Bispingen waren wir diesmal im Süden auf der Motorsportarena Oppenrod.\n\nFür mich sprang P1 im Qualy raus. Wir starteten dann im Reverse Grid von hinten und mussten direkt am Anfang voll in die Eisen gehen – dann kam der SPIN! Am Ende fehlten knapp 2,7 Sekunden zum Sieger. Das zweite Rennen war ein atemberaubender 3er-Kampf mit Dennis & Sören – am Ende haben wir uns den SIEG geschnappt 🙂",
                        null,
                        false
                    },
                    {
                        "KART-EVENT 6 · Michael-Schumacher-Kartbahn Kerpen",
                        new DateTime(2022, 8, 1),
                        "Michael-Schumacher-Kartbahn, Kerpen",
                        "Das war das 6. nationale Kart-Event der ERC. Exontic fuhr das Rennen mit einer kranken Pace von 53,4 verdient auf Platz 1. Mein Bruder Dennis mit dem absoluten Driver of the Day auf Platz 2 bei 53,6 – STARKER 2. Platz! Carl & ich lieferten uns einen spannenden Kopf-an-Kopf-Fight rundenlang, aber er mit 54,125 hauchdünn vor mir 54,159!\n\nDie Fotos hat die Liebe @lin.d.a.aa gemacht. Ihr findet alle anderen 500+ Fotos in der Dropbox im Discord.",
                        null,
                        false
                    },
                    {
                        "LAN-EVENT 1 · eSports Factory Osnabrück",
                        new DateTime(2022, 12, 1),
                        "eSports Factory, Osnabrück",
                        "Das war das ERC-3-Tage-LAN-Event in der eSports Factory in Osnabrück. Damit endet das Jahr mit einem 6. (!) EVENT in 2022!\n\nDanke an jeden einzelnen, danke fürs Verwirklichen meines Traums/Hobbys. Die verbleibenden Goals werden in den nächsten Tagen natürlich noch ausgeschüttet.\n\nAuf in ein weiteres neues Jahr.",
                        null,
                        false
                    },
                    {
                        "KART-EVENT 7 · Ralf-Schumacher-Kartcenter Bispingen",
                        new DateTime(2023, 4, 1),
                        "Ralf-Schumacher-Kartcenter, Bispingen",
                        "<OWNER_HANDLE>\u2019s Racing Community ***REMOVED***ERC – Was ein wunderschöner Tag, das war das 7. Kart-Event mit einem rasanten Rennen wie man es von dieser geilen Strecke gewohnt ist. Glückwunsch an @trim.pluss, der das DING in Max-Verstappen-Niveau sowas von easy geholt hat 🙂 Meinen zweiten Platz nehm ich aber auf jeden Fall hautnah vor Mercy ✌️",
                        null,
                        false
                    },
                    {
                        "KART-EVENT 8 · Motorsportarena Oppenrod",
                        new DateTime(2024, 7, 20, 13, 0, 0),
                        "Motorsportarena Oppenrod, Buseck (PLZ 35418)",
                        "Es ist wieder soweit – das 8. KART-EVENT findet statt in OPPENROD 2024.\n\nWANN?  Samstag, den 20.7. ab 13:00 Uhr\nWO?    Motorsportarena Oppenrod in Buseck, PLZ 35418\n\nhttps://www.motorsportarena-oppenrod.de/faq",
                        null,
                        false
                    },
                    {
                        "KART-EVENT 9 · Michael-Schumacher Outdoor-Kartbahn Kerpen",
                        new DateTime(2025, 5, 3, 18, 0, 0),
                        "Michael-Schumacher Outdoor-Kartbahn, Kerpen",
                        "Das nächste (9.) nationale KART-EVENT findet wieder in NRW statt – geisteskrank mittlerweile SO VIELE, danke euch ❤️\n\nAuf der Michael-Schumacher Outdoor-Kartbahn in KERPEN.\nSamstag, den 3.5. ab 18:00 Uhr.",
                        null,
                        true
                    }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "RealLifeEvents");
        }
    }
}
