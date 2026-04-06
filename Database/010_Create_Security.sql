-- ============================================
-- 010_Create_Security.sql
-- Roles and Users security model.
-- ============================================

CREATE TABLE dbo.Roles
(
    RoleId INT IDENTITY(1,1) PRIMARY KEY,
    RoleName NVARCHAR(50) NOT NULL UNIQUE,
    Description NVARCHAR(200) NULL,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Roles_CreatedAt DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.Users
(
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL UNIQUE,
    Email NVARCHAR(100) NULL,
    PasswordHash NVARCHAR(256) NOT NULL,
    RoleId INT NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1,
    LastLoginAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NULL,
    DepartmentId INT NULL,
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId)
);

CREATE INDEX IX_Users_Username ON dbo.Users(Username);
CREATE INDEX IX_Users_RoleId ON dbo.Users(RoleId);

INSERT INTO dbo.Roles (RoleName, Description)
VALUES
    ('Admin', 'Full access to all features'),
    ('Operator', 'Can create and execute tasks'),
    ('Viewer', 'Read-only access to tasks and logs');

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = 'admin')
BEGIN
    INSERT INTO dbo.Users
    (
        Username,
        Email,
        PasswordHash,
        RoleId,
        IsActive,
        CreatedAt,
        UpdatedAt
    )
    VALUES
    (
        'admin',
        'admin@CHRONIQ.local',
        'AAAAAAAAAAAAAAAAAAAAAAqRy/h50tlEUsVcJQ477QaLEbsT',
        (SELECT RoleId FROM dbo.Roles WHERE RoleName = 'Admin'),
        1,
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    );
END;

PRINT 'Security tables created.';
