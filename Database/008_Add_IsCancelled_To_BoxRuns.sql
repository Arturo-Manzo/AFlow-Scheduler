IF COL_LENGTH('dbo.BoxRuns', 'IsCancelled') IS NULL
BEGIN
    ALTER TABLE dbo.BoxRuns
    ADD IsCancelled BIT NOT NULL CONSTRAINT DF_BoxRuns_IsCancelled DEFAULT 0;

    PRINT 'Added BoxRuns.IsCancelled column';
END
ELSE
BEGIN
    PRINT 'BoxRuns.IsCancelled already exists';
END