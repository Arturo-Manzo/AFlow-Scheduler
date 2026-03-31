-- ============================================
-- Boxes, BoxRuns and BoxExecutionQueue
-- Boxes are scheduling containers that own the CronExpression.
-- Each Box contains one or more Task steps.
-- BoxRuns represent concrete execution instances of a Box.
-- ============================================

-- Drop in dependency order.
IF OBJECT_ID('dbo.TaskExecutions', 'U') IS NOT NULL DROP TABLE dbo.TaskExecutions;
IF OBJECT_ID('dbo.TaskDependencies', 'U') IS NOT NULL DROP TABLE dbo.TaskDependencies;
IF OBJECT_ID('dbo.Tasks', 'U') IS NOT NULL DROP TABLE dbo.Tasks;
IF OBJECT_ID('dbo.BoxExecutionQueue', 'U') IS NOT NULL DROP TABLE dbo.BoxExecutionQueue;
IF OBJECT_ID('dbo.BoxRuns', 'U') IS NOT NULL DROP TABLE dbo.BoxRuns;
IF OBJECT_ID('dbo.Boxes', 'U') IS NOT NULL DROP TABLE dbo.Boxes;

-- Create Boxes table
CREATE TABLE Boxes (
    BoxId           INT IDENTITY(1,1) PRIMARY KEY,
    Name            NVARCHAR(100) NOT NULL,
    Description     NVARCHAR(500) NULL,
    CronExpression  NVARCHAR(100) NOT NULL,
    TimeZoneId      NVARCHAR(100) NOT NULL,
    AllowParallel   BIT NOT NULL DEFAULT 0,
    Enabled         BIT NOT NULL DEFAULT 1,
    CreatedAtUtc    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    LastRunUtc      DATETIME2 NULL
);
CREATE UNIQUE INDEX IX_Boxes_Name    ON Boxes(Name);
CREATE        INDEX IX_Boxes_Enabled ON Boxes(Enabled);
PRINT 'Boxes table recreated successfully';

-- Create BoxRuns table (one per cron occurrence or manual trigger)
CREATE TABLE BoxRuns (
    BoxRunId          INT IDENTITY(1,1) PRIMARY KEY,
    BoxId             INT NOT NULL,
    ScheduledForUtc   DATETIME2 NULL,
    StartedAtUtc      DATETIME2 NULL,
    EndedAtUtc        DATETIME2 NULL,
    Status            NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    TriggerSource     NVARCHAR(20) NOT NULL DEFAULT 'Scheduled',
    RequestedByUserId INT NULL,
    CreatedAtUtc      DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_BoxRuns_Boxes FOREIGN KEY (BoxId) REFERENCES Boxes(BoxId) ON DELETE CASCADE,
    CONSTRAINT FK_BoxRuns_Users FOREIGN KEY (RequestedByUserId) REFERENCES Users(UserId)
);
CREATE INDEX IX_BoxRuns_BoxId_Scheduled ON BoxRuns(BoxId, ScheduledForUtc DESC);
CREATE INDEX IX_BoxRuns_BoxId_Status    ON BoxRuns(BoxId, Status);
CREATE INDEX IX_BoxRuns_Status          ON BoxRuns(Status);
PRINT 'BoxRuns table recreated successfully';

-- Create BoxExecutionQueue for manual run-now audit trail
CREATE TABLE BoxExecutionQueue (
    QueueId            INT IDENTITY(1,1) PRIMARY KEY,
    BoxId              INT NOT NULL,
    RequestedByUserId  INT NULL,
    IgnoreDependencies BIT NOT NULL DEFAULT 0,
    IgnoreSchedule     BIT NOT NULL DEFAULT 0,
    Reason             NVARCHAR(255) NULL,
    Status             NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    CreatedAt          DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ProcessedAt        DATETIME2 NULL,
    CONSTRAINT FK_BoxExecutionQueue_Boxes FOREIGN KEY (BoxId) REFERENCES Boxes(BoxId),
    CONSTRAINT FK_BoxExecutionQueue_Users FOREIGN KEY (RequestedByUserId) REFERENCES Users(UserId)
);
CREATE INDEX IX_BoxExecutionQueue_BoxId   ON BoxExecutionQueue(BoxId);
CREATE INDEX IX_BoxExecutionQueue_Status  ON BoxExecutionQueue(Status);
CREATE INDEX IX_BoxExecutionQueue_Created ON BoxExecutionQueue(CreatedAt);
PRINT 'BoxExecutionQueue table recreated successfully';

-- (Legacy placeholder — see script 004 for TaskDependencies)

