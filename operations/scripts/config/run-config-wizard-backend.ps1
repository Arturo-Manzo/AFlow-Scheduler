<#
Backend Configuration Wizard

Purpose:
- Configure backend production settings independently from frontend hosting.
- Update connection string and JWT behavior in appsettings.Production.json.
- Generate or preview environment variables for backend runtime.

Behavior:
- In diagnostic mode (default), no files are written.
- In apply mode (-Apply), writes appsettings.Production.json and environment variables.
- Supports user or machine scope for environment variables.
- Updates Kestrel endpoint URL in appsettings (instead of ASPNETCORE_URLS).
#>

param(
    [string]$DefaultSqlServer = "(localdb)\\MSSQLLocalDB",
    [switch]$Apply,
    [switch]$UseMachineScope
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

function Read-YesNo {
    param(
        [string]$Prompt,
        [bool]$Default = $true
    )

    $defaultToken = if ($Default) { "Y/n" } else { "y/N" }
    $raw = Read-Host "$Prompt ($defaultToken)"
    if ([string]::IsNullOrWhiteSpace($raw)) {
        return $Default
    }

    $normalized = $raw.Trim().ToLowerInvariant()
    return $normalized -in @('y', 'yes', 's', 'si')
}

function Test-PortAvailable {
    param([int]$Port)

    $listener = $null
    try {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
        $listener.Start()
        return $true
    }
    catch {
        return $false
    }
    finally {
        if ($null -ne $listener) {
            $listener.Stop()
        }
    }
}

function New-Base64Secret {
    param([int]$Bytes = 64)

    $buffer = [byte[]]::new($Bytes)
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($buffer)
    }
    finally {
        $rng.Dispose()
    }

    return [Convert]::ToBase64String($buffer)
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

function Set-EnvironmentVariableScoped {
    param(
        [string]$Name,
        [string]$Value,
        [System.EnvironmentVariableTarget]$Scope
    )

    [Environment]::SetEnvironmentVariable($Name, $Value, $Scope)
}

function Ensure-NoteProperty {
    param(
        [object]$Target,
        [string]$Name,
        [object]$InitialValue
    )

    if ($null -eq $Target.PSObject.Properties[$Name]) {
        $Target | Add-Member -MemberType NoteProperty -Name $Name -Value $InitialValue
    }
}

function Normalize-KestrelHttpEndpoint {
    param(
        [object]$HttpEndpoint,
        [string]$FallbackUrl
    )

    if ($null -eq $HttpEndpoint) {
        return [pscustomobject]@{ Url = $FallbackUrl }
    }

    if ($HttpEndpoint -is [string]) {
        $urlFromString = $HttpEndpoint.Trim()
        return [pscustomobject]@{ Url = if ([string]::IsNullOrWhiteSpace($urlFromString)) { $FallbackUrl } else { $urlFromString } }
    }

    if ($HttpEndpoint -is [System.Collections.IDictionary]) {
        $urlFromDictionary = $null
        if ($HttpEndpoint.Contains('Url')) {
            $urlFromDictionary = [string]$HttpEndpoint['Url']
        }
        elseif ($HttpEndpoint.Contains('url')) {
            $urlFromDictionary = [string]$HttpEndpoint['url']
        }

        if ([string]::IsNullOrWhiteSpace($urlFromDictionary)) {
            $urlFromDictionary = $FallbackUrl
        }

        return [pscustomobject]@{ Url = $urlFromDictionary }
    }

    $urlFromObject = $null
    if ($null -ne $HttpEndpoint.PSObject.Properties['Url']) {
        $urlFromObject = [string]$HttpEndpoint.Url
    }
    elseif ($null -ne $HttpEndpoint.PSObject.Properties['url']) {
        $urlFromObject = [string]$HttpEndpoint.url
    }

    if ([string]::IsNullOrWhiteSpace($urlFromObject)) {
        $urlFromObject = $FallbackUrl
    }

    return [pscustomobject]@{ Url = $urlFromObject }
}

function Get-PropertyOrKeyValue {
    param(
        [object]$Source,
        [string[]]$Names
    )

    if ($null -eq $Source) {
        return $null
    }

    if ($Source -is [System.Collections.IDictionary]) {
        foreach ($name in $Names) {
            if ($Source.Contains($name)) {
                return $Source[$name]
            }
        }
        return $null
    }

    foreach ($name in $Names) {
        if ($null -ne $Source.PSObject.Properties[$name]) {
            return $Source.$name
        }
    }

    return $null
}

function Set-PropertyOrKeyValue {
    param(
        [object]$Target,
        [string]$Name,
        [object]$Value
    )

    if ($Target -is [System.Collections.IDictionary]) {
        $Target[$Name] = $Value
        return
    }

    if ($null -eq $Target.PSObject.Properties[$Name]) {
        $Target | Add-Member -MemberType NoteProperty -Name $Name -Value $Value
    }
    else {
        $Target.$Name = $Value
    }
}

$projectRoot = $PSScriptRoot
$appSettingsBasePath = Join-Path $projectRoot "appsettings.json"
$appSettingsProductionPath = Join-Path $projectRoot "appsettings.Production.json"
$preflightScriptPath = Join-Path $projectRoot "preflight-host-check.ps1"

Set-Location -Path $projectRoot

Write-Host "CHRONIQ Config Wizard - Backend (CLI)" -ForegroundColor Green
Write-Host "Project root: $projectRoot" -ForegroundColor DarkGray
if (-not $Apply) {
    Write-Host "Diagnostic mode: no changes will be written (use -Apply to write)." -ForegroundColor Yellow
}

Write-Step "1) Preflight Checks"
$dotnetVersion = dotnet --version 2>$null
if ([string]::IsNullOrWhiteSpace($dotnetVersion)) {
    throw "dotnet runtime was not found. Install .NET 8 runtime before continuing."
}

if (-not $dotnetVersion.StartsWith('8.')) {
    Write-Host "Warning: dotnet $dotnetVersion detected; target project version is .NET 8." -ForegroundColor Yellow
}

$appSettingsSourcePath = $appSettingsBasePath
if (-not (Test-Path $appSettingsSourcePath)) {
    if (Test-Path $appSettingsProductionPath) {
        $appSettingsSourcePath = $appSettingsProductionPath
        Write-Host "Note: appsettings.json not found, using appsettings.Production.json as source." -ForegroundColor Yellow
    }
    else {
        throw "File not found: $appSettingsBasePath"
    }
}

$appSettings = Get-Content $appSettingsSourcePath -Raw | ConvertFrom-Json

$defaultKestrelUrl = 'http://localhost:5000'
$currentKestrelUrl = $defaultKestrelUrl

if ($null -ne $appSettings.Kestrel -and
    $null -ne $appSettings.Kestrel.Endpoints -and
    $null -ne $appSettings.Kestrel.Endpoints.Http) {
    $normalizedCurrentEndpoint = Normalize-KestrelHttpEndpoint -HttpEndpoint $appSettings.Kestrel.Endpoints.Http -FallbackUrl $defaultKestrelUrl
    $currentKestrelUrl = $normalizedCurrentEndpoint.Url
}

$defaultKestrelUrl = $currentKestrelUrl
Write-Host "Current Kestrel HTTP URL: $defaultKestrelUrl" -ForegroundColor DarkGray

$backendKestrelUrl = Read-Value -Prompt 'Backend Kestrel URL (http://host:port)' -Default $defaultKestrelUrl
try {
    $backendUri = [System.Uri]$backendKestrelUrl
    $backendPort = $backendUri.Port
}
catch {
    throw "Invalid backend Kestrel URL: '$backendKestrelUrl'. Use format like http://localhost:5000"
}

$backendPortOk = Test-PortAvailable -Port $backendPort
if (-not $backendPortOk) {
    Write-Host "Warning: backend port $backendPort appears to be in use." -ForegroundColor Yellow
}

Write-Step "2) Backend Configuration"
$generateJwt = Read-YesNo -Prompt 'Generate JWT secret automatically' -Default $true
if ($generateJwt) {
    $jwtSecret = New-Base64Secret
}
else {
    $jwtSecret = Read-Value -Prompt 'Enter JWT secret'
    if ([string]::IsNullOrWhiteSpace($jwtSecret)) {
        throw 'JWT secret is required.'
    }
}

$defaultConnectionString = "Server=$DefaultSqlServer;Database=CHRONIQDB;Trusted_Connection=True;TrustServerCertificate=True"
$connectionString = Read-Value -Prompt 'Backend connection string' -Default $defaultConnectionString
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    throw 'Backend connection string is required.'
}
if ($null -eq $appSettings.ConnectionStrings) {
    $appSettings | Add-Member -MemberType NoteProperty -Name ConnectionStrings -Value ([pscustomobject]@{})
}
if ($null -eq $appSettings.Jwt) {
    $appSettings | Add-Member -MemberType NoteProperty -Name Jwt -Value ([pscustomobject]@{})
}

