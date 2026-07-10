CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;
DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260420183736_InitialCreate') THEN

    ALTER DATABASE CHARACTER SET utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260420183736_InitialCreate') THEN

    CREATE TABLE `AdminUsers` (
        `DiscordId` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
        `DisplayName` varchar(128) CHARACTER SET utf8mb4 NULL,
        `AddedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_AdminUsers` PRIMARY KEY (`DiscordId`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260420183736_InitialCreate') THEN

    CREATE TABLE `Leagues` (
        `Id` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `Name` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `Description` varchar(1024) CHARACTER SET utf8mb4 NOT NULL,
        CONSTRAINT `PK_Leagues` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260420183736_InitialCreate') THEN

    CREATE TABLE `RealLifeEvents` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `Title` longtext CHARACTER SET utf8mb4 NOT NULL,
        `Date` datetime(6) NOT NULL,
        `Location` longtext CHARACTER SET utf8mb4 NOT NULL,
        `Description` longtext CHARACTER SET utf8mb4 NOT NULL,
        `ImageFileName` longtext CHARACTER SET utf8mb4 NULL,
        `IsUpcoming` tinyint(1) NOT NULL,
        CONSTRAINT `PK_RealLifeEvents` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260420183736_InitialCreate') THEN

    CREATE TABLE `DriverStandings` (
        `RowId` int NOT NULL AUTO_INCREMENT,
        `LeagueId` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `Position` int NOT NULL,
        `Driver` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `Team` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `Points` int NOT NULL,
        `Wins` int NOT NULL,
        CONSTRAINT `PK_DriverStandings` PRIMARY KEY (`RowId`),
        CONSTRAINT `FK_DriverStandings_Leagues_LeagueId` FOREIGN KEY (`LeagueId`) REFERENCES `Leagues` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260420183736_InitialCreate') THEN

    CREATE TABLE `RaceResults` (
        `RowId` int NOT NULL AUTO_INCREMENT,
        `LeagueId` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `Date` datetime(6) NOT NULL,
        `Track` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `Winner` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `FastestLap` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        CONSTRAINT `PK_RaceResults` PRIMARY KEY (`RowId`),
        CONSTRAINT `FK_RaceResults_Leagues_LeagueId` FOREIGN KEY (`LeagueId`) REFERENCES `Leagues` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260420183736_InitialCreate') THEN

    CREATE TABLE `UpcomingEvents` (
        `RowId` int NOT NULL AUTO_INCREMENT,
        `LeagueId` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `Date` datetime(6) NOT NULL,
        `Track` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `Format` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        CONSTRAINT `PK_UpcomingEvents` PRIMARY KEY (`RowId`),
        CONSTRAINT `FK_UpcomingEvents_Leagues_LeagueId` FOREIGN KEY (`LeagueId`) REFERENCES `Leagues` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260420183736_InitialCreate') THEN

    CREATE INDEX `IX_DriverStandings_LeagueId` ON `DriverStandings` (`LeagueId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260420183736_InitialCreate') THEN

    CREATE INDEX `IX_RaceResults_LeagueId` ON `RaceResults` (`LeagueId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260420183736_InitialCreate') THEN

    CREATE INDEX `IX_UpcomingEvents_LeagueId` ON `UpcomingEvents` (`LeagueId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260420183736_InitialCreate') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260420183736_InitialCreate', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260420192705_AddRealLifeEventImages') THEN

    ALTER TABLE `RealLifeEvents` MODIFY COLUMN `Title` varchar(256) CHARACTER SET utf8mb4 NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260420192705_AddRealLifeEventImages') THEN

    ALTER TABLE `RealLifeEvents` MODIFY COLUMN `Location` varchar(256) CHARACTER SET utf8mb4 NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260420192705_AddRealLifeEventImages') THEN

    ALTER TABLE `RealLifeEvents` MODIFY COLUMN `ImageFileName` varchar(256) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260420192705_AddRealLifeEventImages') THEN

    ALTER TABLE `RealLifeEvents` MODIFY COLUMN `Description` TEXT CHARACTER SET utf8mb4 NOT NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260420192705_AddRealLifeEventImages') THEN

    CREATE TABLE `RealLifeEventImages` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `EventId` int NOT NULL,
        `FileName` varchar(256) CHARACTER SET utf8mb4 NOT NULL,
        `Caption` varchar(512) CHARACTER SET utf8mb4 NULL,
        `UploadedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_RealLifeEventImages` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_RealLifeEventImages_RealLifeEvents_EventId` FOREIGN KEY (`EventId`) REFERENCES `RealLifeEvents` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260420192705_AddRealLifeEventImages') THEN

    CREATE INDEX `IX_RealLifeEventImages_EventId` ON `RealLifeEventImages` (`EventId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260420192705_AddRealLifeEventImages') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260420192705_AddRealLifeEventImages', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260420200000_AddRealLifeEvents') THEN

    INSERT INTO `RealLifeEvents` (`Title`, `Date`, `Location`, `Description`, `ImageFileName`, `IsUpcoming`)
    VALUES ('KART-EVENT 1 · Kartbahn Werther', TIMESTAMP '2021-07-01 00:00:00', 'Kartbahn Werther', 'Hiermit endet das erste Twitch-Community-Kart-Event. Ich bedanke mich bei Fly2, MaikF1, Sairajjin, Grete, Phil, Beatmixer, Hctaw, Andiex, KevinGo, Sasuke, Inifinity, Leadx, Nikesfreundin, Kleriker, Ares, Leopard, Paranoid, Swarley & Towelie für das Erlebnis. Im Laufe der nächsten Tage wird dazu noch ein VLOG folgen. Falls ihr für’s nächste Mal mit dabei sein möchtet – im Discord wird’s neue Infos geben!', NULL, FALSE),
    ('KART-EVENT 2 · Highway Kartracing Dortmund', TIMESTAMP '2021-08-01 00:00:00', 'Highway Kartracing, Dortmund', CONCAT('Hiermit endet das zweite Twitch-Community-Kart-Event. Ich bedanke mich bei meiner Community für das wiedermal geile Erlebnis. Für mich war nach einem katastrophalen Qualy nicht mehr als P4 drin. Gz an Fly, Metten und Towelie für die ersten 3 Plätze und die Pokale 🏆', CHAR(10), 'Falls ihr für’s nächste Mal mit dabei sein möchtet – im Discord wird’s neue Infos geben! Habt ihr Vorschläge für eine mögliche neue Location?'), NULL, FALSE),
    ('KART-EVENT 3 · Michael-Schumacher-Kartbahn Kerpen', TIMESTAMP '2021-09-01 00:00:00', 'Michael-Schumacher-Kartbahn, Kerpen', 'Community Treffen und DA IST DER SIEG – 1 Stunde pures Racing – was ein Kopf-an-Kopf-Rennen mit @sauerbratentwitch, wir waren beide ZEITGLEICH. Liebe & Kuss geht raus an alle. ***REMOVED***ERC ***REMOVED***kart ***REMOVED***michaelschumacherkartbahn ***REMOVED***kartbahn ***REMOVED***twitch ***REMOVED***f12021 ***REMOVED***racing ***REMOVED***airborne', NULL, FALSE),
    ('KART-EVENT 4 · Ralf-Schumacher-Kartbahn Bispingen', TIMESTAMP '2022-04-01 00:00:00', 'Ralf-Schumacher-Kartbahn, Bispingen', CONCAT('SOOOOO <OWNER_HANDLE>’s Racing Community | ERC hat das mittlerweile 4. Deutschland-Kart-Event hinter sich. Nach Werther, Dortmund & Kerpen war diesmal die Ralf-Schumacher-Kartbahn in Bispingen dran. 14 Fahrer, 20 Zuschauer und es hat auf der langen Outdoor-Bahn wieder richtig Spaß gemacht zu racen!', CHAR(10), '', CHAR(10), 'Für mich sprang P3 im Qualy, P2 im Rennen, P4 im Reverse Grid und INDOOR Platz 1 raus.'), NULL, FALSE),
    ('KART-EVENT 5 · Motorsportarena Oppenrod', TIMESTAMP '2022-06-01 00:00:00', 'Motorsportarena Oppenrod, Buseck', CONCAT('Das war das 5. nationale Kart-Event der ERC. Nach Werther, Dortmund, Kerpen, Bispingen waren wir diesmal im Süden auf der Motorsportarena Oppenrod.', CHAR(10), '', CHAR(10), 'Für mich sprang P1 im Qualy raus. Wir starteten dann im Reverse Grid von hinten und mussten direkt am Anfang voll in die Eisen gehen – dann kam der SPIN! Am Ende fehlten knapp 2,7 Sekunden zum Sieger. Das zweite Rennen war ein atemberaubender 3er-Kampf mit Dennis & Sören – am Ende haben wir uns den SIEG geschnappt 🙂'), NULL, FALSE),
    ('KART-EVENT 6 · Michael-Schumacher-Kartbahn Kerpen', TIMESTAMP '2022-08-01 00:00:00', 'Michael-Schumacher-Kartbahn, Kerpen', CONCAT('Das war das 6. nationale Kart-Event der ERC. Exontic fuhr das Rennen mit einer kranken Pace von 53,4 verdient auf Platz 1. Mein Bruder Dennis mit dem absoluten Driver of the Day auf Platz 2 bei 53,6 – STARKER 2. Platz! Carl & ich lieferten uns einen spannenden Kopf-an-Kopf-Fight rundenlang, aber er mit 54,125 hauchdünn vor mir 54,159!', CHAR(10), '', CHAR(10), 'Die Fotos hat die Liebe @lin.d.a.aa gemacht. Ihr findet alle anderen 500+ Fotos in der Dropbox im Discord.'), NULL, FALSE),
    ('LAN-EVENT 1 · eSports Factory Osnabrück', TIMESTAMP '2022-12-01 00:00:00', 'eSports Factory, Osnabrück', CONCAT('Das war das ERC-3-Tage-LAN-Event in der eSports Factory in Osnabrück. Damit endet das Jahr mit einem 6. (!) EVENT in 2022!', CHAR(10), '', CHAR(10), 'Danke an jeden einzelnen, danke fürs Verwirklichen meines Traums/Hobbys. Die verbleibenden Goals werden in den nächsten Tagen natürlich noch ausgeschüttet.', CHAR(10), '', CHAR(10), 'Auf in ein weiteres neues Jahr.'), NULL, FALSE),
    ('KART-EVENT 7 · Ralf-Schumacher-Kartcenter Bispingen', TIMESTAMP '2023-04-01 00:00:00', 'Ralf-Schumacher-Kartcenter, Bispingen', '<OWNER_HANDLE>’s Racing Community ***REMOVED***ERC – Was ein wunderschöner Tag, das war das 7. Kart-Event mit einem rasanten Rennen wie man es von dieser geilen Strecke gewohnt ist. Glückwunsch an @trim.pluss, der das DING in Max-Verstappen-Niveau sowas von easy geholt hat 🙂 Meinen zweiten Platz nehm ich aber auf jeden Fall hautnah vor Mercy ✌️', NULL, FALSE),
    ('KART-EVENT 8 · Motorsportarena Oppenrod', TIMESTAMP '2024-07-20 13:00:00', 'Motorsportarena Oppenrod, Buseck (PLZ 35418)', CONCAT('Es ist wieder soweit – das 8. KART-EVENT findet statt in OPPENROD 2024.', CHAR(10), '', CHAR(10), 'WANN?  Samstag, den 20.7. ab 13:00 Uhr', CHAR(10), 'WO?    Motorsportarena Oppenrod in Buseck, PLZ 35418', CHAR(10), '', CHAR(10), 'https://www.motorsportarena-oppenrod.de/faq'), NULL, FALSE),
    ('KART-EVENT 9 · Michael-Schumacher Outdoor-Kartbahn Kerpen', TIMESTAMP '2025-05-03 18:00:00', 'Michael-Schumacher Outdoor-Kartbahn, Kerpen', CONCAT('Das nächste (9.) nationale KART-EVENT findet wieder in NRW statt – geisteskrank mittlerweile SO VIELE, danke euch ❤️', CHAR(10), '', CHAR(10), 'Auf der Michael-Schumacher Outdoor-Kartbahn in KERPEN.', CHAR(10), 'Samstag, den 3.5. ab 18:00 Uhr.'), NULL, TRUE);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260420200000_AddRealLifeEvents') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260420200000_AddRealLifeEvents', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260420201907_AddApplicationForm') THEN

    CREATE TABLE `ApplicationForms` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `Age` int NOT NULL,
        `DiscordName` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `Role` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `JoinedCommunityDiscord` tinyint(1) NOT NULL,
        `JoinedLeagueDiscord` tinyint(1) NOT NULL,
        `GamingName` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `Platform` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `AiLevel` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `Division` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `SubmittedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_ApplicationForms` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260420201907_AddApplicationForm') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260420201907_AddApplicationForm', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260421045652_AddRaceFinishes') THEN

    CREATE TABLE `RaceFinishes` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `RaceResultId` int NOT NULL,
        `Driver` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `Position` int NOT NULL,
        `FastestLap` tinyint(1) NOT NULL,
        CONSTRAINT `PK_RaceFinishes` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_RaceFinishes_RaceResults_RaceResultId` FOREIGN KEY (`RaceResultId`) REFERENCES `RaceResults` (`RowId`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260421045652_AddRaceFinishes') THEN

    CREATE INDEX `IX_RaceFinishes_RaceResultId` ON `RaceFinishes` (`RaceResultId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260421045652_AddRaceFinishes') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260421045652_AddRaceFinishes', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423100000_AddRaceTimeToRaceFinish') THEN

    ALTER TABLE `RaceFinishes` ADD `RaceTimeMs` int NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423100000_AddRaceTimeToRaceFinish') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260423100000_AddRaceTimeToRaceFinish', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423215956_AddApplicationAcceptance') THEN

    ALTER TABLE `ApplicationForms` ADD `AcceptedAt` datetime(6) NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423215956_AddApplicationAcceptance') THEN

    ALTER TABLE `ApplicationForms` ADD `IsAccepted` tinyint(1) NOT NULL DEFAULT FALSE;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423215956_AddApplicationAcceptance') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260423215956_AddApplicationAcceptance', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424190507_AddAdminAuditAndRaceUndo') THEN

    CREATE TABLE `AdminAuditLogs` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `CreatedAt` datetime(6) NOT NULL,
        `Actor` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `Action` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `EntityType` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `EntityId` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `Details` varchar(2048) CHARACTER SET utf8mb4 NOT NULL,
        CONSTRAINT `PK_AdminAuditLogs` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424190507_AddAdminAuditAndRaceUndo') THEN

    CREATE TABLE `RaceUndoEntries` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `CreatedAt` datetime(6) NOT NULL,
        `Actor` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `LeagueId` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `OriginalRaceId` int NOT NULL,
        `PayloadJson` LONGTEXT CHARACTER SET utf8mb4 NOT NULL,
        `IsUsed` tinyint(1) NOT NULL,
        `UsedAt` datetime(6) NULL,
        CONSTRAINT `PK_RaceUndoEntries` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424190507_AddAdminAuditAndRaceUndo') THEN

    CREATE INDEX `IX_AdminAuditLogs_CreatedAt` ON `AdminAuditLogs` (`CreatedAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424190507_AddAdminAuditAndRaceUndo') THEN

    CREATE INDEX `IX_AdminAuditLogs_EntityType_EntityId` ON `AdminAuditLogs` (`EntityType`, `EntityId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424190507_AddAdminAuditAndRaceUndo') THEN

    CREATE INDEX `IX_RaceUndoEntries_LeagueId_IsUsed_CreatedAt` ON `RaceUndoEntries` (`LeagueId`, `IsUsed`, `CreatedAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424190507_AddAdminAuditAndRaceUndo') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260424190507_AddAdminAuditAndRaceUndo', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424193709_AddLeagueArchiveState') THEN

    ALTER TABLE `Leagues` ADD `ArchivedAt` datetime(6) NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424193709_AddLeagueArchiveState') THEN

    ALTER TABLE `Leagues` ADD `ArchivedName` varchar(128) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424193709_AddLeagueArchiveState') THEN

    ALTER TABLE `Leagues` ADD `IsArchived` tinyint(1) NOT NULL DEFAULT FALSE;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424193709_AddLeagueArchiveState') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260424193709_AddLeagueArchiveState', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424195443_AddReserveDriverSupport') THEN

    ALTER TABLE `DriverStandings` ADD `IsReserveDriver` tinyint(1) NOT NULL DEFAULT FALSE;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424195443_AddReserveDriverSupport') THEN

    ALTER TABLE `DriverStandings` ADD `ReserveForDriver` varchar(128) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424195443_AddReserveDriverSupport') THEN

    ALTER TABLE `DriverStandings` ADD `ReservePointsForMain` int NOT NULL DEFAULT 0;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424195443_AddReserveDriverSupport') THEN

    ALTER TABLE `DriverStandings` ADD `ReserveStarts` int NOT NULL DEFAULT 0;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424195443_AddReserveDriverSupport') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260424195443_AddReserveDriverSupport', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260426034135_AddRaceReserveAssignments') THEN

    CREATE TABLE `RaceReserveAssignments` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `RaceResultId` int NOT NULL,
        `ReserveDriver` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `MainDriver` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        CONSTRAINT `PK_RaceReserveAssignments` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_RaceReserveAssignments_RaceResults_RaceResultId` FOREIGN KEY (`RaceResultId`) REFERENCES `RaceResults` (`RowId`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260426034135_AddRaceReserveAssignments') THEN

    CREATE INDEX `IX_RaceReserveAssignments_RaceResultId_MainDriver` ON `RaceReserveAssignments` (`RaceResultId`, `MainDriver`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260426034135_AddRaceReserveAssignments') THEN

    CREATE UNIQUE INDEX `IX_RaceReserveAssignments_RaceResultId_ReserveDriver` ON `RaceReserveAssignments` (`RaceResultId`, `ReserveDriver`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260426034135_AddRaceReserveAssignments') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260426034135_AddRaceReserveAssignments', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260428031730_AddTodoFeatures') THEN

    ALTER TABLE `RealLifeEvents` ADD `YouTubeUrl` varchar(512) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260428031730_AddTodoFeatures') THEN

    ALTER TABLE `DriverStandings` ADD `DriverNumber` int NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260428031730_AddTodoFeatures') THEN

    CREATE TABLE `LeaguePenalties` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `LeagueId` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `Date` datetime(6) NOT NULL,
        `Driver` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `Points` int NOT NULL,
        `Reason` varchar(1024) CHARACTER SET utf8mb4 NOT NULL,
        CONSTRAINT `PK_LeaguePenalties` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_LeaguePenalties_Leagues_LeagueId` FOREIGN KEY (`LeagueId`) REFERENCES `Leagues` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260428031730_AddTodoFeatures') THEN

    CREATE TABLE `StreamSchedules` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `StartAt` datetime(6) NOT NULL,
        `DurationMinutes` int NOT NULL,
        `Title` varchar(256) CHARACTER SET utf8mb4 NOT NULL,
        `Url` varchar(512) CHARACTER SET utf8mb4 NULL,
        `CreatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_StreamSchedules` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260428031730_AddTodoFeatures') THEN

    CREATE INDEX `IX_LeaguePenalties_LeagueId_Date` ON `LeaguePenalties` (`LeagueId`, `Date`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260428031730_AddTodoFeatures') THEN

    CREATE INDEX `IX_StreamSchedules_StartAt` ON `StreamSchedules` (`StartAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260428031730_AddTodoFeatures') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260428031730_AddTodoFeatures', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260430031510_AddTrackSetups') THEN

    ALTER TABLE `LeaguePenalties` DROP FOREIGN KEY `FK_LeaguePenalties_Leagues_LeagueId`;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260430031510_AddTrackSetups') THEN

    CREATE TABLE `TrackSetups` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `Track` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `Title` varchar(256) CHARACTER SET utf8mb4 NOT NULL,
        `SetupText` LONGTEXT CHARACTER SET utf8mb4 NOT NULL,
        `RequiredAccessTier` int NOT NULL,
        `RequiredRoleLabel` varchar(128) CHARACTER SET utf8mb4 NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_TrackSetups` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260430031510_AddTrackSetups') THEN

    CREATE INDEX `IX_TrackSetups_Track_RequiredAccessTier` ON `TrackSetups` (`Track`, `RequiredAccessTier`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260430031510_AddTrackSetups') THEN

    CREATE INDEX `IX_TrackSetups_UpdatedAt` ON `TrackSetups` (`UpdatedAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260430031510_AddTrackSetups') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260430031510_AddTrackSetups', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260430033910_AddSetupAccessRoleMappings') THEN

    CREATE TABLE `SetupAccessRoleMappings` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `Tier` int NOT NULL,
        `RoleId` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `Label` varchar(128) CHARACTER SET utf8mb4 NULL,
        `CreatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_SetupAccessRoleMappings` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260430033910_AddSetupAccessRoleMappings') THEN

    CREATE UNIQUE INDEX `IX_SetupAccessRoleMappings_RoleId` ON `SetupAccessRoleMappings` (`RoleId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260430033910_AddSetupAccessRoleMappings') THEN

    CREATE INDEX `IX_SetupAccessRoleMappings_Tier` ON `SetupAccessRoleMappings` (`Tier`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260430033910_AddSetupAccessRoleMappings') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260430033910_AddSetupAccessRoleMappings', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260502205247_AddTrackSetupInfoAndRemoveEngineBraking') THEN

    ALTER TABLE `TrackSetups` ADD `SetupInfo` varchar(1024) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260502205247_AddTrackSetupInfoAndRemoveEngineBraking') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260502205247_AddTrackSetupInfoAndRemoveEngineBraking', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260503235621_AddCustomAchievements') THEN

    CREATE TABLE `CustomAchievements` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `LeagueId` longtext CHARACTER SET utf8mb4 NULL,
        `Driver` longtext CHARACTER SET utf8mb4 NOT NULL,
        `Title` longtext CHARACTER SET utf8mb4 NOT NULL,
        `Description` longtext CHARACTER SET utf8mb4 NOT NULL,
        `Icon` longtext CHARACTER SET utf8mb4 NOT NULL,
        `Tone` longtext CHARACTER SET utf8mb4 NOT NULL,
        `Tier` longtext CHARACTER SET utf8mb4 NOT NULL,
        `Category` longtext CHARACTER SET utf8mb4 NOT NULL,
        `AwardedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_CustomAchievements` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260503235621_AddCustomAchievements') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260503235621_AddCustomAchievements', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN


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
                

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN


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
                

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN


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
                

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN


                    CREATE TABLE IF NOT EXISTS `AdminUserPermissions` (
                        `Id` int NOT NULL AUTO_INCREMENT,
                        `DiscordId` varchar(32) NOT NULL,
                        `Permission` varchar(64) NOT NULL,
                        PRIMARY KEY (`Id`),
                        CONSTRAINT `FK_AdminUserPermissions_AdminUsers_DiscordId`
                            FOREIGN KEY (`DiscordId`) REFERENCES `AdminUsers`(`DiscordId`) ON DELETE CASCADE
                    ) CHARACTER SET=utf8mb4;
                

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN


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
                

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN


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
                

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN


                    CREATE TABLE IF NOT EXISTS `CommunityVoteResponses` (
                        `Id` int NOT NULL AUTO_INCREMENT,
                        `PollId` int NOT NULL,
                        `OptionId` int NOT NULL,
                        `DiscordId` varchar(32) NOT NULL,
                        `DiscordName` varchar(128) NOT NULL,
                        `CreatedAt` datetime(6) NOT NULL,
                        PRIMARY KEY (`Id`)
                    ) CHARACTER SET=utf8mb4;
                

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN


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
                

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN


                    CREATE TABLE IF NOT EXISTS `ProfileWallMessages` (
                        `Id` int NOT NULL AUTO_INCREMENT,
                        `ProfileDiscordId` varchar(32) NOT NULL,
                        `AuthorDiscordId` varchar(32) NOT NULL,
                        `AuthorName` varchar(128) NOT NULL,
                        `Message` varchar(600) NOT NULL,
                        `CreatedAt` datetime(6) NOT NULL,
                        PRIMARY KEY (`Id`)
                    ) CHARACTER SET=utf8mb4;
                

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN


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
                

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN


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
                

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN


                    CREATE TABLE IF NOT EXISTS `SetupComments` (
                        `Id` int NOT NULL AUTO_INCREMENT,
                        `TrackSetupId` int NOT NULL,
                        `AuthorDiscordId` varchar(32) NOT NULL,
                        `AuthorName` varchar(128) NOT NULL,
                        `Message` varchar(600) NOT NULL,
                        `CreatedAt` datetime(6) NOT NULL,
                        PRIMARY KEY (`Id`)
                    ) CHARACTER SET=utf8mb4;
                

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN


                    CREATE TABLE IF NOT EXISTS `SetupLikes` (
                        `Id` int NOT NULL AUTO_INCREMENT,
                        `TrackSetupId` int NOT NULL,
                        `DiscordId` varchar(32) NOT NULL,
                        `DiscordName` varchar(128) NOT NULL,
                        `CreatedAt` datetime(6) NOT NULL,
                        PRIMARY KEY (`Id`)
                    ) CHARACTER SET=utf8mb4;
                

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN


                    CREATE TABLE IF NOT EXISTS `CommunityVoteOptions` (
                        `Id` int NOT NULL AUTO_INCREMENT,
                        `PollId` int NOT NULL,
                        `Label` varchar(160) NOT NULL,
                        PRIMARY KEY (`Id`),
                        CONSTRAINT `FK_CommunityVoteOptions_CommunityVotePolls_PollId`
                            FOREIGN KEY (`PollId`) REFERENCES `CommunityVotePolls`(`Id`) ON DELETE CASCADE
                    ) CHARACTER SET=utf8mb4;
                

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN


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
                

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN


                    CREATE UNIQUE INDEX IF NOT EXISTS `IX_AchievementDefinitions_Key`
                    ON `AchievementDefinitions` (`Key`);
                

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN

    CREATE UNIQUE INDEX IF NOT EXISTS `IX_AdminUserPermissions_DiscordId_Permission` ON `AdminUserPermissions` (`DiscordId`, `Permission`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN

    CREATE INDEX IF NOT EXISTS `IX_CommunityNewsPosts_IsPublished_IsPinned_PublishedAt` ON `CommunityNewsPosts` (`IsPublished`, `IsPinned`, `PublishedAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN

    CREATE INDEX IF NOT EXISTS `IX_CommunityVoteOptions_PollId` ON `CommunityVoteOptions` (`PollId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN

    CREATE UNIQUE INDEX IF NOT EXISTS `IX_CommunityVoteResponses_PollId_DiscordId` ON `CommunityVoteResponses` (`PollId`, `DiscordId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN

    CREATE UNIQUE INDEX IF NOT EXISTS `IX_DriverGamerTags_DiscordId_Platform` ON `DriverGamerTags` (`DiscordId`, `Platform`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN

    CREATE INDEX IF NOT EXISTS `IX_DriverGamerTags_GamerTag` ON `DriverGamerTags` (`GamerTag`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN

    CREATE INDEX IF NOT EXISTS `IX_ProfileWallMessages_ProfileDiscordId_CreatedAt` ON `ProfileWallMessages` (`ProfileDiscordId`, `CreatedAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN

    CREATE UNIQUE INDEX IF NOT EXISTS `IX_RaceAvailabilityEntries_EventId_DiscordId` ON `RaceAvailabilityEntries` (`EventId`, `DiscordId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN

    CREATE INDEX IF NOT EXISTS `IX_RaceAvailabilityEntries_EventId_Status` ON `RaceAvailabilityEntries` (`EventId`, `Status`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN

    CREATE INDEX IF NOT EXISTS `IX_RaceHighlightClips_IsApproved_CreatedAt` ON `RaceHighlightClips` (`IsApproved`, `CreatedAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN

    CREATE INDEX IF NOT EXISTS `IX_SetupComments_TrackSetupId_CreatedAt` ON `SetupComments` (`TrackSetupId`, `CreatedAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN

    CREATE UNIQUE INDEX IF NOT EXISTS `IX_SetupLikes_TrackSetupId_DiscordId` ON `SetupLikes` (`TrackSetupId`, `DiscordId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507023536_AddAdminPermissions') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260507023536_AddAdminPermissions', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507090257_AddLeaguePenaltyCreatedAt') THEN

    ALTER TABLE `LeaguePenalties` ADD `CreatedAt` datetime(6) NOT NULL DEFAULT UTC_TIMESTAMP(6);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507090257_AddLeaguePenaltyCreatedAt') THEN

    ALTER TABLE `LeaguePenalties` ADD `CreatedBy` varchar(128) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507090257_AddLeaguePenaltyCreatedAt') THEN

    ALTER TABLE `LeaguePenalties` ADD `Incident` varchar(2048) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507090257_AddLeaguePenaltyCreatedAt') THEN

    ALTER TABLE `LeaguePenalties` ADD `IsPublic` tinyint(1) NOT NULL DEFAULT FALSE;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507090257_AddLeaguePenaltyCreatedAt') THEN

    ALTER TABLE `LeaguePenalties` ADD `PenaltyType` varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT '';

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507090257_AddLeaguePenaltyCreatedAt') THEN

    ALTER TABLE `LeaguePenalties` ADD `RaceTrack` varchar(128) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507090257_AddLeaguePenaltyCreatedAt') THEN

    ALTER TABLE `LeaguePenalties` ADD `SecondDriver` varchar(128) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507090257_AddLeaguePenaltyCreatedAt') THEN

    CREATE INDEX `IX_LeaguePenalties_IsPublic_Date` ON `LeaguePenalties` (`IsPublic`, `Date`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507090257_AddLeaguePenaltyCreatedAt') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260507090257_AddLeaguePenaltyCreatedAt', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507125000_AddDiscordWebhooks') THEN

    CREATE TABLE `DiscordWebhooks` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `Name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
        `Url` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
        `Description` varchar(200) CHARACTER SET utf8mb4 NULL,
        `Category` varchar(64) CHARACTER SET utf8mb4 NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `LastUsedAt` datetime(6) NULL,
        `CreatedBy` varchar(100) CHARACTER SET utf8mb4 NULL,
        CONSTRAINT `PK_DiscordWebhooks` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507125000_AddDiscordWebhooks') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260507125000_AddDiscordWebhooks', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507133144_AddAboutMeProfiles') THEN

    CREATE TABLE `AboutMeProfiles` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `Slug` varchar(80) CHARACTER SET utf8mb4 NOT NULL,
        `DisplayName` varchar(120) CHARACTER SET utf8mb4 NOT NULL,
        `Tagline` varchar(200) CHARACTER SET utf8mb4 NULL,
        `AvatarUrl` varchar(500) CHARACTER SET utf8mb4 NULL,
        `BannerUrl` varchar(500) CHARACTER SET utf8mb4 NULL,
        `Bio` longtext CHARACTER SET utf8mb4 NULL,
        `ShortBio` varchar(300) CHARACTER SET utf8mb4 NULL,
        `DiscordUsername` varchar(200) CHARACTER SET utf8mb4 NULL,
        `TwitchUrl` varchar(200) CHARACTER SET utf8mb4 NULL,
        `YouTubeUrl` varchar(200) CHARACTER SET utf8mb4 NULL,
        `InstagramUrl` varchar(200) CHARACTER SET utf8mb4 NULL,
        `TwitterUrl` varchar(200) CHARACTER SET utf8mb4 NULL,
        `SteamUrl` varchar(200) CHARACTER SET utf8mb4 NULL,
        `FavoriteTrack` varchar(120) CHARACTER SET utf8mb4 NULL,
        `FavoriteCar` varchar(120) CHARACTER SET utf8mb4 NULL,
        `RacingNumber` varchar(10) CHARACTER SET utf8mb4 NULL,
        `AccentColor` varchar(7) CHARACTER SET utf8mb4 NULL,
        `IsPublic` tinyint(1) NOT NULL,
        `SortOrder` int NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        `LastEditedBy` varchar(100) CHARACTER SET utf8mb4 NULL,
        CONSTRAINT `PK_AboutMeProfiles` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260507133144_AddAboutMeProfiles') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260507133144_AddAboutMeProfiles', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260508045629_AddWebhookAutomationRules') THEN

    CREATE TABLE `WebhookAutomationRules` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `EventType` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `WebhookId` int NOT NULL,
        `IsEnabled` tinyint(1) NOT NULL,
        `ContentTemplate` varchar(2000) CHARACTER SET utf8mb4 NULL,
        `UsernameOverride` varchar(80) CHARACTER SET utf8mb4 NULL,
        `AvatarUrlOverride` varchar(512) CHARACTER SET utf8mb4 NULL,
        `UseEmbed` tinyint(1) NOT NULL,
        `EmbedTitleTemplate` varchar(256) CHARACTER SET utf8mb4 NULL,
        `EmbedDescriptionTemplate` varchar(4096) CHARACTER SET utf8mb4 NULL,
        `EmbedColor` varchar(9) CHARACTER SET utf8mb4 NULL,
        `EmbedFooterTemplate` varchar(512) CHARACTER SET utf8mb4 NULL,
        `EmbedThumbnailTemplate` varchar(512) CHARACTER SET utf8mb4 NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `CreatedBy` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        CONSTRAINT `PK_WebhookAutomationRules` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_WebhookAutomationRules_DiscordWebhooks_WebhookId` FOREIGN KEY (`WebhookId`) REFERENCES `DiscordWebhooks` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260508045629_AddWebhookAutomationRules') THEN

    CREATE INDEX `IX_WebhookAutomationRules_WebhookId` ON `WebhookAutomationRules` (`WebhookId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260508045629_AddWebhookAutomationRules') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260508045629_AddWebhookAutomationRules', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260508111829_ExpandAboutMeImageUrls') THEN

    ALTER TABLE `AboutMeProfiles` MODIFY COLUMN `BannerUrl` longtext CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260508111829_ExpandAboutMeImageUrls') THEN

    ALTER TABLE `AboutMeProfiles` MODIFY COLUMN `AvatarUrl` longtext CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260508111829_ExpandAboutMeImageUrls') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260508111829_ExpandAboutMeImageUrls', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260508115522_AddAboutMeBackgroundUrl') THEN

    ALTER TABLE `AboutMeProfiles` ADD `BackgroundUrl` longtext CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260508115522_AddAboutMeBackgroundUrl') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260508115522_AddAboutMeBackgroundUrl', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260508120002_AddAboutMeShowBanner') THEN

    ALTER TABLE `AboutMeProfiles` ADD `ShowBanner` tinyint(1) NOT NULL DEFAULT FALSE;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260508120002_AddAboutMeShowBanner') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260508120002_AddAboutMeShowBanner', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260513121904_AddRegelwerkDocAndDriverRoleHistory') THEN

    ALTER TABLE `ApplicationForms` ADD `AssignedRole` varchar(64) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260513121904_AddRegelwerkDocAndDriverRoleHistory') THEN

    ALTER TABLE `ApplicationForms` ADD `ReviewNote` varchar(1000) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260513121904_AddRegelwerkDocAndDriverRoleHistory') THEN

    ALTER TABLE `ApplicationForms` ADD `ReviewStatus` int NOT NULL DEFAULT 0;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260513121904_AddRegelwerkDocAndDriverRoleHistory') THEN

    CREATE TABLE `DriverRoleHistories` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `LeagueId` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `Driver` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `PreviousRole` varchar(64) CHARACTER SET utf8mb4 NULL,
        `NewRole` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `Reason` varchar(500) CHARACTER SET utf8mb4 NULL,
        `ChangedAt` datetime(6) NOT NULL,
        `ChangedBy` varchar(128) CHARACTER SET utf8mb4 NULL,
        CONSTRAINT `PK_DriverRoleHistories` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260513121904_AddRegelwerkDocAndDriverRoleHistory') THEN

    CREATE TABLE `RegelwerkDocuments` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `Title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
        `Version` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
        `Description` varchar(1000) CHARACTER SET utf8mb4 NULL,
        `FilePath` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
        `OriginalFileName` varchar(260) CHARACTER SET utf8mb4 NOT NULL,
        `ContentType` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
        `IsActive` tinyint(1) NOT NULL,
        `IsArchived` tinyint(1) NOT NULL,
        `UploadedAt` datetime(6) NOT NULL,
        `UploadedBy` varchar(128) CHARACTER SET utf8mb4 NULL,
        CONSTRAINT `PK_RegelwerkDocuments` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260513121904_AddRegelwerkDocAndDriverRoleHistory') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260513121904_AddRegelwerkDocAndDriverRoleHistory', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260513124748_SyncApplicationWorkflowAndRulebook') THEN

    CREATE INDEX `IX_RegelwerkDocuments_IsActive` ON `RegelwerkDocuments` (`IsActive`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260513124748_SyncApplicationWorkflowAndRulebook') THEN

    CREATE INDEX `IX_DriverRoleHistories_LeagueId_Driver` ON `DriverRoleHistories` (`LeagueId`, `Driver`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260513124748_SyncApplicationWorkflowAndRulebook') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260513124748_SyncApplicationWorkflowAndRulebook', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260513134854_AddPerformanceIndexes') THEN

    CREATE INDEX `IX_UpcomingEvents_Date` ON `UpcomingEvents` (`Date`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260513134854_AddPerformanceIndexes') THEN

    CREATE INDEX `IX_UpcomingEvents_LeagueId_Date` ON `UpcomingEvents` (`LeagueId`, `Date`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260513134854_AddPerformanceIndexes') THEN

    CREATE INDEX `IX_RaceResults_Date` ON `RaceResults` (`Date`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260513134854_AddPerformanceIndexes') THEN

    CREATE INDEX `IX_RaceResults_LeagueId_Date` ON `RaceResults` (`LeagueId`, `Date`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260513134854_AddPerformanceIndexes') THEN

    CREATE INDEX `IX_RaceFinishes_Driver` ON `RaceFinishes` (`Driver`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260513134854_AddPerformanceIndexes') THEN

    CREATE INDEX `IX_DriverStandings_Driver` ON `DriverStandings` (`Driver`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260513134854_AddPerformanceIndexes') THEN

    CREATE INDEX `IX_DriverStandings_LeagueId_Driver` ON `DriverStandings` (`LeagueId`, `Driver`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260513134854_AddPerformanceIndexes') THEN

    CREATE INDEX `IX_DriverProfiles_DisplayName` ON `DriverProfiles` (`DisplayName`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260513134854_AddPerformanceIndexes') THEN

    ALTER TABLE `UpcomingEvents` DROP INDEX `IX_UpcomingEvents_LeagueId`;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260513134854_AddPerformanceIndexes') THEN

    ALTER TABLE `RaceResults` DROP INDEX `IX_RaceResults_LeagueId`;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260513134854_AddPerformanceIndexes') THEN

    ALTER TABLE `DriverStandings` DROP INDEX `IX_DriverStandings_LeagueId`;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260513134854_AddPerformanceIndexes') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260513134854_AddPerformanceIndexes', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260522024951_AddRaceCalendarSettings') THEN

    CREATE TABLE `RaceCalendarSettings` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `BackgroundImageFileName` longtext CHARACTER SET utf8mb4 NULL,
        `SeasonTitle` longtext CHARACTER SET utf8mb4 NULL,
        `SeasonSubtitle` longtext CHARACTER SET utf8mb4 NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_RaceCalendarSettings` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260522024951_AddRaceCalendarSettings') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260522024951_AddRaceCalendarSettings', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260522150723_AddRaceWeekendsDropUpcomingEvents') THEN

    DROP TABLE `UpcomingEvents`;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260522150723_AddRaceWeekendsDropUpcomingEvents') THEN

    CREATE TABLE `RaceWeekends` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `Order` int NOT NULL,
        `Track` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `DistancePercent` int NOT NULL DEFAULT 100,
        CONSTRAINT `PK_RaceWeekends` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260522150723_AddRaceWeekendsDropUpcomingEvents') THEN

    CREATE TABLE `RaceWeekendLegs` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `RaceWeekendId` int NOT NULL,
        `LeagueId` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `Date` datetime(6) NOT NULL,
        CONSTRAINT `PK_RaceWeekendLegs` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_RaceWeekendLegs_RaceWeekends_RaceWeekendId` FOREIGN KEY (`RaceWeekendId`) REFERENCES `RaceWeekends` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260522150723_AddRaceWeekendsDropUpcomingEvents') THEN

    CREATE INDEX `IX_RaceWeekendLegs_Date` ON `RaceWeekendLegs` (`Date`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260522150723_AddRaceWeekendsDropUpcomingEvents') THEN

    CREATE INDEX `IX_RaceWeekendLegs_LeagueId_Date` ON `RaceWeekendLegs` (`LeagueId`, `Date`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260522150723_AddRaceWeekendsDropUpcomingEvents') THEN

    CREATE UNIQUE INDEX `IX_RaceWeekendLegs_RaceWeekendId_LeagueId` ON `RaceWeekendLegs` (`RaceWeekendId`, `LeagueId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260522150723_AddRaceWeekendsDropUpcomingEvents') THEN

    CREATE INDEX `IX_RaceWeekends_Order` ON `RaceWeekends` (`Order`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260522150723_AddRaceWeekendsDropUpcomingEvents') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260522150723_AddRaceWeekendsDropUpcomingEvents', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260526031353_AddWebhookRuleStatus') THEN

    ALTER TABLE `WebhookAutomationRules` ADD `FailureCount` int NOT NULL DEFAULT 0;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260526031353_AddWebhookRuleStatus') THEN

    ALTER TABLE `WebhookAutomationRules` ADD `LastError` varchar(500) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260526031353_AddWebhookRuleStatus') THEN

    ALTER TABLE `WebhookAutomationRules` ADD `LastRunAt` datetime(6) NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260526031353_AddWebhookRuleStatus') THEN

    ALTER TABLE `WebhookAutomationRules` ADD `LastStatus` varchar(16) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260526031353_AddWebhookRuleStatus') THEN

    ALTER TABLE `WebhookAutomationRules` ADD `SuccessCount` int NOT NULL DEFAULT 0;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260526031353_AddWebhookRuleStatus') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260526031353_AddWebhookRuleStatus', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260529220930_AddApplicationFormRowVersion') THEN

    ALTER TABLE `DriverProfiles` ADD `Age` int NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260529220930_AddApplicationFormRowVersion') THEN

    ALTER TABLE `DriverProfiles` ADD `PhotoUrl` varchar(512) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260529220930_AddApplicationFormRowVersion') THEN

    ALTER TABLE `ApplicationForms` MODIFY COLUMN `Role` varchar(64) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260529220930_AddApplicationFormRowVersion') THEN

    ALTER TABLE `ApplicationForms` MODIFY COLUMN `Platform` varchar(64) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260529220930_AddApplicationFormRowVersion') THEN

    ALTER TABLE `ApplicationForms` MODIFY COLUMN `Division` varchar(128) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260529220930_AddApplicationFormRowVersion') THEN

    ALTER TABLE `ApplicationForms` ADD `RowVersion` timestamp(6) NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260529220930_AddApplicationFormRowVersion') THEN

    CREATE INDEX `IX_ApplicationForms_SubmittedAt` ON `ApplicationForms` (`SubmittedAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260529220930_AddApplicationFormRowVersion') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260529220930_AddApplicationFormRowVersion', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260601191403_AddTrackSetupGameYear') THEN

    ALTER TABLE `TrackSetups` DROP INDEX `IX_TrackSetups_Track_RequiredAccessTier`;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260601191403_AddTrackSetupGameYear') THEN

    ALTER TABLE `TrackSetups` ADD `GameYear` varchar(16) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260601191403_AddTrackSetupGameYear') THEN

    CREATE INDEX `IX_TrackSetups_Track_GameYear_RequiredAccessTier` ON `TrackSetups` (`Track`, `GameYear`, `RequiredAccessTier`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260601191403_AddTrackSetupGameYear') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260601191403_AddTrackSetupGameYear', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260603000902_AddIsRejected') THEN

    ALTER TABLE `ApplicationForms` DROP COLUMN `ReviewStatus`;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260603000902_AddIsRejected') THEN

    ALTER TABLE `ApplicationForms` ADD `IsRejected` tinyint(1) NOT NULL DEFAULT FALSE;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260603000902_AddIsRejected') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260603000902_AddIsRejected', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260607113255_AddExclusiveSetupConsent') THEN

    ALTER TABLE `ApplicationForms` DROP COLUMN `IsRejected`;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260607113255_AddExclusiveSetupConsent') THEN

    ALTER TABLE `Leagues` MODIFY COLUMN `Description` varchar(1024) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260607113255_AddExclusiveSetupConsent') THEN

    ALTER TABLE `DriverProfiles` ADD `ExclusiveSetupTermsAcceptedAt` datetime(6) NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260607113255_AddExclusiveSetupConsent') THEN

    ALTER TABLE `DriverProfiles` ADD `HasAcceptedExclusiveSetupTerms` tinyint(1) NOT NULL DEFAULT FALSE;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260607113255_AddExclusiveSetupConsent') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260607113255_AddExclusiveSetupConsent', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260611100407_LinkApplicationsToLeagues') THEN

    ALTER TABLE `Leagues` ADD `ApplicationInfo` varchar(256) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260611100407_LinkApplicationsToLeagues') THEN

    ALTER TABLE `Leagues` ADD `IsOpenForApplications` tinyint(1) NOT NULL DEFAULT FALSE;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260611100407_LinkApplicationsToLeagues') THEN

    ALTER TABLE `ApplicationForms` ADD `AppliedLeagueId` varchar(64) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260611100407_LinkApplicationsToLeagues') THEN

    CREATE INDEX `IX_ApplicationForms_AppliedLeagueId` ON `ApplicationForms` (`AppliedLeagueId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260611100407_LinkApplicationsToLeagues') THEN

    ALTER TABLE `ApplicationForms` ADD CONSTRAINT `FK_ApplicationForms_Leagues_AppliedLeagueId` FOREIGN KEY (`AppliedLeagueId`) REFERENCES `Leagues` (`Id`) ON DELETE SET NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260611100407_LinkApplicationsToLeagues') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260611100407_LinkApplicationsToLeagues', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260611103557_OpenSeedLeagueForApplications') THEN

    UPDATE `Leagues` SET `IsOpenForApplications` = 1,     `ApplicationInfo` = 'Freitags 20:00 · KI bis 105' WHERE `Id` = 'pro' AND `Name` = 'ERC Pro League';

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260611103557_OpenSeedLeagueForApplications') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260611103557_OpenSeedLeagueForApplications', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260612170307_AddFavoriteTeam') THEN

    ALTER TABLE `DriverProfiles` ADD `FavoriteTeam` varchar(64) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260612170307_AddFavoriteTeam') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260612170307_AddFavoriteTeam', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260613185113_AddApplicationRejection') THEN

    ALTER TABLE `ApplicationForms` ADD `IsRejected` tinyint(1) NOT NULL DEFAULT FALSE;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260613185113_AddApplicationRejection') THEN

    ALTER TABLE `ApplicationForms` ADD `RejectedAt` datetime(6) NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260613185113_AddApplicationRejection') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260613185113_AddApplicationRejection', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260615102908_AddTrollAdminTables') THEN

    CREATE TABLE `TrollCustomGags` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `Key` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `Kind` int NOT NULL,
        `Category` int NOT NULL,
        `Eyebrow` varchar(80) CHARACTER SET utf8mb4 NULL,
        `Title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
        `Lead` varchar(400) CHARACTER SET utf8mb4 NULL,
        `Body` TEXT CHARACTER SET utf8mb4 NULL,
        `Question` varchar(400) CHARACTER SET utf8mb4 NULL,
        `Answer` varchar(200) CHARACTER SET utf8mb4 NULL,
        `OptionsJson` TEXT CHARACTER SET utf8mb4 NULL,
        `IsEnabled` tinyint(1) NOT NULL,
        `Weight` int NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `CreatedBy` varchar(128) CHARACTER SET utf8mb4 NULL,
        CONSTRAINT `PK_TrollCustomGags` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260615102908_AddTrollAdminTables') THEN

    CREATE TABLE `TrollGagOverrides` (
        `Key` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `IsEnabled` tinyint(1) NOT NULL,
        `Weight` int NOT NULL,
        CONSTRAINT `PK_TrollGagOverrides` PRIMARY KEY (`Key`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260615102908_AddTrollAdminTables') THEN

    CREATE TABLE `TrollSettings` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `Enabled` tinyint(1) NOT NULL,
        `TriggerChance` double NOT NULL,
        `CooldownMinutes` int NOT NULL,
        `ApplyToAdmins` tinyint(1) NOT NULL,
        `MercyAfterAttempts` int NOT NULL,
        `MathMaxOperand` int NOT NULL,
        CONSTRAINT `PK_TrollSettings` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260615102908_AddTrollAdminTables') THEN

    CREATE UNIQUE INDEX `IX_TrollCustomGags_Key` ON `TrollCustomGags` (`Key`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260615102908_AddTrollAdminTables') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260615102908_AddTrollAdminTables', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260616103716_AddManualSetupTier') THEN

    ALTER TABLE `DriverProfiles` ADD `ManualSetupTier` int NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260616103716_AddManualSetupTier') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260616103716_AddManualSetupTier', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260617174935_AddApplicationFlagAndDedupGuard') THEN

    ALTER TABLE `ApplicationForms` ADD `FlaggedAt` datetime(6) NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260617174935_AddApplicationFlagAndDedupGuard') THEN

    ALTER TABLE `ApplicationForms` ADD `IsFlagged` tinyint(1) NOT NULL DEFAULT FALSE;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260617174935_AddApplicationFlagAndDedupGuard') THEN


                    UPDATE `ApplicationForms` AS af
                    JOIN (
                        SELECT `Id`,
                               ROW_NUMBER() OVER (
                                   PARTITION BY `DiscordId`
                                   ORDER BY `IsAccepted` DESC, `SubmittedAt` DESC, `Id` DESC
                               ) AS rn
                        FROM `ApplicationForms`
                        WHERE `IsRejected` = 0 AND `DiscordId` IS NOT NULL AND `DiscordId` <> ''
                    ) AS ranked ON af.`Id` = ranked.`Id`
                    SET af.`IsRejected` = 1,
                        af.`RejectedAt` = UTC_TIMESTAMP(),
                        af.`ReviewNote` = CONCAT(COALESCE(af.`ReviewNote`, ''), '\n[Auto] Duplikat automatisch abgelehnt (Dedup-Migration).')
                    WHERE ranked.rn > 1;
                

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260617174935_AddApplicationFlagAndDedupGuard') THEN

    ALTER TABLE `ApplicationForms` ADD `ActiveDiscordKey` varchar(32) CHARACTER SET utf8mb4 AS ((CASE WHEN `IsRejected` = 0 THEN `DiscordId` ELSE NULL END)) STORED NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260617174935_AddApplicationFlagAndDedupGuard') THEN

    CREATE UNIQUE INDEX `IX_ApplicationForms_ActiveDiscordKey` ON `ApplicationForms` (`ActiveDiscordKey`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260617174935_AddApplicationFlagAndDedupGuard') THEN

    CREATE INDEX `IX_ApplicationForms_DiscordId` ON `ApplicationForms` (`DiscordId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260617174935_AddApplicationFlagAndDedupGuard') THEN

    CREATE INDEX `IX_ApplicationForms_IsAccepted_IsRejected` ON `ApplicationForms` (`IsAccepted`, `IsRejected`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260617174935_AddApplicationFlagAndDedupGuard') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260617174935_AddApplicationFlagAndDedupGuard', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

COMMIT;

