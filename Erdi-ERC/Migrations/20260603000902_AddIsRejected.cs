using Microsoft.EntityFrameworkCore.Migrations;

***REMOVED***nullable disable

namespace <OWNER_HANDLE>_ERC.Migrations
{
    /// <inheritdoc />
    public partial class AddIsRejected : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewStatus",
                table: "ApplicationForms");

            migrationBuilder.AddColumn<bool>(
                name: "IsRejected",
                table: "ApplicationForms",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRejected",
                table: "ApplicationForms");

            migrationBuilder.AddColumn<int>(
                name: "ReviewStatus",
                table: "ApplicationForms",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
