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
BACKUP DATABASE [$Database]
TO DISK = N'$BackupPath'
WITH INIT, COMPRESSION, CHECKSUM, STATS = 10;
"@

sqlcmd -S $Server -d master -E -b -Q $sql
Write-Host "Backup completed: $BackupPath"
