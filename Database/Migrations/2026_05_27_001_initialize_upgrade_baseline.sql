-- ============================================
-- 2026_05_27_001_initialize_upgrade_baseline.sql
-- Bootstrap migration for versioned upgrade tracking.
-- Safe to execute once and record in dbo.SchemaMigrations.
-- ============================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT 'Initialized upgrade baseline migration.';
