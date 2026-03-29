-- ============================================
-- Users and Roles Tables for AScheduler
-- ============================================

-- Drop in dependency order to allow full reset from any previous state.
IF OBJECT_ID('dbo.vw_TaskExecutionHistory', 'V') IS NOT NULL DROP VIEW dbo.vw_TaskExecutionHistory;

IF OBJECT_ID('dbo.TaskExecutions', 'U') IS NOT NULL DROP TABLE dbo.TaskExecutions;
IF OBJECT_ID('dbo.TaskDependencies', 'U') IS NOT NULL DROP TABLE dbo.TaskDependencies;
IF OBJECT_ID('dbo.Tasks', 'U') IS NOT NULL DROP TABLE dbo.Tasks;
IF OBJECT_ID('dbo.BoxExecutionQueue', 'U') IS NOT NULL DROP TABLE dbo.BoxExecutionQueue;
IF OBJECT_ID('dbo.BoxRuns', 'U') IS NOT NULL DROP TABLE dbo.BoxRuns;
IF OBJECT_ID('dbo.AuditLog', 'U') IS NOT NULL DROP TABLE dbo.AuditLog;
IF OBJECT_ID('dbo.Boxes', 'U') IS NOT NULL DROP TABLE dbo.Boxes;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
IF OBJECT_ID('dbo.Roles', 'U') IS NOT NULL DROP TABLE dbo.Roles;

-- Create Roles table
CREATE TABLE Roles (
    RoleId INT IDENTITY(1,1) PRIMARY KEY,
    RoleName NVARCHAR(50) NOT NULL UNIQUE,
    Description NVARCHAR(200),
    CreatedAt DATETIME DEFAULT GETDATE()
);

-- Insert default roles
INSERT INTO Roles (RoleName, Description) VALUES
    ('Admin', 'Full access to all features'),
    ('Operator', 'Can create and execute tasks'),
    ('Viewer', 'Read-only access to tasks and logs');

PRINT 'Roles table recreated successfully';

-- Create Users table
CREATE TABLE Users (
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL UNIQUE,
    Email NVARCHAR(100),
    PasswordHash NVARCHAR(256) NOT NULL,
    RoleId INT NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    LastLoginAt DATETIME NULL,
    CreatedAt DATETIME DEFAULT GETDATE(),
    UpdatedAt DATETIME NULL,
    FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
);

CREATE INDEX IX_Users_Username ON Users(Username);
CREATE INDEX IX_Users_RoleId ON Users(RoleId);

PRINT 'Users table recreated successfully';
