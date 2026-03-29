-- ScheduledForUtc is included in Database/005_Create_TaskExecutions.sql,
-- which now drops and recreates TaskExecutions from scratch.
-- This script remains a compatibility no-op for existing migration chains.
PRINT 'Migration 007: no-op (ScheduledForUtc is recreated in script 005).';