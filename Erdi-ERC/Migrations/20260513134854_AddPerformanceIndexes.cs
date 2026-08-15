using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erdi_ERC.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UpcomingEvents_Date",
                table: "UpcomingEvents",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_UpcomingEvents_LeagueId_Date",
                table: "UpcomingEvents",
                columns: new[] { "LeagueId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_RaceResults_Date",
                table: "RaceResults",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_RaceResults_LeagueId_Date",
                table: "RaceResults",
                columns: new[] { "LeagueId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_RaceFinishes_Driver",
                table: "RaceFinishes",
                column: "Driver");

            migrationBuilder.CreateIndex(
                name: "IX_DriverStandings_Driver",
                table: "DriverStandings",
                column: "Driver");

            migrationBuilder.CreateIndex(
                name: "IX_DriverStandings_LeagueId_Driver",
                table: "DriverStandings",
                columns: new[] { "LeagueId", "Driver" });

            migrationBuilder.CreateIndex(
                name: "IX_DriverProfiles_DisplayName",
                table: "DriverProfiles",
                column: "DisplayName");

            migrationBuilder.DropIndex(
                name: "IX_UpcomingEvents_LeagueId",
                table: "UpcomingEvents");

            migrationBuilder.DropIndex(
                name: "IX_RaceResults_LeagueId",
                table: "RaceResults");

            migrationBuilder.DropIndex(
                name: "IX_DriverStandings_LeagueId",
                table: "DriverStandings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UpcomingEvents_Date",
                table: "UpcomingEvents");

            migrationBuilder.DropIndex(
                name: "IX_UpcomingEvents_LeagueId_Date",
                table: "UpcomingEvents");

            migrationBuilder.DropIndex(
                name: "IX_RaceResults_Date",
                table: "RaceResults");

            migrationBuilder.DropIndex(
                name: "IX_RaceResults_LeagueId_Date",
                table: "RaceResults");

            migrationBuilder.DropIndex(
                name: "IX_RaceFinishes_Driver",
                table: "RaceFinishes");

            migrationBuilder.DropIndex(
                name: "IX_DriverStandings_Driver",
                table: "DriverStandings");

            migrationBuilder.DropIndex(
                name: "IX_DriverStandings_LeagueId_Driver",
                table: "DriverStandings");

            migrationBuilder.DropIndex(
                name: "IX_DriverProfiles_DisplayName",
                table: "DriverProfiles");

            migrationBuilder.CreateIndex(
                name: "IX_UpcomingEvents_LeagueId",
                table: "UpcomingEvents",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_RaceResults_LeagueId",
                table: "RaceResults",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverStandings_LeagueId",
                table: "DriverStandings",
                column: "LeagueId");
        }
    }
}
