using System;
using Microsoft.EntityFrameworkCore.Migrations;

***REMOVED***nullable disable

namespace <OWNER_HANDLE>_ERC.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookRuleStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailureCount",
                table: "WebhookAutomationRules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "WebhookAutomationRules",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRunAt",
                table: "WebhookAutomationRules",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastStatus",
                table: "WebhookAutomationRules",
                type: "varchar(16)",
                maxLength: 16,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "SuccessCount",
                table: "WebhookAutomationRules",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailureCount",
                table: "WebhookAutomationRules");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "WebhookAutomationRules");

            migrationBuilder.DropColumn(
                name: "LastRunAt",
                table: "WebhookAutomationRules");

            migrationBuilder.DropColumn(
                name: "LastStatus",
                table: "WebhookAutomationRules");

            migrationBuilder.DropColumn(
                name: "SuccessCount",
                table: "WebhookAutomationRules");
        }
    }
}
