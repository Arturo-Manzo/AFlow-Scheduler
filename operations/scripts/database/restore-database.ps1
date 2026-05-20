param(
    [Parameter(Mandatory = $true)]
    [string]$Server,

    [Parameter(Mandatory = $true)]
    [string]$Database,

    [Parameter(Mandatory = $true)]
    [string]$BackupPath
)

$ErrorActionPreference = 'Stop'

# Get LocalDB data directory for the instance
if ($Server -match '\(localdb\)\\(.+)') {
    $instanceName = $Matches[1]
    $localDbPath = "$env:LOCALAPPDATA\Microsoft\Microsoft SQL Server Local DB\Instances\$instanceName"
    $dataPath = "$localDbPath\$Database.mdf"
    $logPath = "$localDbPath\${Database}_log.ldf"
} else {
    # For regular SQL Server, use default data directory
    $dataPath = "C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\DATA\$Database.mdf"
    $logPath = "C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\DATA\${Database}_log.ldf"
}

$sql = @"
ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [$Database]
FROM DISK = N'$BackupPath'
WITH REPLACE, RECOVERY, STATS = 10,
MOVE 'CHRONIQDB' TO '$dataPath',
MOVE 'CHRONIQDB_log' TO '$logPath';
ALTER DATABASE [$Database] SET MULTI_USER;
"@

sqlcmd -S $Server -d master -E -b -Q $sql
Write-Host "Restore completed from: $BackupPath"

pause
