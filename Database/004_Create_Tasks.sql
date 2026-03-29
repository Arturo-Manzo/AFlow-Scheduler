-- ============================================
-- Tasks Table (executable steps within a Box)
-- ============================================

IF OBJECT_ID('dbo.TaskExecutions', 'U') IS NOT NULL DROP TABLE dbo.TaskExecutions;
IF OBJECT_ID('dbo.TaskDependencies', 'U') IS NOT NULL DROP TABLE dbo.TaskDependencies;
IF OBJECT_ID('dbo.Tasks', 'U') IS NOT NULL DROP TABLE dbo.Tasks;

CREATE TABLE Tasks (
    TaskId        INT IDENTITY(1,1) PRIMARY KEY,
    BoxId         INT NOT NULL,
    Name          NVARCHAR(100) NOT NULL,
    Description   NVARCHAR(500) NULL,
    Command       NVARCHAR(4000) NOT NULL,
    TaskType      NVARCHAR(20) NOT NULL,
    AllowParallel BIT NOT NULL DEFAULT 0,
    SortOrder     INT NOT NULL DEFAULT 0,
    Enabled       BIT NOT NULL DEFAULT 1,
    CreatedAtUtc  DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Tasks_Boxes FOREIGN KEY (BoxId) REFERENCES Boxes(BoxId) ON DELETE CASCADE
);
CREATE INDEX IX_Tasks_BoxId_Sort ON Tasks(BoxId, SortOrder);
CREATE INDEX IX_Tasks_Enabled    ON Tasks(Enabled);
PRINT 'Tasks table recreated successfully';

-- TaskDependencies: ordered step execution within the same Box
CREATE TABLE TaskDependencies (
    TaskId          INT NOT NULL,
    DependsOnTaskId INT NOT NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    PRIMARY KEY (TaskId, DependsOnTaskId),
    CONSTRAINT FK_TaskDeps_Task      FOREIGN KEY (TaskId)          REFERENCES Tasks(TaskId) ON DELETE CASCADE,
    CONSTRAINT FK_TaskDeps_DependsOn FOREIGN KEY (DependsOnTaskId) REFERENCES Tasks(TaskId),
    CONSTRAINT CK_No_Self_Dependency CHECK (TaskId <> DependsOnTaskId)
);
CREATE INDEX IX_TaskDependencies_DependsOn ON TaskDependencies(DependsOnTaskId);
PRINT 'TaskDependencies table recreated successfully';
