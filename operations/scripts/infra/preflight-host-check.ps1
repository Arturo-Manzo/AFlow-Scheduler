param(
    [string]$SqlServer = "(localdb)\MSSQLLocalDB",
    [string]$LogPath = ".\logs"
)

$ErrorActionPreference = 'Stop'
$issues = @()

Write-Host "[Preflight] Checking .NET runtime..." -ForegroundColor Cyan
$dotnetVersion = dotnet --version 2>$null
if (-not $dotnetVersion) { $issues += "dotnet runtime not found" }

Write-Host "[Preflight] Checking sqlcmd availability..." -ForegroundColor Cyan
$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmd) { $issues += "sqlcmd not found in PATH" }

Write-Host "[Preflight] Checking logs folder write permissions..." -ForegroundColor Cyan
if (-not (Test-Path $LogPath)) {
    New-Item -ItemType Directory -Path $LogPath -Force | Out-Null
}
try {
    $probe = Join-Path $LogPath "_write_probe.tmp"
    "ok" | Set-Content $probe -Encoding UTF8
    Remove-Item $probe -Force
}
catch {
    $issues += "cannot write to logs path: $LogPath"
}

Write-Host "[Preflight] Checking SQL connectivity..." -ForegroundColor Cyan
if ($sqlcmd) {
    $prevEAP = $ErrorActionPreference
    $ErrorActionPreference = 'SilentlyContinue'
    $null = sqlcmd -S $SqlServer -d master -Q "SELECT 1" -b 2>$null
    $sqlExitCode = $LASTEXITCODE
    $ErrorActionPreference = $prevEAP
    if ($sqlExitCode -ne 0) {
        $issues += "cannot connect to SQL server $SqlServer"
    }
}

if ($issues.Count -gt 0) {
    Write-Host "[Preflight] FAILED" -ForegroundColor Red
    $issues | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    exit 1
}

Write-Host "[Preflight] PASSED" -ForegroundColor Green
