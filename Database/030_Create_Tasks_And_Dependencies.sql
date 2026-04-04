-- ============================================
-- 030_Create_Tasks_And_Dependencies.sql
-- Task catalog and DAG dependencies.
-- ============================================

CREATE TABLE dbo.Tasks
(
    TaskId INT IDENTITY(1,1) PRIMARY KEY,
    BoxId INT NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NULL,
    Command NVARCHAR(4000) NOT NULL,
    TaskType NVARCHAR(20) NOT NULL,
    AllowParallel BIT NOT NULL CONSTRAINT DF_Tasks_AllowParallel DEFAULT 0,
    SortOrder INT NOT NULL CONSTRAINT DF_Tasks_SortOrder DEFAULT 0,
    Enabled BIT NOT NULL CONSTRAINT DF_Tasks_Enabled DEFAULT 1,
    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_Tasks_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Tasks_Boxes FOREIGN KEY (BoxId) REFERENCES dbo.Boxes(BoxId) ON DELETE CASCADE,
    CONSTRAINT CK_Tasks_TaskType CHECK (TaskType IN ('Exe', 'Bat', 'Python', 'Api'))
);

CREATE INDEX IX_Tasks_BoxId_Sort ON dbo.Tasks(BoxId, SortOrder);
CREATE INDEX IX_Tasks_Enabled ON dbo.Tasks(Enabled);

CREATE TABLE dbo.TaskDependencies
(
    TaskId INT NOT NULL,
    DependsOnTaskId INT NOT NULL,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TaskDependencies_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_TaskDependencies PRIMARY KEY (TaskId, DependsOnTaskId),
    CONSTRAINT FK_TaskDependencies_Task FOREIGN KEY (TaskId) REFERENCES dbo.Tasks(TaskId) ON DELETE CASCADE,
    CONSTRAINT FK_TaskDependencies_DependsOn FOREIGN KEY (DependsOnTaskId) REFERENCES dbo.Tasks(TaskId),
    CONSTRAINT CK_TaskDependencies_NoSelfDependency CHECK (TaskId <> DependsOnTaskId)
);

CREATE INDEX IX_TaskDependencies_DependsOn ON dbo.TaskDependencies(DependsOnTaskId);

PRINT 'Tasks and dependencies created.';
