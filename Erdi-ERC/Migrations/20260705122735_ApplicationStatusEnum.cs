using Microsoft.EntityFrameworkCore.Migrations;

***REMOVED***nullable disable

namespace <OWNER_HANDLE>_ERC.Migrations
{
    /// <summary>
    /// Bewerbungs-Datenmodell für den System-Neubau konsolidieren:
    /// 1. Persistiertes Status-Enum (0=Open, 1=Accepted, 2=Rejected) ersetzt die
    ///    widersprüchlichen Bool-Flags IsAccepted/IsRejected (Ablehnung gewinnt bei
    ///    Konflikt — betraf Bewerbung ***REMOVED***14, die beides gleichzeitig war).
    /// 2. Dedup-Guard (ActiveDiscordKey) wird auf Status umgestellt. Die STORED-
    ///    Generated-Column muss dafür gedroppt und neu angelegt werden — MySQL
    ///    erlaubt kein ALTER des Ausdrucks und kein Droppen referenzierter Spalten.
    /// 3. Altbestand ohne Liga-Verknüpfung bekommt AppliedLeagueId aus dem alten
    ///    Division-Freitext (Mapping auf die Ligen pro/second/rookie).
    /// 4. Bekannte Test-Datensätze werden markiert statt gelöscht.
    /// </summary>
    public partial class ApplicationStatusEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Status-Spalte anlegen und aus den alten Flags befüllen ────────
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "ApplicationForms",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Ablehnung gewinnt bei widersprüchlichen Flags (IsAccepted UND IsRejected):
            // die Ablehnung war jeweils die spätere Aktion (Auto-Dedup vom 17.06.2026).
            migrationBuilder.Sql(@"
                UPDATE `ApplicationForms`
                SET `Status` = CASE
                    WHEN `IsRejected` = 1 THEN 2
                    WHEN `IsAccepted` = 1 THEN 1
                    ELSE 0
                END;
            ");

            // ── 2. Dedup-Guard auf Status umstellen (Drop + Re-Create) ───────────
            migrationBuilder.DropIndex(
                name: "IX_ApplicationForms_ActiveDiscordKey",
                table: "ApplicationForms");

            migrationBuilder.DropColumn(
                name: "ActiveDiscordKey",
                table: "ApplicationForms");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationForms_IsAccepted_IsRejected",
                table: "ApplicationForms");

            migrationBuilder.DropColumn(
                name: "IsAccepted",
                table: "ApplicationForms");

            migrationBuilder.DropColumn(
                name: "IsRejected",
                table: "ApplicationForms");

            migrationBuilder.AddColumn<string>(
                name: "ActiveDiscordKey",
                table: "ApplicationForms",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true,
                computedColumnSql: "(CASE WHEN `Status` <> 2 THEN `DiscordId` ELSE NULL END)",
                stored: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationForms_ActiveDiscordKey",
                table: "ApplicationForms",
                column: "ActiveDiscordKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationForms_Status",
                table: "ApplicationForms",
                column: "Status");

            // ── 3. Altbestand: Liga-Verknüpfung aus dem Division-Freitext ableiten ─
            // Bewerbungen vor dem 11./12.06.2026 haben nur den alten Divisionsnamen.
            // EXISTS-Guard: nur backfillen, wenn die Ziel-Liga in dieser DB existiert
            // (FK ApplicationForms→Leagues). "Community Crossplay Event" hat keine
            // Ziel-Liga und bleibt bewusst ohne Verknüpfung.
            migrationBuilder.Sql(@"
                UPDATE `ApplicationForms`
                SET `AppliedLeagueId` = 'pro'
                WHERE (`AppliedLeagueId` IS NULL OR `AppliedLeagueId` = '')
                  AND (`Division` = 'Main Division 1' OR `Division` LIKE 'DIV MAIN%')
                  AND EXISTS (SELECT 1 FROM `Leagues` WHERE `Id` = 'pro');
            ");
            migrationBuilder.Sql(@"
                UPDATE `ApplicationForms`
                SET `AppliedLeagueId` = 'second'
                WHERE (`AppliedLeagueId` IS NULL OR `AppliedLeagueId` = '')
                  AND (`Division` = 'Second Crossplay Division 2' OR `Division` LIKE 'DIV SECOND%')
                  AND EXISTS (SELECT 1 FROM `Leagues` WHERE `Id` = 'second');
            ");
            migrationBuilder.Sql(@"
                UPDATE `ApplicationForms`
                SET `AppliedLeagueId` = 'rookie'
                WHERE (`AppliedLeagueId` IS NULL OR `AppliedLeagueId` = '')
                  AND (`Division` = 'Rookie Crossplay Division 3' OR `Division` LIKE 'DIV 3 ROOKIE%')
                  AND EXISTS (SELECT 1 FROM `Leagues` WHERE `Id` = 'rookie');
            ");

            // ── 4. Bekannte Test-Datensätze markieren (nicht löschen) ────────────
            // Ids + DiscordName doppelt abgesichert, damit in fremden DBs nichts
            // Falsches markiert wird. Idempotent über den Notiz-Prefix.
            migrationBuilder.Sql(@"
                UPDATE `ApplicationForms`
                SET `ReviewNote` = CONCAT('[Test-Datensatz] ', COALESCE(`ReviewNote`, ''))
                WHERE ((`Id` = 12 AND `DiscordName` = 'thenobraingamer')
                    OR (`Id` = 16 AND `DiscordName` = 'aimfish030')
                    OR (`Id` = 121 AND `DiscordName` = 'phinuu'))
                  AND (`ReviewNote` IS NULL OR `ReviewNote` NOT LIKE '[Test-Datensatz]%');
            ");
            // Die noch offene Test-Bewerbung zusätzlich flaggen, damit sie den Admins
            // in der Review-Liste auffällt und bewusst entschieden werden kann.
            migrationBuilder.Sql(@"
                UPDATE `ApplicationForms`
                SET `IsFlagged` = 1, `FlaggedAt` = UTC_TIMESTAMP()
                WHERE `Id` = 12 AND `DiscordName` = 'thenobraingamer' AND `Status` = 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reihenfolge gespiegelt: Generated Column erst droppen, dann die Bools
            // aus Status rekonstruieren, dann den alten Guard wiederherstellen.
            // (Liga-Backfill und Test-Markierungen bleiben bewusst bestehen.)
            migrationBuilder.DropIndex(
                name: "IX_ApplicationForms_Status",
                table: "ApplicationForms");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationForms_ActiveDiscordKey",
                table: "ApplicationForms");

            migrationBuilder.DropColumn(
                name: "ActiveDiscordKey",
                table: "ApplicationForms");

            migrationBuilder.AddColumn<bool>(
                name: "IsAccepted",
                table: "ApplicationForms",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRejected",
                table: "ApplicationForms",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(@"
                UPDATE `ApplicationForms`
                SET `IsAccepted` = CASE WHEN `Status` = 1 THEN 1 ELSE 0 END,
                    `IsRejected` = CASE WHEN `Status` = 2 THEN 1 ELSE 0 END;
            ");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ApplicationForms");

            migrationBuilder.AddColumn<string>(
                name: "ActiveDiscordKey",
                table: "ApplicationForms",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true,
                computedColumnSql: "(CASE WHEN `IsRejected` = 0 THEN `DiscordId` ELSE NULL END)",
                stored: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationForms_ActiveDiscordKey",
                table: "ApplicationForms",
                column: "ActiveDiscordKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationForms_IsAccepted_IsRejected",
                table: "ApplicationForms",
                columns: new[] { "IsAccepted", "IsRejected" });
        }
    }
}
