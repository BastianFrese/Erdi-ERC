using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erdi_ERC.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaguePenaltyCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "LeaguePenalties",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "UTC_TIMESTAMP(6)");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "LeaguePenalties",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Incident",
                table: "LeaguePenalties",
                type: "varchar(2048)",
                maxLength: 2048,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "LeaguePenalties",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PenaltyType",
                table: "LeaguePenalties",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RaceTrack",
                table: "LeaguePenalties",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SecondDriver",
                table: "LeaguePenalties",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_LeaguePenalties_IsPublic_Date",
                table: "LeaguePenalties",
                columns: new[] { "IsPublic", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LeaguePenalties_IsPublic_Date",
                table: "LeaguePenalties");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "LeaguePenalties");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "LeaguePenalties");

            migrationBuilder.DropColumn(
                name: "Incident",
                table: "LeaguePenalties");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "LeaguePenalties");

            migrationBuilder.DropColumn(
                name: "PenaltyType",
                table: "LeaguePenalties");

            migrationBuilder.DropColumn(
                name: "RaceTrack",
                table: "LeaguePenalties");

            migrationBuilder.DropColumn(
                name: "SecondDriver",
                table: "LeaguePenalties");
        }
    }
}
