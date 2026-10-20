using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erdi_ERC.Migrations
{
    /// <inheritdoc />
    public partial class AddDotdBallotVotedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DotdBallots_VoteId_VotedAt",
                table: "DotdBallots",
                columns: new[] { "VoteId", "VotedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DotdBallots_VoteId_VotedAt",
                table: "DotdBallots");
        }
    }
}
