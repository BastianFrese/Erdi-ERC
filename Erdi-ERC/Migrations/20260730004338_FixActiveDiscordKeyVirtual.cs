using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erdi_ERC.Migrations
{
    /// <summary>
    /// Synchronisiert den ModelSnapshot mit der virtuellen ActiveDiscordKey-Spalte.
    /// Die tatsächliche Schema-Änderung (Spalte ist bereits korrekt als VIRTUAL angelegt)
    /// wird durch Raw-SQL in den vorherigen Migrationen abgedeckt, da Pomelo 9.0.0 für
    /// nullable generated columns ungültiges SQL erzeugt.
    /// </summary>
    public partial class FixActiveDiscordKeyVirtual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: ActiveDiscordKey wurde bereits in vorherigen Migrationen korrekt angelegt.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: Down ist hier nicht sinnvoll, da die Spalte vorher durch Raw-SQL existiert.
        }
    }
}
