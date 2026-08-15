using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erdi_ERC.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignedLeagueToApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedLeagueId",
                table: "ApplicationForms",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationForms_AssignedLeagueId",
                table: "ApplicationForms",
                column: "AssignedLeagueId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationForms_Leagues_AssignedLeagueId",
                table: "ApplicationForms",
                column: "AssignedLeagueId",
                principalTable: "Leagues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Backfill (best-effort) — ersetzt das frühere manuelle "Re-Sort"-Werkzeug:
            // 1) Fehlende AppliedLeagueId aus dem Division-Namen rekonstruieren (nur
            //    eindeutig auflösbare, nicht archivierte Ligen; MIN(Id) macht die Wahl
            //    bei Namens-Dubletten deterministisch).
            migrationBuilder.Sql(@"
UPDATE ApplicationForms a
JOIN (SELECT Name, MIN(Id) AS Id FROM Leagues WHERE IsArchived = 0 GROUP BY Name) l
    ON l.Name = a.Division
SET a.AppliedLeagueId = l.Id
WHERE a.AppliedLeagueId IS NULL;");

            // 2) Für angenommene Bewerbungen (Status=1) die tatsächlich zugewiesene Liga
            //    setzen: beim Annehmen wurde Division stets auf den Liga-Namen der
            //    Ziel-Liga gesetzt, der Name ist also die verlässlichste Quelle.
            //    Nicht Zuordenbares bleibt NULL und ist im Admin per Filter auffindbar.
            migrationBuilder.Sql(@"
UPDATE ApplicationForms a
JOIN (SELECT Name, MIN(Id) AS Id FROM Leagues WHERE IsArchived = 0 GROUP BY Name) l
    ON l.Name = a.Division
SET a.AssignedLeagueId = l.Id
WHERE a.Status = 1 AND a.AssignedLeagueId IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApplicationForms_Leagues_AssignedLeagueId",
                table: "ApplicationForms");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationForms_AssignedLeagueId",
                table: "ApplicationForms");

            migrationBuilder.DropColumn(
                name: "AssignedLeagueId",
                table: "ApplicationForms");
        }
    }
}
