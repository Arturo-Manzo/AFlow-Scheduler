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

function Test-SqlConnection {
    param(
        [string]$Server,
        [string]$Database = 'master'
    )

    $sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
    if ($null -eq $sqlcmd) {
        return [pscustomobject]@{
            Available = $false
            Connected = $false
            Message = 'sqlcmd no esta disponible en PATH.'
        }
    }

    $null = sqlcmd -S $Server -d $Database -Q "SELECT 1" -b 2>$null
    if ($LASTEXITCODE -eq 0) {
        return [pscustomobject]@{
            Available = $true
            Connected = $true
            Message = "Conexion SQL OK a $Server"
        }
    }

    return [pscustomobject]@{
        Available = $true
        Connected = $false
        Message = "No se pudo conectar a SQL Server: $Server"
    }
}

function Backup-File {
    param([string]$FilePath)

    if (-not (Test-Path $FilePath)) {
        return
    }

    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $backupPath = "$FilePath.bak.$timestamp"
    Copy-Item -Path $FilePath -Destination $backupPath -Force
    Write-Host "Backup creado: $backupPath" -ForegroundColor DarkGray
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

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$frontendConfigPath = Join-Path $projectRoot "frontend\public\config.json"
$appSettingsBasePath = Join-Path $projectRoot "appsettings.json"
$appSettingsProductionPath = Join-Path $projectRoot "appsettings.Production.json"

Write-Host "AScheduler Config Wizard (CLI)" -ForegroundColor Green
Write-Host "Project root: $projectRoot" -ForegroundColor DarkGray
if (-not $Apply) {
    Write-Host "Modo diagnostico: no se aplicaran cambios (usa -Apply para escribir)." -ForegroundColor Yellow
}

Write-Step "1) Validaciones previas"
$dotnetVersion = dotnet --version 2>$null
if ([string]::IsNullOrWhiteSpace($dotnetVersion)) {
    throw "No se encontro dotnet runtime. Instala .NET 8 runtime antes de continuar."
}

if (-not $dotnetVersion.StartsWith('8.')) {
    Write-Host "Advertencia: se detecto dotnet $dotnetVersion; el proyecto objetivo usa .NET 8." -ForegroundColor Yellow
}

$frontendPort = [int](Read-Value -Prompt 'Puerto frontend' -Default '4000')
$backendPort = [int](Read-Value -Prompt 'Puerto backend' -Default '5000')

$frontendPortOk = Test-PortAvailable -Port $frontendPort
$backendPortOk = Test-PortAvailable -Port $backendPort

if (-not $frontendPortOk) {
    Write-Host "Advertencia: puerto frontend $frontendPort parece ocupado." -ForegroundColor Yellow
}
if (-not $backendPortOk) {
    Write-Host "Advertencia: puerto backend $backendPort parece ocupado." -ForegroundColor Yellow
}

$sqlServer = Read-Value -Prompt 'Servidor SQL o LocalDB' -Default $DefaultSqlServer
$sqlConnectionCheck = Test-SqlConnection -Server $sqlServer
$sqlStatusColor = 'Yellow'
if ($sqlConnectionCheck.Connected) {
    $sqlStatusColor = 'Green'
}
Write-Host $sqlConnectionCheck.Message -ForegroundColor $sqlStatusColor

Write-Step "2) Configuracion Backend"
$generateJwt = Read-YesNo -Prompt 'Generar JWT secret automaticamente' -Default $true
if ($generateJwt) {
    $jwtSecret = New-Base64Secret
}
else {
    $jwtSecret = Read-Value -Prompt 'Ingresa JWT secret'
    if ([string]::IsNullOrWhiteSpace($jwtSecret)) {
        throw 'JWT secret es obligatorio.'
    }
}

$defaultConnectionString = "Server=$sqlServer;Database=ASchedulerDB;Trusted_Connection=True;TrustServerCertificate=True"
$connectionString = Read-Value -Prompt 'Connection string backend' -Default $defaultConnectionString

if (-not (Test-Path $appSettingsBasePath)) {
    throw "No se encontro $appSettingsBasePath"
}

$appSettings = Get-Content $appSettingsBasePath -Raw | ConvertFrom-Json
if ($null -eq $appSettings.ConnectionStrings) {
    $appSettings | Add-Member -MemberType NoteProperty -Name ConnectionStrings -Value ([pscustomobject]@{})
}
if ($null -eq $appSettings.Jwt) {
    $appSettings | Add-Member -MemberType NoteProperty -Name Jwt -Value ([pscustomobject]@{})
}

$appSettings.ConnectionStrings.Default = $connectionString
$appSettings.Jwt.Secret = '__FROM_ENV__'

if ($Apply) {
    Backup-File -FilePath $appSettingsProductionPath
    Save-JsonFile -FilePath $appSettingsProductionPath -Data $appSettings
    Write-Host "Escrito: $appSettingsProductionPath" -ForegroundColor Green
}
else {
    Write-Host "Preview backend config listo (sin escritura): $appSettingsProductionPath" -ForegroundColor Yellow
}

$envScope = if ($UseMachineScope) { [System.EnvironmentVariableTarget]::Machine } else { [System.EnvironmentVariableTarget]::User }
if ($Apply) {
    Set-EnvironmentVariableScoped -Name 'ASCHEDULER_JWT_SECRET' -Value $jwtSecret -Scope $envScope
    Set-EnvironmentVariableScoped -Name 'DOTNET_ENVIRONMENT' -Value 'Production' -Scope $envScope
    Set-EnvironmentVariableScoped -Name 'ASPNETCORE_URLS' -Value "http://localhost:$backendPort" -Scope $envScope
    Write-Host "Variables de entorno actualizadas en scope: $envScope" -ForegroundColor Green
}
else {
    Write-Host "Preview env vars listo (sin escritura): ASCHEDULER_JWT_SECRET, DOTNET_ENVIRONMENT, ASPNETCORE_URLS" -ForegroundColor Yellow
}

Write-Step "3) Configuracion Frontend"
$backendUrlDefault = "http://localhost:$backendPort/api"
$backendUrl = Read-Value -Prompt 'Backend URL para frontend' -Default $backendUrlDefault

$frontendConfig = [pscustomobject]@{
    port = $frontendPort
    backendUrl = $backendUrl
}

if ($Apply) {
    Backup-File -FilePath $frontendConfigPath
    Save-JsonFile -FilePath $frontendConfigPath -Data $frontendConfig
    Write-Host "Escrito: $frontendConfigPath" -ForegroundColor Green
}
else {
    Write-Host "Preview frontend config listo (sin escritura): $frontendConfigPath" -ForegroundColor Yellow
}

Write-Step "4) Resultado"
Write-Host "Frontend config: $frontendConfigPath" -ForegroundColor Gray
Write-Host "Backend config:  $appSettingsProductionPath" -ForegroundColor Gray
Write-Host "SQL target:      $sqlServer" -ForegroundColor Gray
Write-Host "Backend URL:     $backendUrl" -ForegroundColor Gray
Write-Host "Frontend port:   $frontendPort" -ForegroundColor Gray
Write-Host "Backend port:    $backendPort" -ForegroundColor Gray

Write-Host "`nConfig Wizard finalizado." -ForegroundColor Green
Write-Host "Siguiente paso sugerido: ejecutar operations/scripts/infra/preflight-host-check.ps1" -ForegroundColor DarkGray
