using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erdi_ERC.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationFlagAndDedupGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FlaggedAt",
                table: "ApplicationForms",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFlagged",
                table: "ApplicationForms",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // Bestehende Doppelbewerbungen bereinigen, BEVOR der Unique-Index angelegt wird —
            // sonst scheitert die Index-Erstellung an vorhandenen Duplikaten. Pro DiscordId bleibt
            // die "beste" aktive Bewerbung (zuerst akzeptierte, sonst neueste) erhalten; alle
            // weiteren aktiven Bewerbungen werden als abgelehnt markiert (Daten bleiben erhalten).
            migrationBuilder.Sql(@"
                UPDATE `ApplicationForms` AS af
                JOIN (
                    SELECT `Id`,
                           ROW_NUMBER() OVER (
                               PARTITION BY `DiscordId`
                               ORDER BY `IsAccepted` DESC, `SubmittedAt` DESC, `Id` DESC
                           ) AS rn
                    FROM `ApplicationForms`
                    WHERE `IsRejected` = 0 AND `DiscordId` IS NOT NULL AND `DiscordId` <> ''
                ) AS ranked ON af.`Id` = ranked.`Id`
                SET af.`IsRejected` = 1,
                    af.`RejectedAt` = UTC_TIMESTAMP(),
                    af.`ReviewNote` = CONCAT(COALESCE(af.`ReviewNote`, ''), '\n[Auto] Duplikat automatisch abgelehnt (Dedup-Migration).')
                WHERE ranked.rn > 1;
            ");

            // Workaround für Pomelo 9.0.0 / MariaDB: EF generiert für nullable generated
            // columns ungültiges SQL (... AS ...) NULL). Hier manuell als VIRTUAL-Spalte.
            migrationBuilder.Sql(@"
                ALTER TABLE `ApplicationForms`
                ADD COLUMN `ActiveDiscordKey` varchar(32) CHARACTER SET utf8mb4
                AS (CASE WHEN `IsRejected` = 0 THEN `DiscordId` ELSE NULL END) VIRTUAL;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationForms_ActiveDiscordKey",
                table: "ApplicationForms",
                column: "ActiveDiscordKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationForms_DiscordId",
                table: "ApplicationForms",
                column: "DiscordId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationForms_IsAccepted_IsRejected",
                table: "ApplicationForms",
                columns: new[] { "IsAccepted", "IsRejected" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApplicationForms_ActiveDiscordKey",
                table: "ApplicationForms");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationForms_DiscordId",
                table: "ApplicationForms");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationForms_IsAccepted_IsRejected",
                table: "ApplicationForms");

            migrationBuilder.DropColumn(
                name: "ActiveDiscordKey",
                table: "ApplicationForms");

            migrationBuilder.DropColumn(
                name: "FlaggedAt",
                table: "ApplicationForms");

            migrationBuilder.DropColumn(
                name: "IsFlagged",
                table: "ApplicationForms");
        }
    }
}
