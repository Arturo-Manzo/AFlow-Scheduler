param(
    [Parameter(Mandatory = $true)]
    [string]$Server,

    [Parameter(Mandatory = $true)]
    [string]$Database,

    [Parameter(Mandatory = $true)]
    [string]$BackupPath
)

$ErrorActionPreference = 'Stop'

$sql = @"
ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [$Database]
FROM DISK = N'$BackupPath'
WITH REPLACE, RECOVERY, STATS = 10;
ALTER DATABASE [$Database] SET MULTI_USER;
"@

sqlcmd -S $Server -d master -E -b -Q $sql
Write-Host "Restore completed from: $BackupPath"
