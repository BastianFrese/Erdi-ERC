using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erdi_ERC.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationAge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Age",
                table: "WaitlistEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Age",
                table: "Applications",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Age",
                table: "WaitlistEntries");

            migrationBuilder.DropColumn(
                name: "Age",
                table: "Applications");
        }
    }
}
