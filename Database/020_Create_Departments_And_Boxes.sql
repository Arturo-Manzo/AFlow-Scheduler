-- ============================================
-- 020_Create_Departments_And_Boxes.sql
-- Department governance + scheduler containers.
-- ============================================

CREATE TABLE dbo.Departments
(
    DepartmentId INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(500) NULL,
    ContactEmail NVARCHAR(255) NOT NULL,
    RetryPolicy INT NOT NULL CONSTRAINT DF_Departments_RetryPolicy DEFAULT 0,
    LogRetentionDays INT NOT NULL CONSTRAINT DF_Departments_LogRetentionDays DEFAULT 90,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Departments_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT CK_Departments_RetryPolicy CHECK (RetryPolicy IN (0, 1, 2)),
    CONSTRAINT CK_Departments_LogRetentionDays CHECK (LogRetentionDays > 0)
);

CREATE INDEX IX_Departments_Name ON dbo.Departments(Name);

ALTER TABLE dbo.Users
ADD CONSTRAINT FK_Users_Departments
    FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments(DepartmentId);

CREATE INDEX IX_Users_DepartmentId ON dbo.Users(DepartmentId);

CREATE TABLE dbo.Boxes
(
    BoxId INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NULL,
    CronExpression NVARCHAR(100) NOT NULL,
    TimeZoneId NVARCHAR(100) NOT NULL,
    AllowParallel BIT NOT NULL CONSTRAINT DF_Boxes_AllowParallel DEFAULT 0,
    Enabled BIT NOT NULL CONSTRAINT DF_Boxes_Enabled DEFAULT 1,
    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_Boxes_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    LastRunUtc DATETIME2 NULL,
    NotificationEmail NVARCHAR(256) NULL,
    DepartmentId INT NULL,
    CONSTRAINT FK_Boxes_Departments FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments(DepartmentId)
);

CREATE UNIQUE INDEX IX_Boxes_Name ON dbo.Boxes(Name);
CREATE INDEX IX_Boxes_Enabled ON dbo.Boxes(Enabled);
CREATE INDEX IX_Boxes_DepartmentId ON dbo.Boxes(DepartmentId);

CREATE TABLE dbo.BoxRuns
(
    BoxRunId INT IDENTITY(1,1) PRIMARY KEY,
    BoxId INT NOT NULL,
    ScheduledForUtc DATETIME2 NULL,
    StartedAtUtc DATETIME2 NULL,
    EndedAtUtc DATETIME2 NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_BoxRuns_Status DEFAULT 'Pending',
    IsCancelled BIT NOT NULL CONSTRAINT DF_BoxRuns_IsCancelled DEFAULT 0,
    TriggerSource NVARCHAR(20) NOT NULL CONSTRAINT DF_BoxRuns_TriggerSource DEFAULT 'Scheduler',
    RequestedByUserId INT NULL,
    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_BoxRuns_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_BoxRuns_Boxes FOREIGN KEY (BoxId) REFERENCES dbo.Boxes(BoxId) ON DELETE CASCADE,
    CONSTRAINT FK_BoxRuns_Users FOREIGN KEY (RequestedByUserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT CK_BoxRuns_Status CHECK (Status IN ('Pending', 'Running', 'Completed', 'Partial', 'Failed', 'Cancelled')),
    CONSTRAINT CK_BoxRuns_TriggerSource CHECK (TriggerSource IN ('Scheduler', 'Manual', 'Retry', 'ForceStart', 'Scheduled'))
);

CREATE INDEX IX_BoxRuns_BoxId_Scheduled ON dbo.BoxRuns(BoxId, ScheduledForUtc DESC);
CREATE INDEX IX_BoxRuns_BoxId_Status ON dbo.BoxRuns(BoxId, Status);
CREATE INDEX IX_BoxRuns_Status ON dbo.BoxRuns(Status);

CREATE TABLE dbo.BoxExecutionQueue
(
    QueueId INT IDENTITY(1,1) PRIMARY KEY,
    BoxId INT NOT NULL,
    RequestedByUserId INT NULL,
    IgnoreDependencies BIT NOT NULL CONSTRAINT DF_BoxExecutionQueue_IgnoreDependencies DEFAULT 0,
    IgnoreSchedule BIT NOT NULL CONSTRAINT DF_BoxExecutionQueue_IgnoreSchedule DEFAULT 0,
    Reason NVARCHAR(255) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_BoxExecutionQueue_Status DEFAULT 'Pending',
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_BoxExecutionQueue_CreatedAt DEFAULT SYSUTCDATETIME(),
    ProcessedAt DATETIME2 NULL,
    CONSTRAINT FK_BoxExecutionQueue_Boxes FOREIGN KEY (BoxId) REFERENCES dbo.Boxes(BoxId),
    CONSTRAINT FK_BoxExecutionQueue_Users FOREIGN KEY (RequestedByUserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT CK_BoxExecutionQueue_Status CHECK (Status IN ('Pending', 'Running', 'Completed', 'Failed', 'Cancelled'))
);

CREATE INDEX IX_BoxExecutionQueue_BoxId ON dbo.BoxExecutionQueue(BoxId);
CREATE INDEX IX_BoxExecutionQueue_Status ON dbo.BoxExecutionQueue(Status);
CREATE INDEX IX_BoxExecutionQueue_Created ON dbo.BoxExecutionQueue(CreatedAt);

IF NOT EXISTS (SELECT 1 FROM dbo.Departments WHERE Name = 'Default')
BEGIN
        INSERT INTO dbo.Departments (Name, Description, ContactEmail, RetryPolicy, LogRetentionDays)
        VALUES ('Default', 'Default department for legacy and non-assigned records', 'owner@placeholder.local', 0, 90);
END;

UPDATE u
SET u.DepartmentId = d.DepartmentId
FROM dbo.Users u
INNER JOIN dbo.Departments d ON d.Name = 'Default'
INNER JOIN dbo.Roles r ON r.RoleId = u.RoleId
WHERE r.RoleName = 'Admin'
    AND u.Username = 'admin'
    AND u.DepartmentId IS NULL;

PRINT 'Departments and boxes model created.';
