-- ============================================
-- 070_Create_Application_Logs.sql
-- Warning/Error/Critical persistent application logs.
-- ============================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

CREATE TABLE dbo.ApplicationLogs
(
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    LogFileName NVARCHAR(100) NOT NULL,
    Timestamp DATETIME2 NOT NULL CONSTRAINT DF_ApplicationLogs_Timestamp DEFAULT GETUTCDATE(),
    Level NVARCHAR(10) NOT NULL,
    Message NVARCHAR(1000) NOT NULL,
    ErrorFile NVARCHAR(255) NULL,
    ErrorMethod NVARCHAR(255) NULL,
    ErrorLine INT NULL,
    ExceptionType NVARCHAR(255) NULL,
    Source NVARCHAR(255) NULL,
    CorrelationId UNIQUEIDENTIFIER NULL,
    UserId INT NULL,
    RequestPath NVARCHAR(500) NULL,
    StatusCode INT NULL,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_ApplicationLogs_CreatedAt DEFAULT GETUTCDATE(),
    CONSTRAINT FK_ApplicationLogs_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
);

CREATE INDEX IX_ApplicationLogs_Timestamp_Level ON dbo.ApplicationLogs(Timestamp DESC, Level);
CREATE INDEX IX_ApplicationLogs_CorrelationId ON dbo.ApplicationLogs(CorrelationId) WHERE CorrelationId IS NOT NULL;
CREATE INDEX IX_ApplicationLogs_ErrorFile_Line ON dbo.ApplicationLogs(ErrorFile, ErrorLine) WHERE ErrorFile IS NOT NULL;
CREATE INDEX IX_ApplicationLogs_ExceptionType ON dbo.ApplicationLogs(ExceptionType) WHERE ExceptionType IS NOT NULL;

PRINT 'Application logs table created.';
