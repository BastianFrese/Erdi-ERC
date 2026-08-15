using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erdi_ERC.Migrations
{
    /// <inheritdoc />
    public partial class AddRaceGuestAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RaceGuestAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RaceResultId = table.Column<int>(type: "int", nullable: false),
                    GuestDriver = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MainDriver = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaceGuestAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RaceGuestAssignments_RaceResults_RaceResultId",
                        column: x => x.RaceResultId,
                        principalTable: "RaceResults",
                        principalColumn: "RowId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RaceGuestAssignments_RaceResultId_GuestDriver",
                table: "RaceGuestAssignments",
                columns: new[] { "RaceResultId", "GuestDriver" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RaceGuestAssignments_RaceResultId_MainDriver",
                table: "RaceGuestAssignments",
                columns: new[] { "RaceResultId", "MainDriver" });

            // Backfill: vorhandene Finishes, deren Driver weder in Liga-Standings
            // noch als Reserve-Assignment existiert, werden als Gast mit
            // Sentinel "(kein Hauptfahrer)" markiert. Diese zählen weder in
            // Standings noch in Team-Punkten.
            migrationBuilder.Sql(@"
                INSERT INTO RaceGuestAssignments (RaceResultId, GuestDriver, MainDriver)
                SELECT rf.RaceResultId, rf.Driver, '(kein Hauptfahrer)'
                FROM RaceFinishes rf
                INNER JOIN RaceResults rr ON rr.RowId = rf.RaceResultId
                LEFT JOIN DriverStandings ds
                    ON ds.LeagueId = rr.LeagueId AND ds.Driver = rf.Driver
                LEFT JOIN RaceReserveAssignments ra
                    ON ra.RaceResultId = rf.RaceResultId AND ra.ReserveDriver = rf.Driver
                WHERE ds.LeagueId IS NULL
                  AND ra.Id IS NULL
                  AND rf.Driver IS NOT NULL
                  AND rf.Driver <> '';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RaceGuestAssignments");
        }
    }
}
