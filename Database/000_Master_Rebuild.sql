-- ============================================
-- 000_Master_Rebuild.sql
-- Master script to rebuild AScheduler DB from scratch.
--
-- IMPORTANT:
-- Run with SQLCMD mode enabled (SSMS: Query -> SQLCMD Mode)
-- or via sqlcmd CLI so :r directives are processed.
--
-- This script is destructive and intended for reset scenarios.

-- Usage:
-- Push-Location .\Database
-- sqlcmd -S "(localdb)\MSSQLLocalDB" -d ASchedulerDB -E -b -i ".\000_Master_Rebuild.sql"
-- Pop-Location
-- ============================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    :r .\001_Reset_Database.sql
    :r .\010_Create_Security.sql
    :r .\020_Create_Departments_And_Boxes.sql
    :r .\030_Create_Tasks_And_Dependencies.sql
    :r .\040_Create_Executions_And_Logs.sql
    :r .\050_Create_Audit_And_View.sql
    :r .\060_Create_Notification_Settings.sql
    :r .\070_Create_Application_Logs.sql
    :r .\080_Optimize_Execution_Queries.sql
    :r .\090_Validate_Integrity_And_Continuity.sql

    IF NOT EXISTS (SELECT 1 FROM dbo.Departments WHERE Name = 'Default')
        THROW 51000, 'Validation failed: Default department was not created.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.Users u
        INNER JOIN dbo.Roles r ON r.RoleId = u.RoleId
        WHERE u.Username = 'admin'
          AND r.RoleName = 'Admin'
    )
        THROW 51001, 'Validation failed: default admin user was not created.', 1;

    COMMIT TRANSACTION;
    PRINT 'AScheduler rebuild completed successfully.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorNumber INT = ERROR_NUMBER();
    DECLARE @ErrorLine INT = ERROR_LINE();

    RAISERROR('Master rebuild failed. Error %d at line %d: %s', 16, 1, @ErrorNumber, @ErrorLine, @ErrorMessage);
END CATCH;
