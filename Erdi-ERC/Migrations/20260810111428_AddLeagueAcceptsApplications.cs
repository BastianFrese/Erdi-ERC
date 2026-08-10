using Microsoft.EntityFrameworkCore.Migrations;

***REMOVED***nullable disable

namespace <OWNER_HANDLE>_ERC.Migrations
{
    /// <inheritdoc />
    public partial class AddLeagueAcceptsApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default true: bestehende Ligen bleiben bewerbbar (kein Verhaltenssprung beim Deploy);
            // Admins schließen Bewerbungen danach gezielt pro Liga.
            migrationBuilder.AddColumn<bool>(
                name: "AcceptsApplications",
                table: "Leagues",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptsApplications",
                table: "Leagues");
        }
    }
}
