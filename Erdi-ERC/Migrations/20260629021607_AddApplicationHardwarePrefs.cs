using Microsoft.EntityFrameworkCore.Migrations;

***REMOVED***nullable disable

namespace <OWNER_HANDLE>_ERC.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationHardwarePrefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaceReference",
                table: "ApplicationForms",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "PreferredNumber",
                table: "ApplicationForms",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredTeam",
                table: "ApplicationForms",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SimHardware",
                table: "ApplicationForms",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaceReference",
                table: "ApplicationForms");

            migrationBuilder.DropColumn(
                name: "PreferredNumber",
                table: "ApplicationForms");

            migrationBuilder.DropColumn(
                name: "PreferredTeam",
                table: "ApplicationForms");

            migrationBuilder.DropColumn(
                name: "SimHardware",
                table: "ApplicationForms");
        }
    }
}
