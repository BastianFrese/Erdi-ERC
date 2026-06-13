using Microsoft.EntityFrameworkCore.Migrations;

***REMOVED***nullable disable

namespace <OWNER_HANDLE>_ERC.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackSetupGameYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrackSetups_Track_RequiredAccessTier",
                table: "TrackSetups");

            migrationBuilder.AddColumn<string>(
                name: "GameYear",
                table: "TrackSetups",
                type: "varchar(16)",
                maxLength: 16,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_TrackSetups_Track_GameYear_RequiredAccessTier",
                table: "TrackSetups",
                columns: new[] { "Track", "GameYear", "RequiredAccessTier" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrackSetups_Track_GameYear_RequiredAccessTier",
                table: "TrackSetups");

            migrationBuilder.DropColumn(
                name: "GameYear",
                table: "TrackSetups");

            migrationBuilder.CreateIndex(
                name: "IX_TrackSetups_Track_RequiredAccessTier",
                table: "TrackSetups",
                columns: new[] { "Track", "RequiredAccessTier" });
        }
    }
}
