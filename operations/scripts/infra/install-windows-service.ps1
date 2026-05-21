param(
    [string]$ServiceName = "CHRONIQ",
    [string]$BinaryPath = ".\AScheduler.exe",
    [string]$DisplayName = "CHRONIQ Service",
    [string]$Description = "CHRONIQ API + Scheduler Worker",
    [ValidateSet("auto", "delayed-auto", "demand", "disabled")]
    [string]$StartupType = "delayed-auto",
    [string]$Username = "",
    [string]$Password = "",
    [switch]$Force,
    [switch]$NoStart
)

$ErrorActionPreference = 'Stop'

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Run this script from an elevated PowerShell session."
    }
}

function Invoke-Sc {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)

    $output = & sc.exe @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "sc.exe $($Arguments -join ' ') failed with exit code $LASTEXITCODE. Output: $output"
    }

    return $output
}

function Wait-ServiceState {
    param(
        [string]$Name,
        [string]$DesiredStatus,
        [int]$TimeoutSeconds = 30
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $service = Get-Service -Name $Name -ErrorAction SilentlyContinue
        if ($service -and $service.Status.ToString() -eq $DesiredStatus) {
            return
        }

        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)

    throw "Service '$Name' did not reach status '$DesiredStatus' within $TimeoutSeconds seconds."
}

Assert-Administrator

$resolvedBinary = (Resolve-Path -LiteralPath $BinaryPath -ErrorAction Stop).Path
if (-not (Test-Path -LiteralPath $resolvedBinary -PathType Leaf)) {
    throw "Binary path not found: $resolvedBinary"
}

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    if (-not $Force) {
        throw "Service '$ServiceName' already exists. Re-run with -Force to update it."
    }

    Write-Host "Updating existing service $ServiceName..." -ForegroundColor Cyan
    if ($service.Status -ne 'Stopped') {
        Stop-Service -Name $ServiceName -Force
        Wait-ServiceState -Name $ServiceName -DesiredStatus "Stopped"
    }

    $configArgs = @("config", $ServiceName, "binPath=", "`"$resolvedBinary`"", "DisplayName=", $DisplayName)
    if (-not [string]::IsNullOrWhiteSpace($Username)) {
        $configArgs += @("obj=", $Username, "password=", $Password)
    }
    Invoke-Sc @configArgs | Out-Null
}
else {
    Write-Host "Creating service $ServiceName..." -ForegroundColor Cyan
    $createArgs = @("create", $ServiceName, "binPath=", "`"$resolvedBinary`"", "DisplayName=", $DisplayName)
    if (-not [string]::IsNullOrWhiteSpace($Username)) {
        $createArgs += @("obj=", $Username, "password=", $Password)
    }
    Invoke-Sc @createArgs | Out-Null
}

if ($StartupType -eq "delayed-auto") {
    Invoke-Sc config $ServiceName start= auto | Out-Null
    Invoke-Sc config $ServiceName delayed-auto= yes | Out-Null
}
else {
    Invoke-Sc config $ServiceName start= $StartupType | Out-Null
}

Invoke-Sc description $ServiceName $Description | Out-Null

Write-Host "Configuring automatic restart policy..." -ForegroundColor Cyan
Invoke-Sc failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null
Invoke-Sc failureflag $ServiceName 1 | Out-Null

if (-not $NoStart) {
    Write-Host "Starting service..." -ForegroundColor Cyan
    Start-Service -Name $ServiceName
    Wait-ServiceState -Name $ServiceName -DesiredStatus "Running"
}

Write-Host "Service $ServiceName is installed." -ForegroundColor Green
Write-Host "Binary: $resolvedBinary" -ForegroundColor Gray
