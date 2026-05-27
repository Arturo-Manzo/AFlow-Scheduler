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

function Resolve-ConfigPath {
    $candidatePaths = @(
        (Join-Path $PSScriptRoot "config.json"),
        (Join-Path $PSScriptRoot "..\..\..\frontend\public\config.json")
    )

    foreach ($candidatePath in $candidatePaths) {
        $fullPath = [System.IO.Path]::GetFullPath($candidatePath)
        if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
            return $fullPath
        }
    }

    return [System.IO.Path]::GetFullPath($candidatePaths[0])
}

function Resolve-TemplatePath {
    $candidatePaths = @(
        (Join-Path $PSScriptRoot "deployment-templates\frontend\config.upgrade-template.json"),
        (Join-Path $PSScriptRoot "..\..\..\deployment-templates\frontend\config.upgrade-template.json")
    )

    foreach ($candidatePath in $candidatePaths) {
        $fullPath = [System.IO.Path]::GetFullPath($candidatePath)
        if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
            return $fullPath
        }
    }

    return $null
}

function Get-ObjectPropertyNames {
    param([object]$Value)

    if ($null -eq $Value) {
        return @()
    }

    if ($Value -is [System.Collections.IDictionary]) {
        return @($Value.Keys)
    }

    return @($Value.PSObject.Properties | ForEach-Object { $_.Name })
}

function Get-ObjectPropertyValue {
    param(
        [object]$Value,
        [string]$Name
    )

    if ($null -eq $Value) {
        return $null
    }

    if ($Value -is [System.Collections.IDictionary]) {
        if ($Value.Contains($Name)) {
            return $Value[$Name]
        }
        return $null
    }

    if ($null -ne $Value.PSObject.Properties[$Name]) {
        return $Value.$Name
    }

    return $null
}

function Set-ObjectPropertyValue {
    param(
        [object]$Value,
        [string]$Name,
        [object]$PropertyValue
    )

    if ($Value -is [System.Collections.IDictionary]) {
        $Value[$Name] = $PropertyValue
        return
    }

    if ($null -eq $Value.PSObject.Properties[$Name]) {
        $Value | Add-Member -MemberType NoteProperty -Name $Name -Value $PropertyValue
    }
    else {
        $Value.$Name = $PropertyValue
    }
}

function Test-IsJsonObject {
    param([object]$Value)

    if ($null -eq $Value -or $Value -is [string]) {
        return $false
    }

    if ($Value -is [System.Collections.IDictionary]) {
        return $true
    }

    if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [pscustomobject]) {
        return $false
    }

    return $Value.PSObject.Properties.Count -gt 0
}

function Clone-JsonValue {
    param([object]$Value)

    if ($null -eq $Value) {
        return $null
    }

    if (Test-IsJsonObject -Value $Value) {
        $clone = [pscustomobject]@{}
        foreach ($propertyName in Get-ObjectPropertyNames -Value $Value) {
            Set-ObjectPropertyValue -Value $clone -Name $propertyName -PropertyValue (Clone-JsonValue -Value (Get-ObjectPropertyValue -Value $Value -Name $propertyName))
        }
        return $clone
    }

    if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) {
        $items = New-Object System.Collections.ArrayList
        foreach ($item in $Value) {
            [void]$items.Add((Clone-JsonValue -Value $item))
        }
        return ,$items.ToArray()
    }

    return $Value
}

function Merge-MissingJsonProperties {
    param(
        [object]$Existing,
        [object]$Template
    )

    foreach ($propertyName in Get-ObjectPropertyNames -Value $Template) {
        $templateValue = Get-ObjectPropertyValue -Value $Template -Name $propertyName
        $existingProperty = $Existing.PSObject.Properties[$propertyName]

        if ($null -eq $existingProperty) {
            Set-ObjectPropertyValue -Value $Existing -Name $propertyName -PropertyValue (Clone-JsonValue -Value $templateValue)
            continue
        }

        $existingValue = $existingProperty.Value
        if ((Test-IsJsonObject -Value $existingValue) -and (Test-IsJsonObject -Value $templateValue)) {
            Merge-MissingJsonProperties -Existing $existingValue -Template $templateValue
        }
    }
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
$frontendConfigPath = Resolve-ConfigPath
$frontendTemplatePath = Resolve-TemplatePath

Set-Location -Path $projectRoot

Write-Host "CHRONIQ Config Wizard - Frontend (CLI)" -ForegroundColor Green
Write-Host "Project root: $projectRoot" -ForegroundColor DarkGray
if (-not $Apply) {
    Write-Host "Diagnostic mode: no changes will be written (use -Apply to write)." -ForegroundColor Yellow
}

$frontendConfig = if (Test-Path -LiteralPath $frontendConfigPath -PathType Leaf) {
    Get-Content -LiteralPath $frontendConfigPath -Raw | ConvertFrom-Json
}
elseif ($frontendTemplatePath) {
    Get-Content -LiteralPath $frontendTemplatePath -Raw | ConvertFrom-Json
}
else {
    [pscustomobject]@{}
}

if ($frontendTemplatePath) {
    $frontendTemplate = Get-Content -LiteralPath $frontendTemplatePath -Raw | ConvertFrom-Json
    Merge-MissingJsonProperties -Existing $frontendConfig -Template $frontendTemplate
}

Write-Step "1) Frontend Configuration"
$backendUrlDefault = if ($null -ne $frontendConfig.PSObject.Properties['backendUrl'] -and -not [string]::IsNullOrWhiteSpace([string]$frontendConfig.backendUrl)) {
    [string]$frontendConfig.backendUrl
}
else {
    'http://localhost:5000/api'
}
$backendUrlRaw = Read-Value -Prompt 'Backend URL for frontend (can be on another server)' -Default $backendUrlDefault
$backendUrl = Normalize-BackendApiUrl -Value $backendUrlRaw -Fallback $backendUrlDefault
if ($backendUrl -ne $backendUrlRaw) {
    Write-Host "Note: Backend URL was adjusted to '$backendUrl' to include /api suffix." -ForegroundColor Yellow
}

Set-ObjectPropertyValue -Value $frontendConfig -Name "backendUrl" -PropertyValue $backendUrl

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