$kestrelPropertyName = if ($appSettings -is [System.Collections.IDictionary] -and $appSettings.Contains('kestrel')) { 'kestrel' } else { 'Kestrel' }
$existingKestrel = Get-PropertyOrKeyValue -Source $appSettings -Names @('Kestrel', 'kestrel')
$existingEndpoints = Get-PropertyOrKeyValue -Source $existingKestrel -Names @('Endpoints', 'endpoints')
$existingHttpEndpoint = Get-PropertyOrKeyValue -Source $existingEndpoints -Names @('Http', 'http')

$normalizedKestrel = [pscustomobject]@{
    Endpoints = [pscustomobject]@{
        Http = Normalize-KestrelHttpEndpoint -HttpEndpoint $existingHttpEndpoint -FallbackUrl $defaultKestrelUrl
    }
}
Set-PropertyOrKeyValue -Target $appSettings -Name $kestrelPropertyName -Value $normalizedKestrel
$kestrelConfig = Get-PropertyOrKeyValue -Source $appSettings -Names @('Kestrel', 'kestrel')
$endpointsConfig = Get-PropertyOrKeyValue -Source $kestrelConfig -Names @('Endpoints', 'endpoints')
$httpPropertyName = if ($endpointsConfig -is [System.Collections.IDictionary] -and $endpointsConfig.Contains('http')) { 'http' } else { 'Http' }
$httpEndpointConfig = Get-PropertyOrKeyValue -Source $endpointsConfig -Names @('Http', 'http')

