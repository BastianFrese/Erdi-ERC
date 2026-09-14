using Erdi_ERC.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erdi_ERC.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260815143225_AddApplicationSeason")]
    public partial class AddApplicationSeason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MySQL braucht einen Index auf LeagueId für die FK nach Leagues.
            // Wir legen die Erweiterung (LeagueId, Season, Position) VOR dem Drop des
            // alten Index an, damit MySQL den FK-Index nicht verliert.

            // 1. Neue Spalten + Backfill
            migrationBuilder.AddColumn<string>(
                name: "Season",
                table: "WaitlistEntries",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "ApplicationsOpenForNextSeason",
                table: "Leagues",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NextSeason",
                table: "Leagues",
                type: "varchar(16)",
                maxLength: 16,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Season",
                table: "Applications",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(@"
                UPDATE Applications a
                JOIN Leagues l ON l.Id = a.TargetLeagueId
                SET a.Season = COALESCE(NULLIF(l.CurrentSeason, ''), 'current')
                WHERE a.Season = '';
            ");

            migrationBuilder.Sql(@"
                UPDATE WaitlistEntries w
                JOIN Leagues l ON l.Id = w.LeagueId
                SET w.Season = COALESCE(NULLIF(l.CurrentSeason, ''), 'current')
                WHERE w.Season = '';
            ");

            // 2. Neue Indizes für Season-Verhalten
            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_LeagueId_Season_Position",
                table: "WaitlistEntries",
                columns: new[] { "LeagueId", "Season", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_DiscordId_LeagueId_Season",
                table: "WaitlistEntries",
                columns: new[] { "DiscordId", "LeagueId", "Season" });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_TargetLeagueId_Season_Status",
                table: "Applications",
                columns: new[] { "TargetLeagueId", "Season", "Status" });

            // 3. Alte Indizes droppen (MySQL nutzt jetzt den neuen LeagueId-Index als FK-Index).
            migrationBuilder.DropIndex(
                name: "IX_WaitlistEntries_LeagueId_Position",
                table: "WaitlistEntries");

            migrationBuilder.DropIndex(
                name: "IX_WaitlistEntries_DiscordId_LeagueId",
                table: "WaitlistEntries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WaitlistEntries_DiscordId_LeagueId_Season",
                table: "WaitlistEntries");

            migrationBuilder.DropIndex(
                name: "IX_WaitlistEntries_LeagueId_Season_Position",
                table: "WaitlistEntries");

            migrationBuilder.DropIndex(
                name: "IX_Applications_TargetLeagueId_Season_Status",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Season",
                table: "WaitlistEntries");

            migrationBuilder.DropColumn(
                name: "ApplicationsOpenForNextSeason",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "NextSeason",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "Season",
                table: "Applications");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_DiscordId_LeagueId",
                table: "WaitlistEntries",
                columns: new[] { "DiscordId", "LeagueId" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_LeagueId_Position",
                table: "WaitlistEntries",
                columns: new[] { "LeagueId", "Position" });
        }
    }
}
