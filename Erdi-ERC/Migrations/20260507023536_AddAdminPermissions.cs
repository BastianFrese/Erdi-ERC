using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

***REMOVED***nullable disable

namespace <OWNER_HANDLE>_ERC.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'ApplicationForms';
                SET @columnname = 'DiscordId';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                     WHERE TABLE_SCHEMA = @dbname
                       AND TABLE_NAME = @tablename
                       AND COLUMN_NAME = @columnname) > 0,
                    'SELECT 1',
                    CONCAT('ALTER TABLE `', @tablename, '` ADD COLUMN `', @columnname, '` varchar(32) NULL')
                ));
                PREPARE alterIfNotExists FROM @preparedStatement;
                EXECUTE alterIfNotExists;
                DEALLOCATE PREPARE alterIfNotExists;
            ");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'AdminUsers';
                SET @columnname = 'IsSuperAdmin';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                     WHERE TABLE_SCHEMA = @dbname
                       AND TABLE_NAME = @tablename
                       AND COLUMN_NAME = @columnname) > 0,
                    'SELECT 1',
                    CONCAT('ALTER TABLE `', @tablename, '` ADD COLUMN `', @columnname, '` tinyint(1) NOT NULL DEFAULT 0')
                ));
                PREPARE alterIfNotExists FROM @preparedStatement;
                EXECUTE alterIfNotExists;
                DEALLOCATE PREPARE alterIfNotExists;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `AchievementDefinitions` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `Key` varchar(64) NOT NULL,
                    `Title` varchar(128) NOT NULL,
                    `Description` varchar(512) NOT NULL,
                    `Icon` varchar(64) NOT NULL,
                    `Tone` varchar(32) NOT NULL,
                    `Tier` varchar(32) NOT NULL,
                    `Category` varchar(64) NOT NULL,
                    `Metric` int NOT NULL DEFAULT 0,
                    `Target` int NOT NULL DEFAULT 1,
                    `IsActive` tinyint(1) NOT NULL DEFAULT 1,
                    `IsBuiltIn` tinyint(1) NOT NULL DEFAULT 0,
                    `SortOrder` int NOT NULL DEFAULT 0,
                    PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `AdminUserPermissions` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `DiscordId` varchar(32) NOT NULL,
                    `Permission` varchar(64) NOT NULL,
                    PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_AdminUserPermissions_AdminUsers_DiscordId`
                        FOREIGN KEY (`DiscordId`) REFERENCES `AdminUsers`(`DiscordId`) ON DELETE CASCADE
                ) CHARACTER SET=utf8mb4;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `CommunityNewsPosts` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `Title` varchar(160) NOT NULL,
                    `Category` varchar(96) NOT NULL,
                    `Summary` varchar(480) NULL,
                    `Content` LONGTEXT NOT NULL,
                    `AuthorName` varchar(128) NOT NULL,
                    `IsPinned` tinyint(1) NOT NULL DEFAULT 0,
                    `IsPublished` tinyint(1) NOT NULL DEFAULT 1,
                    `CreatedAt` datetime(6) NOT NULL,
                    `PublishedAt` datetime(6) NOT NULL,
                    PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `CommunityVotePolls` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `Title` varchar(160) NOT NULL,
                    `Category` varchar(96) NOT NULL,
                    `Description` varchar(480) NULL,
                    `IsActive` tinyint(1) NOT NULL DEFAULT 1,
                    `CreatedAt` datetime(6) NOT NULL,
                    `ExpiresAt` datetime(6) NULL,
                    PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `CommunityVoteResponses` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `PollId` int NOT NULL,
                    `OptionId` int NOT NULL,
                    `DiscordId` varchar(32) NOT NULL,
                    `DiscordName` varchar(128) NOT NULL,
                    `CreatedAt` datetime(6) NOT NULL,
                    PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `DriverProfiles` (
                    `DiscordId` varchar(32) NOT NULL,
                    `DiscordName` varchar(128) NOT NULL,
                    `DisplayName` varchar(128) NULL,
                    `FavoriteTrack` varchar(128) NULL,
                    `InputDevice` varchar(64) NULL,
                    `PreferredPlatform` varchar(64) NULL,
                    `Nationality` varchar(64) NULL,
                    `Bio` varchar(512) NULL,
                    `CreatedAt` datetime(6) NOT NULL,
                    `UpdatedAt` datetime(6) NOT NULL,
                    PRIMARY KEY (`DiscordId`)
                ) CHARACTER SET=utf8mb4;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `ProfileWallMessages` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `ProfileDiscordId` varchar(32) NOT NULL,
                    `AuthorDiscordId` varchar(32) NOT NULL,
                    `AuthorName` varchar(128) NOT NULL,
                    `Message` varchar(600) NOT NULL,
                    `CreatedAt` datetime(6) NOT NULL,
                    PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `RaceAvailabilityEntries` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `EventId` int NOT NULL,
                    `DiscordId` varchar(32) NOT NULL,
                    `DiscordName` varchar(128) NOT NULL,
                    `Status` varchar(24) NOT NULL,
                    `Note` varchar(240) NULL,
                    `UpdatedAt` datetime(6) NOT NULL,
                    PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `RaceHighlightClips` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `Title` varchar(160) NOT NULL,
                    `Url` varchar(512) NOT NULL,
                    `Category` varchar(96) NOT NULL,
                    `RaceLabel` varchar(160) NULL,
                    `SubmittedByDiscordId` varchar(32) NULL,
                    `SubmittedByName` varchar(128) NOT NULL,
                    `IsApproved` tinyint(1) NOT NULL DEFAULT 1,
                    `CreatedAt` datetime(6) NOT NULL,
                    PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `SetupComments` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `TrackSetupId` int NOT NULL,
                    `AuthorDiscordId` varchar(32) NOT NULL,
                    `AuthorName` varchar(128) NOT NULL,
                    `Message` varchar(600) NOT NULL,
                    `CreatedAt` datetime(6) NOT NULL,
                    PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `SetupLikes` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `TrackSetupId` int NOT NULL,
                    `DiscordId` varchar(32) NOT NULL,
                    `DiscordName` varchar(128) NOT NULL,
                    `CreatedAt` datetime(6) NOT NULL,
                    PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `CommunityVoteOptions` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `PollId` int NOT NULL,
                    `Label` varchar(160) NOT NULL,
                    PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_CommunityVoteOptions_CommunityVotePolls_PollId`
                        FOREIGN KEY (`PollId`) REFERENCES `CommunityVotePolls`(`Id`) ON DELETE CASCADE
                ) CHARACTER SET=utf8mb4;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `DriverGamerTags` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `DiscordId` varchar(32) NOT NULL,
                    `Platform` varchar(64) NOT NULL,
                    `GamerTag` varchar(128) NOT NULL,
                    `IsPrimary` tinyint(1) NOT NULL DEFAULT 0,
                    `LinkedAt` datetime(6) NOT NULL,
                    `LinkedByDiscordId` varchar(32) NULL,
                    PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_DriverGamerTags_DriverProfiles_DiscordId`
                        FOREIGN KEY (`DiscordId`) REFERENCES `DriverProfiles`(`DiscordId`) ON DELETE CASCADE
                ) CHARACTER SET=utf8mb4;
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS `IX_AchievementDefinitions_Key`
                ON `AchievementDefinitions` (`Key`);
            ");

            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `IX_AdminUserPermissions_DiscordId_Permission` ON `AdminUserPermissions` (`DiscordId`, `Permission`);");

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_CommunityNewsPosts_IsPublished_IsPinned_PublishedAt` ON `CommunityNewsPosts` (`IsPublished`, `IsPinned`, `PublishedAt`);");

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_CommunityVoteOptions_PollId` ON `CommunityVoteOptions` (`PollId`);");

            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `IX_CommunityVoteResponses_PollId_DiscordId` ON `CommunityVoteResponses` (`PollId`, `DiscordId`);");

            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `IX_DriverGamerTags_DiscordId_Platform` ON `DriverGamerTags` (`DiscordId`, `Platform`);");

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_DriverGamerTags_GamerTag` ON `DriverGamerTags` (`GamerTag`);");

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_ProfileWallMessages_ProfileDiscordId_CreatedAt` ON `ProfileWallMessages` (`ProfileDiscordId`, `CreatedAt`);");

            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `IX_RaceAvailabilityEntries_EventId_DiscordId` ON `RaceAvailabilityEntries` (`EventId`, `DiscordId`);");

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_RaceAvailabilityEntries_EventId_Status` ON `RaceAvailabilityEntries` (`EventId`, `Status`);");

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_RaceHighlightClips_IsApproved_CreatedAt` ON `RaceHighlightClips` (`IsApproved`, `CreatedAt`);");

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_SetupComments_TrackSetupId_CreatedAt` ON `SetupComments` (`TrackSetupId`, `CreatedAt`);");

            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `IX_SetupLikes_TrackSetupId_DiscordId` ON `SetupLikes` (`TrackSetupId`, `DiscordId`);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS `AchievementDefinitions`;");

            migrationBuilder.DropTable(
                name: "AdminUserPermissions");

            migrationBuilder.DropTable(
                name: "CommunityNewsPosts");

            migrationBuilder.DropTable(
                name: "CommunityVoteOptions");

            migrationBuilder.DropTable(
                name: "CommunityVoteResponses");

            migrationBuilder.DropTable(
                name: "DriverGamerTags");

            migrationBuilder.DropTable(
                name: "ProfileWallMessages");

            migrationBuilder.DropTable(
                name: "RaceAvailabilityEntries");

            migrationBuilder.DropTable(
                name: "RaceHighlightClips");

            migrationBuilder.DropTable(
                name: "SetupComments");

            migrationBuilder.DropTable(
                name: "SetupLikes");

            migrationBuilder.DropTable(
                name: "CommunityVotePolls");

            migrationBuilder.DropTable(
                name: "DriverProfiles");

            migrationBuilder.DropColumn(
                name: "DiscordId",
                table: "ApplicationForms");

            migrationBuilder.DropColumn(
                name: "IsSuperAdmin",
                table: "AdminUsers");
        }
    }
}
