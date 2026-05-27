# Database Migrations

This folder contains incremental upgrade migrations for existing CHRONIQ installations.

Rules:

- Scripts are ordered and applied once.
- Applied migrations are tracked in `dbo.SchemaMigrations`.
- `000_Master_Rebuild.sql` remains for clean rebuild/reset scenarios only.
- Upgrade scripts should be additive and safe for in-place upgrades.
