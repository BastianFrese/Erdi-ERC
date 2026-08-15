using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erdi_ERC.Migrations
{
    /// <inheritdoc />
    public partial class LinkApplicationsToLeagues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<string>(
                name: "AppliedLeagueId",
                table: "ApplicationForms",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationForms_AppliedLeagueId",
                table: "ApplicationForms",
                column: "AppliedLeagueId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationForms_Leagues_AppliedLeagueId",
                table: "ApplicationForms",
                column: "AppliedLeagueId",
                principalTable: "Leagues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApplicationForms_Leagues_AppliedLeagueId",
                table: "ApplicationForms");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationForms_AppliedLeagueId",
                table: "ApplicationForms");

            migrationBuilder.DropColumn(
                name: "ApplicationInfo",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "IsOpenForApplications",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "AppliedLeagueId",
                table: "ApplicationForms");
        }
    }
}
