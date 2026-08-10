using Microsoft.EntityFrameworkCore.Migrations;

***REMOVED***nullable disable

namespace <OWNER_HANDLE>_ERC.Migrations
{
    public partial class AddRecurringStreamScheduleFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The StreamSchedules table was created in 20260428031730_AddTodoFeatures
            // without the recurring-schedule columns (the model was ahead of the schema).
            // These statements bring the DB in line with the modeled StreamSchedule entity.
            //
            // Idempotent: this migration was originally committed without a Designer file,
            // so EF never applied it and some environments already have a subset of these
            // columns (added manually). Guard every ADD/CREATE against INFORMATION_SCHEMA
            // so the migration is safe to run on any partial state. MySQL has no native
            // "ADD COLUMN IF NOT EXISTS", so we branch via a prepared statement.
            AddColumnIfNotExists(migrationBuilder, "StreamSchedules", "DayOfWeek", "int NULL");
            AddColumnIfNotExists(migrationBuilder, "StreamSchedules", "IsRecurring", "tinyint(1) NOT NULL DEFAULT 0");
            AddColumnIfNotExists(migrationBuilder, "StreamSchedules", "TimeOfDay", "time(6) NULL");
            CreateIndexIfNotExists(
                migrationBuilder,
                "StreamSchedules",
                "IX_StreamSchedules_IsRecurring_DayOfWeek",
                "(`IsRecurring`, `DayOfWeek`)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropIndexIfExists(migrationBuilder, "StreamSchedules", "IX_StreamSchedules_IsRecurring_DayOfWeek");
            DropColumnIfExists(migrationBuilder, "StreamSchedules", "DayOfWeek");
            DropColumnIfExists(migrationBuilder, "StreamSchedules", "IsRecurring");
            DropColumnIfExists(migrationBuilder, "StreamSchedules", "TimeOfDay");
        }

        private static void AddColumnIfNotExists(MigrationBuilder migrationBuilder, string table, string column, string definition)
        {
            migrationBuilder.Sql($@"
SET @sql = (SELECT IF(COUNT(*) = 0,
    'ALTER TABLE `{table}` ADD COLUMN `{column}` {definition}',
    'SELECT 1')
FROM information_schema.columns
WHERE table_schema = DATABASE() AND table_name = '{table}' AND column_name = '{column}');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;");
        }

        private static void DropColumnIfExists(MigrationBuilder migrationBuilder, string table, string column)
        {
            migrationBuilder.Sql($@"
SET @sql = (SELECT IF(COUNT(*) > 0,
    'ALTER TABLE `{table}` DROP COLUMN `{column}`',
    'SELECT 1')
FROM information_schema.columns
WHERE table_schema = DATABASE() AND table_name = '{table}' AND column_name = '{column}');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;");
        }

        private static void CreateIndexIfNotExists(MigrationBuilder migrationBuilder, string table, string index, string columnsSql)
        {
            migrationBuilder.Sql($@"
SET @sql = (SELECT IF(COUNT(*) = 0,
    'CREATE INDEX `{index}` ON `{table}` {columnsSql}',
    'SELECT 1')
FROM information_schema.statistics
WHERE table_schema = DATABASE() AND table_name = '{table}' AND index_name = '{index}' LIMIT 1);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;");
        }

        private static void DropIndexIfExists(MigrationBuilder migrationBuilder, string table, string index)
        {
            migrationBuilder.Sql($@"
SET @sql = (SELECT IF(COUNT(*) > 0,
    'DROP INDEX `{index}` ON `{table}`',
    'SELECT 1')
FROM information_schema.statistics
WHERE table_schema = DATABASE() AND table_name = '{table}' AND index_name = '{index}' LIMIT 1);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;");
        }
    }
}