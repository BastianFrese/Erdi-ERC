using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erdi_ERC.Migrations
{
    /// <inheritdoc />
    public partial class AddReserveDriverSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReserveDriver",
                table: "DriverStandings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReserveForDriver",
                table: "DriverStandings",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ReservePointsForMain",
                table: "DriverStandings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReserveStarts",
                table: "DriverStandings",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReserveDriver",
                table: "DriverStandings");

            migrationBuilder.DropColumn(
                name: "ReserveForDriver",
                table: "DriverStandings");

            migrationBuilder.DropColumn(
                name: "ReservePointsForMain",
                table: "DriverStandings");

            migrationBuilder.DropColumn(
                name: "ReserveStarts",
                table: "DriverStandings");
        }
    }
}
