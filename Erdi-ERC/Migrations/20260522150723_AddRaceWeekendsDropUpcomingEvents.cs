using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erdi_ERC.Migrations
{
    /// <inheritdoc />
    public partial class AddRaceWeekendsDropUpcomingEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UpcomingEvents");

            migrationBuilder.CreateTable(
                name: "RaceWeekends",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Track = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DistancePercent = table.Column<int>(type: "int", nullable: false, defaultValue: 100)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaceWeekends", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RaceWeekendLegs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RaceWeekendId = table.Column<int>(type: "int", nullable: false),
                    LeagueId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Date = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaceWeekendLegs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RaceWeekendLegs_RaceWeekends_RaceWeekendId",
                        column: x => x.RaceWeekendId,
                        principalTable: "RaceWeekends",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RaceWeekendLegs_Date",
                table: "RaceWeekendLegs",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_RaceWeekendLegs_LeagueId_Date",
                table: "RaceWeekendLegs",
                columns: new[] { "LeagueId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_RaceWeekendLegs_RaceWeekendId_LeagueId",
                table: "RaceWeekendLegs",
                columns: new[] { "RaceWeekendId", "LeagueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RaceWeekends_Order",
                table: "RaceWeekends",
                column: "Order");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RaceWeekendLegs");

            migrationBuilder.DropTable(
                name: "RaceWeekends");

            migrationBuilder.CreateTable(
                name: "UpcomingEvents",
                columns: table => new
                {
                    RowId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Format = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LeagueId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Track = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpcomingEvents", x => x.RowId);
                    table.ForeignKey(
                        name: "FK_UpcomingEvents_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_UpcomingEvents_Date",
                table: "UpcomingEvents",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_UpcomingEvents_LeagueId_Date",
                table: "UpcomingEvents",
                columns: new[] { "LeagueId", "Date" });
        }
    }
}
