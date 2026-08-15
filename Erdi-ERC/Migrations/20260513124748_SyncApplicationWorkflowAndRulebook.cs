using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erdi_ERC.Migrations
{
    /// <inheritdoc />
    public partial class SyncApplicationWorkflowAndRulebook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RegelwerkDocuments_IsActive",
                table: "RegelwerkDocuments",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DriverRoleHistories_LeagueId_Driver",
                table: "DriverRoleHistories",
                columns: new[] { "LeagueId", "Driver" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RegelwerkDocuments_IsActive",
                table: "RegelwerkDocuments");

            migrationBuilder.DropIndex(
                name: "IX_DriverRoleHistories_LeagueId_Driver",
                table: "DriverRoleHistories");
        }
    }
}
