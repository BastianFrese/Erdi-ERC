using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erdi_ERC.Migrations
{
    /// <inheritdoc />
    public partial class AddRacePointsFactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PointsPercent",
                table: "RaceResults",
                type: "int",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<int>(
                name: "CompletedLaps",
                table: "PendingRaceResults",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalLaps",
                table: "PendingRaceResults",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ReservePointsForMain",
                table: "DriverStandings",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "PointsAdjustment",
                table: "DriverStandings",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "Points",
                table: "DriverStandings",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PointsPercent",
                table: "RaceResults");

            migrationBuilder.DropColumn(
                name: "CompletedLaps",
                table: "PendingRaceResults");

            migrationBuilder.DropColumn(
                name: "TotalLaps",
                table: "PendingRaceResults");

            migrationBuilder.AlterColumn<int>(
                name: "ReservePointsForMain",
                table: "DriverStandings",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,2)",
                oldPrecision: 9,
                oldScale: 2);

            migrationBuilder.AlterColumn<int>(
                name: "PointsAdjustment",
                table: "DriverStandings",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,2)",
                oldPrecision: 9,
                oldScale: 2,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<int>(
                name: "Points",
                table: "DriverStandings",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,2)",
                oldPrecision: 9,
                oldScale: 2);
        }
    }
}
