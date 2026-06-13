-- Add IsRejected column to ApplicationForms to match model
ALTER TABLE `ApplicationForms`
ADD COLUMN `IsRejected` tinyint(1) NOT NULL DEFAULT 0;

-- To apply: execute this SQL against your MySQL database used by the app.
-- Example (PowerShell):
-- mysql -u <user> -p -h <host> <database> < <OWNER_HANDLE>-ERC/Migrations/Scripts/20260603_AddIsRejected.sql

-- Or create and run an EF Core migration:
-- dotnet ef migrations add AddIsRejected
-- dotnet ef database update
