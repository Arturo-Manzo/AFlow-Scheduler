-- ============================================
-- 001_Reset_Database.sql
-- Full reset for AScheduler schema alignment.
-- WARNING: This script drops existing data.
-- ============================================

-- Drop all foreign keys first to avoid dependency errors from legacy schemas.
DECLARE @DropFkSql NVARCHAR(MAX) = N'';

SELECT @DropFkSql = @DropFkSql +
	N'ALTER TABLE [' + SCHEMA_NAME(t.schema_id) + N'].[' + t.name + N'] DROP CONSTRAINT [' + fk.name + N'];' + CHAR(10)
FROM sys.foreign_keys fk
INNER JOIN sys.tables t ON fk.parent_object_id = t.object_id;

IF LEN(@DropFkSql) > 0
	EXEC sp_executesql @DropFkSql;

-- Drop views first
IF OBJECT_ID('dbo.vw_TaskExecutionHistory', 'V') IS NOT NULL DROP VIEW dbo.vw_TaskExecutionHistory;

-- Drop foreign-key dependent tables first
IF OBJECT_ID('dbo.ApplicationLogs', 'U') IS NOT NULL DROP TABLE dbo.ApplicationLogs;
IF OBJECT_ID('dbo.NotificationSmtpSettings', 'U') IS NOT NULL DROP TABLE dbo.NotificationSmtpSettings;
IF OBJECT_ID('dbo.TaskExecutionLogs', 'U') IS NOT NULL DROP TABLE dbo.TaskExecutionLogs;
IF OBJECT_ID('dbo.TaskExecutions', 'U') IS NOT NULL DROP TABLE dbo.TaskExecutions;
IF OBJECT_ID('dbo.TaskDependencies', 'U') IS NOT NULL DROP TABLE dbo.TaskDependencies;
IF OBJECT_ID('dbo.Tasks', 'U') IS NOT NULL DROP TABLE dbo.Tasks;
IF OBJECT_ID('dbo.BoxExecutionQueue', 'U') IS NOT NULL DROP TABLE dbo.BoxExecutionQueue;
IF OBJECT_ID('dbo.BoxRuns', 'U') IS NOT NULL DROP TABLE dbo.BoxRuns;
IF OBJECT_ID('dbo.AuditLog', 'U') IS NOT NULL DROP TABLE dbo.AuditLog;

-- Legacy optional objects from previous versions
IF OBJECT_ID('dbo.DepartmentPolicies', 'U') IS NOT NULL DROP TABLE dbo.DepartmentPolicies;
IF OBJECT_ID('dbo.DepartmentBoxAccess', 'U') IS NOT NULL DROP TABLE dbo.DepartmentBoxAccess;

-- Core tables
IF OBJECT_ID('dbo.Boxes', 'U') IS NOT NULL DROP TABLE dbo.Boxes;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
IF OBJECT_ID('dbo.Departments', 'U') IS NOT NULL DROP TABLE dbo.Departments;
IF OBJECT_ID('dbo.Roles', 'U') IS NOT NULL DROP TABLE dbo.Roles;

PRINT 'Database reset complete.';
