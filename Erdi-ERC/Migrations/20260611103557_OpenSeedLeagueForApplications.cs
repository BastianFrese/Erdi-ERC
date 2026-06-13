using Microsoft.EntityFrameworkCore.Migrations;

***REMOVED***nullable disable

namespace <OWNER_HANDLE>_ERC.Migrations
{
    /// <inheritdoc />
    public partial class OpenSeedLeagueForApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Öffnet die Seed-Liga "ERC Pro League" fürs öffentliche Bewerbungsformular,
            // damit auf frischen (Test-)Datenbanken sofort eine bewerbbare Liga existiert.
            // Bewusst auf Id UND unveränderten Namen eingeschränkt: Wurde die Liga
            // (z.B. auf Prod) umbenannt oder gelöscht, ändert dieses Update nichts —
            // echte Ligen werden ausschließlich bewusst über den Admin freigegeben.
            migrationBuilder.Sql(
                "UPDATE `Leagues` " +
                "SET `IsOpenForApplications` = 1, " +
                "    `ApplicationInfo` = 'Freitags 20:00 · KI bis 105' " +
                "WHERE `Id` = 'pro' AND `Name` = 'ERC Pro League';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE `Leagues` " +
                "SET `IsOpenForApplications` = 0, " +
                "    `ApplicationInfo` = NULL " +
                "WHERE `Id` = 'pro' AND `Name` = 'ERC Pro League';");
        }
    }
}
