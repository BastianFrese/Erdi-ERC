using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erdi_ERC.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverNumberColorAndStandingNumberIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DriverNumberColor",
                table: "DriverProfiles",
                type: "varchar(7)",
                maxLength: 7,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_DriverStandings_LeagueId_DriverNumber",
                table: "DriverStandings",
                columns: new[] { "LeagueId", "DriverNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DriverStandings_LeagueId_DriverNumber",
                table: "DriverStandings");

            migrationBuilder.DropColumn(
                name: "DriverNumberColor",
                table: "DriverProfiles");
        }
    }
}
