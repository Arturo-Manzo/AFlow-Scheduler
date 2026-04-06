param(
    [Parameter(Mandatory = $true)]
    [string]$ServiceName,

    [Parameter(Mandatory = $true)]
    [string]$BinaryPath,

    [string]$DisplayName = "CHRONIQ Service",
    [string]$Description = "CHRONIQ API + Scheduler Worker",
    [string]$StartupType = "auto"
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $BinaryPath)) {
    throw "Binary path not found: $BinaryPath"
}

Write-Host "Creating service $ServiceName..." -ForegroundColor Cyan
sc.exe create $ServiceName binPath= "\"$BinaryPath\"" start= $StartupType DisplayName= "$DisplayName" | Out-Null
sc.exe description $ServiceName "$Description" | Out-Null

Write-Host "Configuring automatic restart policy..." -ForegroundColor Cyan
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

Write-Host "Starting service..." -ForegroundColor Cyan
sc.exe start $ServiceName | Out-Null

Write-Host "Service $ServiceName installed and started." -ForegroundColor Green
