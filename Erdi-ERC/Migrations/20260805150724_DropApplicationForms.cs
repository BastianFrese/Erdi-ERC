using Microsoft.EntityFrameworkCore.Migrations;

***REMOVED***nullable disable

namespace <OWNER_HANDLE>_ERC.Migrations
{
    /// <summary>
    /// Bewerbungssystem komplett aus dem Datenbankschema entfernen.
    /// Voraussetzung: Das Bewerbungssystem wurde im Code abgebaut (Models, Services, Controller, Views).
    /// Diese Migration ist die DB-Seite des Greenfield-Cleanups.
    /// ACHTUNG: Alle Bewerbungsdatensaetze gehen verloren. Nur gegen erditest-DB anwenden, nicht gegen prod.
    /// </summary>
    public partial class DropApplicationForms : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Berechnete ActiveDiscordKey-Spalte + Unique-Index droppen
            migrationBuilder.DropIndex(
                name: "IX_ApplicationForms_ActiveDiscordKey",
                table: "ApplicationForms");

            migrationBuilder.DropColumn(
                name: "ActiveDiscordKey",
                table: "ApplicationForms");

            // 2. ApplicationForms-Tabelle droppen (alle FKs inklusive League-Referenzen werden mit gedroppt)
            migrationBuilder.DropTable(
                name: "ApplicationForms");

            // 3. Liga-Felder ApplicationInfo + IsOpenForApplications droppen
            migrationBuilder.DropColumn(
                name: "ApplicationInfo",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "IsOpenForApplications",
                table: "Leagues");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Liga-Felder wiederherstellen
            migrationBuilder.AddColumn<string>(
                name: "ApplicationInfo",
                table: "Leagues",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsOpenForApplications",
                table: "Leagues",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // ApplicationForms-Tabelle wiederherstellen
            migrationBuilder.CreateTable(
                name: "ApplicationForms",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DiscordId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DiscordName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GamingName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Platform = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Division = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Role = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReviewNote = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsFlagged = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    FlaggedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AppliedLeagueId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AssignedLeagueId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SimHardware = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PreferredNumber = table.Column<int>(type: "int", nullable: true),
                    PreferredTeam = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PaceReference = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsOnTrial = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    TrialEndsAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RowVersion = table.Column<DateTime>(type: "timestamp(6)", rowVersion: true, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationForms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationForms_Leagues_AppliedLeagueId",
                        column: x => x.AppliedLeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ApplicationForms_Leagues_AssignedLeagueId",
                        column: x => x.AssignedLeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationForms_SubmittedAt",
                table: "ApplicationForms",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationForms_Status",
                table: "ApplicationForms",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationForms_DiscordId",
                table: "ApplicationForms",
                column: "DiscordId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationForms_AppliedLeagueId",
                table: "ApplicationForms",
                column: "AppliedLeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationForms_AssignedLeagueId",
                table: "ApplicationForms",
                column: "AssignedLeagueId");

            migrationBuilder.AddColumn<string>(
                name: "ActiveDiscordKey",
                table: "ApplicationForms",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true,
                computedColumnSql: "(CASE WHEN `Status` <> 2 THEN `DiscordId` ELSE NULL END)",
                stored: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationForms_ActiveDiscordKey",
                table: "ApplicationForms",
                column: "ActiveDiscordKey",
                unique: true);
        }
    }
}
