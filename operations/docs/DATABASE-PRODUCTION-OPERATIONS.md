# Database Production Operations

## 1. Staging Migration Validation
Run in SQLCMD mode:

```sql
:r .\000_Master_Rebuild.sql
```

Expected outcome:
- All scripts complete without errors.
- `090_Validate_Integrity_And_Continuity.sql` reports passed.

## 2. Rollback Strategy
- Use latest validated full backup.
- Restore with `operations/scripts/database/restore-database.ps1`.
- Re-run integrity validation script after restore.

## 3. Backup and Restore Commands
Backup example:

```powershell
.\operations\scripts\database\backup-database.ps1 -Server "(localdb)\MSSQLLocalDB" -Database "ASchedulerDB" -BackupPath "C:\Backups\ASchedulerDB_full.bak"
```

Restore example:

```powershell
.\operations\scripts\database\restore-database.ps1 -Server "(localdb)\MSSQLLocalDB" -Database "ASchedulerDB" -BackupPath "C:\Backups\ASchedulerDB_full.bak"
```

## 4. Post-Deployment Data Continuity Check
Run:

```sql
:r .\090_Validate_Integrity_And_Continuity.sql
```

If it fails:
- Stop traffic to API.
- Investigate lifecycle anomalies in `TaskExecutions` and `BoxRuns`.
- Roll back DB if integrity cannot be restored quickly.

## 5. Performance Baseline
After deployment, capture query duration for:
- Latest executions endpoint.
- Failed executions endpoint.
- Running executions endpoint.
- BoxRun execution details endpoint.

If latency regresses:
- Check indexes introduced by `080_Optimize_Execution_Queries.sql`.
- Inspect execution plans and missing index recommendations.