$appSettings.ConnectionStrings.Default = $connectionString
$appSettings.Jwt.Secret = '__FROM_ENV__'
$httpEndpointConfig.Url = $backendKestrelUrl
Set-PropertyOrKeyValue -Target $endpointsConfig -Name $httpPropertyName -Value $httpEndpointConfig

if ($Apply) {
    Backup-File -FilePath $appSettingsProductionPath
    Save-JsonFile -FilePath $appSettingsProductionPath -Data $appSettings
    Write-Host "Written: $appSettingsProductionPath" -ForegroundColor Green
}
else {
    Write-Host "Backend config preview ready (no write): $appSettingsProductionPath" -ForegroundColor Yellow
}

$envScope = if ($UseMachineScope) { [System.EnvironmentVariableTarget]::Machine } else { [System.EnvironmentVariableTarget]::User }

$existingJwtSecret = [Environment]::GetEnvironmentVariable('CHRONIQ_JWT_SECRET', $envScope)
$alternateScope = if ($envScope -eq [System.EnvironmentVariableTarget]::User) { [System.EnvironmentVariableTarget]::Machine } else { [System.EnvironmentVariableTarget]::User }
$existingJwtSecretAlternate = [Environment]::GetEnvironmentVariable('CHRONIQ_JWT_SECRET', $alternateScope)
$replaceJwtSecret = $true
if (-not [string]::IsNullOrWhiteSpace($existingJwtSecret)) {
    $replaceJwtSecret = Read-YesNo -Prompt "CHRONIQ_JWT_SECRET already exists in scope $envScope. Replace it" -Default $false
}
elseif (-not [string]::IsNullOrWhiteSpace($existingJwtSecretAlternate)) {
    Write-Host "Note: CHRONIQ_JWT_SECRET exists in scope $alternateScope (current target scope is $envScope)." -ForegroundColor Yellow
}
else {
    Write-Host "No existing CHRONIQ_JWT_SECRET found in scope $envScope." -ForegroundColor DarkGray
}

if ($Apply) {
    if ($replaceJwtSecret) {
        Set-EnvironmentVariableScoped -Name 'CHRONIQ_JWT_SECRET' -Value $jwtSecret -Scope $envScope
    }
    Set-EnvironmentVariableScoped -Name 'DOTNET_ENVIRONMENT' -Value 'Production' -Scope $envScope

    if ($replaceJwtSecret) {
        Write-Host "Environment variables updated in scope: $envScope" -ForegroundColor Green
    }
    else {
        Write-Host "Environment variables updated in scope: $envScope (kept existing CHRONIQ_JWT_SECRET)" -ForegroundColor Green
    }
}
else {
    if ($replaceJwtSecret) {
        Write-Host "Environment variables preview ready (no write): CHRONIQ_JWT_SECRET, DOTNET_ENVIRONMENT" -ForegroundColor Yellow
    }
    else {
        Write-Host "Environment variables preview ready (no write): DOTNET_ENVIRONMENT (existing CHRONIQ_JWT_SECRET will be kept)" -ForegroundColor Yellow
    }
}

Write-Step "3) Result"
Write-Host "Backend config:  $appSettingsProductionPath" -ForegroundColor Gray
Write-Host "Connection str:  $connectionString" -ForegroundColor Gray
Write-Host "Backend port:    $backendPort" -ForegroundColor Gray
Write-Host "Kestrel URL:     $backendKestrelUrl" -ForegroundColor Gray

Write-Host "`nBackend Config Wizard finished." -ForegroundColor Green
if (Test-Path $preflightScriptPath) {
    Write-Host "Suggested next step: run $preflightScriptPath" -ForegroundColor DarkGray
}