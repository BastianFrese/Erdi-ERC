using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erdi_ERC.Migrations
{
    public partial class AddStewarding : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add PenaltyType column
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'LeaguePenalties';
                SET @columnname = 'PenaltyType';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                     WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = @tablename AND COLUMN_NAME = @columnname) > 0,
                    'SELECT 1',
                    'ALTER TABLE `LeaguePenalties` ADD COLUMN `PenaltyType` varchar(32) NOT NULL DEFAULT ''Punkteabzug'' AFTER `Driver`'
                ));
                PREPARE s FROM @preparedStatement; EXECUTE s; DEALLOCATE PREPARE s;
            ");

            // Add RaceTrack column
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'LeaguePenalties';
                SET @columnname = 'RaceTrack';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                     WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = @tablename AND COLUMN_NAME = @columnname) > 0,
                    'SELECT 1',
                    'ALTER TABLE `LeaguePenalties` ADD COLUMN `RaceTrack` varchar(128) NULL AFTER `PenaltyType`'
                ));
                PREPARE s FROM @preparedStatement; EXECUTE s; DEALLOCATE PREPARE s;
            ");

            // Add SecondDriver column
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'LeaguePenalties';
                SET @columnname = 'SecondDriver';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                     WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = @tablename AND COLUMN_NAME = @columnname) > 0,
                    'SELECT 1',
                    'ALTER TABLE `LeaguePenalties` ADD COLUMN `SecondDriver` varchar(128) NULL AFTER `RaceTrack`'
                ));
                PREPARE s FROM @preparedStatement; EXECUTE s; DEALLOCATE PREPARE s;
            ");

            // Add Incident column
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'LeaguePenalties';
                SET @columnname = 'Incident';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                     WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = @tablename AND COLUMN_NAME = @columnname) > 0,
                    'SELECT 1',
                    'ALTER TABLE `LeaguePenalties` ADD COLUMN `Incident` varchar(2048) NULL AFTER `SecondDriver`'
                ));
                PREPARE s FROM @preparedStatement; EXECUTE s; DEALLOCATE PREPARE s;
            ");

            // Add IsPublic column
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'LeaguePenalties';
                SET @columnname = 'IsPublic';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                     WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = @tablename AND COLUMN_NAME = @columnname) > 0,
                    'SELECT 1',
                    'ALTER TABLE `LeaguePenalties` ADD COLUMN `IsPublic` tinyint(1) NOT NULL DEFAULT 1 AFTER `Reason`'
                ));
                PREPARE s FROM @preparedStatement; EXECUTE s; DEALLOCATE PREPARE s;
            ");

            // Add CreatedAt column
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'LeaguePenalties';
                SET @columnname = 'CreatedAt';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                     WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = @tablename AND COLUMN_NAME = @columnname) > 0,
                    'SELECT 1',
                    'ALTER TABLE `LeaguePenalties` ADD COLUMN `CreatedAt` datetime(6) NOT NULL DEFAULT NOW(6) AFTER `IsPublic`'
                ));
                PREPARE s FROM @preparedStatement; EXECUTE s; DEALLOCATE PREPARE s;
            ");

            // Add CreatedBy column
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'LeaguePenalties';
                SET @columnname = 'CreatedBy';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                     WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = @tablename AND COLUMN_NAME = @columnname) > 0,
                    'SELECT 1',
                    'ALTER TABLE `LeaguePenalties` ADD COLUMN `CreatedBy` varchar(128) NULL AFTER `CreatedAt`'
                ));
                PREPARE s FROM @preparedStatement; EXECUTE s; DEALLOCATE PREPARE s;
            ");

            // Add index for public + date queries
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_LeaguePenalties_IsPublic_Date` ON `LeaguePenalties` (`IsPublic`, `Date`);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS `IX_LeaguePenalties_IsPublic_Date` ON `LeaguePenalties`;");
            migrationBuilder.Sql("ALTER TABLE `LeaguePenalties` DROP COLUMN IF EXISTS `CreatedBy`;");
            migrationBuilder.Sql("ALTER TABLE `LeaguePenalties` DROP COLUMN IF EXISTS `CreatedAt`;");
            migrationBuilder.Sql("ALTER TABLE `LeaguePenalties` DROP COLUMN IF EXISTS `IsPublic`;");
            migrationBuilder.Sql("ALTER TABLE `LeaguePenalties` DROP COLUMN IF EXISTS `Incident`;");
            migrationBuilder.Sql("ALTER TABLE `LeaguePenalties` DROP COLUMN IF EXISTS `SecondDriver`;");
            migrationBuilder.Sql("ALTER TABLE `LeaguePenalties` DROP COLUMN IF EXISTS `RaceTrack`;");
            migrationBuilder.Sql("ALTER TABLE `LeaguePenalties` DROP COLUMN IF EXISTS `PenaltyType`;");
        }
    }
}
