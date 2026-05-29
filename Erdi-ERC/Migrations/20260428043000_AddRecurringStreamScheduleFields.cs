using Microsoft.EntityFrameworkCore.Migrations;

***REMOVED***nullable disable

namespace <OWNER_HANDLE>_ERC.Migrations
{
    [Migration("20260428043000_AddRecurringStreamScheduleFields")]
    public partial class AddRecurringStreamScheduleFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `StreamSchedules` ADD COLUMN IF NOT EXISTS `DayOfWeek` int NULL;");
            migrationBuilder.Sql("ALTER TABLE `StreamSchedules` ADD COLUMN IF NOT EXISTS `IsRecurring` tinyint(1) NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE `StreamSchedules` ADD COLUMN IF NOT EXISTS `TimeOfDay` time(6) NULL;");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_StreamSchedules_IsRecurring_DayOfWeek` ON `StreamSchedules` (`IsRecurring`, `DayOfWeek`);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS `IX_StreamSchedules_IsRecurring_DayOfWeek` ON `StreamSchedules`;");
            migrationBuilder.Sql("ALTER TABLE `StreamSchedules` DROP COLUMN IF EXISTS `DayOfWeek`;");
            migrationBuilder.Sql("ALTER TABLE `StreamSchedules` DROP COLUMN IF EXISTS `IsRecurring`;");
            migrationBuilder.Sql("ALTER TABLE `StreamSchedules` DROP COLUMN IF EXISTS `TimeOfDay`;");
        }
    }
}
