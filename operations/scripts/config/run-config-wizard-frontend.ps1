<#
Frontend Configuration Wizard

Purpose:
- Configure frontend runtime settings for IIS static hosting.
- Allow frontend and backend to run on different servers.
- Generate or preview frontend config.json values.

Behavior:
- In diagnostic mode (default), no files are written.
- In apply mode (-Apply), writes config.json next to this script.
- Normalizes backend URL to ensure it ends with /api.
#>

param(
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string]$Message)
    Write-Host "`n=== $Message ===" -ForegroundColor Cyan
}

function Read-Value {
    param(
        [string]$Prompt,
        [string]$Default = ""
    )

    if ([string]::IsNullOrWhiteSpace($Default)) {
        $value = Read-Host $Prompt
    }
    else {
        $value = Read-Host "$Prompt [$Default]"
    }

    if ([string]::IsNullOrWhiteSpace($value)) {
        return $Default
    }

    return $value.Trim()
}

function Backup-File {
    param([string]$FilePath)

    if (-not (Test-Path $FilePath)) {
        return
    }

    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $backupPath = "$FilePath.bak.$timestamp"
    Copy-Item -Path $FilePath -Destination $backupPath -Force
    Write-Host "Backup created: $backupPath" -ForegroundColor DarkGray
}

function Save-JsonFile {
    param(
        [string]$FilePath,
        [object]$Data
    )

    $directory = Split-Path -Parent $FilePath
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $json = $Data | ConvertTo-Json -Depth 20
    [System.IO.File]::WriteAllText($FilePath, $json + [Environment]::NewLine)
}

function Normalize-BackendApiUrl {
    param(
        [string]$Value,
        [string]$Fallback
    )

    $raw = if ([string]::IsNullOrWhiteSpace($Value)) { $Fallback } else { $Value }
    $normalized = $raw.Trim().TrimEnd('/')

    if ([string]::IsNullOrWhiteSpace($normalized)) {
        return $Fallback
    }

    if ($normalized -match '/api$') {
        return $normalized
    }

    return "$normalized/api"
}

$projectRoot = $PSScriptRoot
$frontendConfigPath = Join-Path $projectRoot "config.json"

Set-Location -Path $projectRoot

Write-Host "CHRONIQ Config Wizard - Frontend (CLI)" -ForegroundColor Green
Write-Host "Project root: $projectRoot" -ForegroundColor DarkGray
if (-not $Apply) {
    Write-Host "Diagnostic mode: no changes will be written (use -Apply to write)." -ForegroundColor Yellow
}

Write-Step "1) Frontend Configuration"
$backendUrlDefault = 'http://localhost:5000/api'
$backendUrlRaw = Read-Value -Prompt 'Backend URL for frontend (can be on another server)' -Default $backendUrlDefault
$backendUrl = Normalize-BackendApiUrl -Value $backendUrlRaw -Fallback $backendUrlDefault
if ($backendUrl -ne $backendUrlRaw) {
    Write-Host "Note: Backend URL was adjusted to '$backendUrl' to include /api suffix." -ForegroundColor Yellow
}

$frontendConfig = [pscustomobject]@{
    backendUrl = $backendUrl
}

if ($Apply) {
    Backup-File -FilePath $frontendConfigPath
    Save-JsonFile -FilePath $frontendConfigPath -Data $frontendConfig
    Write-Host "Written: $frontendConfigPath" -ForegroundColor Green
}
else {
    Write-Host "Frontend config preview ready (no write): $frontendConfigPath" -ForegroundColor Yellow
}

Write-Step "2) Result"
Write-Host "Frontend config: $frontendConfigPath" -ForegroundColor Gray
Write-Host "Backend URL:     $backendUrl" -ForegroundColor Gray
Write-Host "Hosting target:  IIS static site" -ForegroundColor Gray

Write-Host "`nFrontend Config Wizard finished." -ForegroundColor Green