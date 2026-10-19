using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erdi_ERC.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverOfDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DotdBallots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    VoteId = table.Column<int>(type: "int", nullable: false),
                    VoterKey = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Driver = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VotedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DotdBallots", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DotdVotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    LeagueId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LeagueName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    WinnerDriver = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WinnerVotes = table.Column<int>(type: "int", nullable: true),
                    TotalVotes = table.Column<int>(type: "int", nullable: true),
                    FinalSharesJson = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartedByDiscordId = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DotdVotes", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_DotdBallots_VoteId_VoterKey",
                table: "DotdBallots",
                columns: new[] { "VoteId", "VoterKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DotdVotes_ClosedAt",
                table: "DotdVotes",
                column: "ClosedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DotdBallots");

            migrationBuilder.DropTable(
                name: "DotdVotes");
        }
    }
}
