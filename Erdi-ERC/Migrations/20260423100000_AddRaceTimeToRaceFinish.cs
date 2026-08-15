using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erdi_ERC.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(Data.AppDbContext))]
    [Migration("20260423100000_AddRaceTimeToRaceFinish")]
    public partial class AddRaceTimeToRaceFinish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RaceTimeMs",
                table: "RaceFinishes",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RaceTimeMs",
                table: "RaceFinishes");
        }
    }
}
