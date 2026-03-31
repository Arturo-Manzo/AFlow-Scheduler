-- Migration 010: Add TimeZoneId to Boxes and backfill existing rows.
-- Cron expressions remain stored as local wall-clock values defined by the user.
-- TimeZoneId defines how the scheduler interprets each box schedule.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Boxes')
      AND name = N'TimeZoneId'
)
BEGIN
    ALTER TABLE dbo.Boxes
        ADD TimeZoneId NVARCHAR(100) NOT NULL
            CONSTRAINT DF_Boxes_TimeZoneId DEFAULT N'Etc/UTC';
END
GO

UPDATE dbo.Boxes
SET TimeZoneId = N'Etc/UTC'
WHERE TimeZoneId IS NULL OR LTRIM(RTRIM(TimeZoneId)) = N'';
GO