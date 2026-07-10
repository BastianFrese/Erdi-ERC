using Microsoft.EntityFrameworkCore.Migrations;

***REMOVED***nullable disable

namespace <OWNER_HANDLE>_ERC.Migrations
{
    /// <inheritdoc />
    public partial class AddLeagueDropWorstResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DropWorstResults",
                table: "Leagues",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DropWorstResults",
                table: "Leagues");
        }
    }
}
