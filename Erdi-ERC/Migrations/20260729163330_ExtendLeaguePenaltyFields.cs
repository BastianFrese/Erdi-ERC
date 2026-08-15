using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erdi_ERC.Migrations
{
    /// <inheritdoc />
    public partial class ExtendLeaguePenaltyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PenaltyType",
                table: "LeaguePenalties",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "DriverNumber",
                table: "LeaguePenalties",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBetweenTwoDrivers",
                table: "LeaguePenalties",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SecondDriverNumber",
                table: "LeaguePenalties",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DriverNumber",
                table: "LeaguePenalties");

            migrationBuilder.DropColumn(
                name: "IsBetweenTwoDrivers",
                table: "LeaguePenalties");

            migrationBuilder.DropColumn(
                name: "SecondDriverNumber",
                table: "LeaguePenalties");

            migrationBuilder.AlterColumn<string>(
                name: "PenaltyType",
                table: "LeaguePenalties",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(64)",
                oldMaxLength: 64)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
