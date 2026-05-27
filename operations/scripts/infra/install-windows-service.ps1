param(
    [string]$ServiceName = "CHRONIQ",
    [string]$BinaryPath = "",
    [string]$DisplayName = "CHRONIQ Service",
    [string]$Description = "CHRONIQ API + Scheduler Worker",
    [ValidateSet("auto", "delayed-auto", "demand", "disabled")]
    [string]$StartupType = "delayed-auto",
    [string]$Username = "",
    [string]$Password = "",
    [switch]$Force,
    [switch]$NoStart,
    [switch]$SkipPrerequisiteChecks
)

$ErrorActionPreference = 'Stop'

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function ConvertTo-PowerShellArgumentLiteral {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return '$null'
    }

    return "'$($Value.ToString().Replace("'", "''"))'"
}

function Restart-Elevated {
    $scriptPath = $MyInvocation.PSCommandPath
    $argumentList = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', (ConvertTo-PowerShellArgumentLiteral $scriptPath)
    )

    foreach ($entry in $PSBoundParameters.GetEnumerator()) {
        $argumentList += "-$($entry.Key)"
        if ($entry.Value -isnot [switch] -or $entry.Value.IsPresent) {
            if ($entry.Value -isnot [switch]) {
                $argumentList += (ConvertTo-PowerShellArgumentLiteral $entry.Value)
            }
        }
    }

    Write-Host "Re-launching installer as administrator..." -ForegroundColor Yellow
    $process = Start-Process -FilePath "powershell.exe" -ArgumentList ($argumentList -join ' ') -Verb RunAs -Wait -PassThru
    exit $process.ExitCode
}

function Assert-Administrator {
    if (-not (Test-IsAdministrator)) {
        Restart-Elevated
    }
}

function Get-ServiceBinaryPathCandidates {
    param([string]$RequestedPath)

    $scriptDirectory = Split-Path -Parent $PSCommandPath
    $repoRoot = (Resolve-Path (Join-Path $scriptDirectory "..\..\..")).Path
    $candidatePaths = [System.Collections.Generic.List[string]]::new()

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        $candidatePaths.Add($RequestedPath)

        if (-not [System.IO.Path]::IsPathRooted($RequestedPath)) {
            $candidatePaths.Add((Join-Path $repoRoot $RequestedPath))
            $candidatePaths.Add((Join-Path $scriptDirectory $RequestedPath))
        }
    }
    else {
        $candidatePaths.Add((Join-Path $repoRoot "artifacts\review-publish-chroniq\CHRONIQ.exe"))
        $candidatePaths.Add((Join-Path $repoRoot "bin\Release\net8.0\CHRONIQ.exe"))
    }

    $binaryName = if ([string]::IsNullOrWhiteSpace($RequestedPath)) { "CHRONIQ.exe" } else { [System.IO.Path]::GetFileName($RequestedPath) }
    if (-not [string]::IsNullOrWhiteSpace($binaryName)) {
        $candidatePaths.Add((Join-Path $repoRoot "artifacts\review-publish-chroniq\$binaryName"))
        $candidatePaths.Add((Join-Path $repoRoot "bin\Release\net8.0\$binaryName"))
    }

    return $candidatePaths | Select-Object -Unique
}

function Prompt-ServiceBinaryPath {
    param([string[]]$SuggestedPaths)

    Write-Host "Enter the full path to the service executable (.exe)." -ForegroundColor Cyan

    $existingSuggestions = @($SuggestedPaths | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
    if ($existingSuggestions.Count -gt 0) {
        Write-Host "Detected candidate executables:" -ForegroundColor DarkCyan
        for ($index = 0; $index -lt $existingSuggestions.Count; $index++) {
            Write-Host ("  [{0}] {1}" -f ($index + 1), $existingSuggestions[$index])
        }
        Write-Host "Press Enter to use [1], type a number, or paste a custom path." -ForegroundColor DarkGray
    }
    else {
        Write-Host "No local candidate executable was detected automatically. Paste a custom path." -ForegroundColor Yellow
    }

    while ($true) {
        $inputValue = Read-Host "Executable path"

        if ([string]::IsNullOrWhiteSpace($inputValue)) {
            if ($existingSuggestions.Count -gt 0) {
                return $existingSuggestions[0]
            }

            Write-Warning "A path is required."
            continue
        }

        $selectedIndex = 0
        if ([int]::TryParse($inputValue, [ref]$selectedIndex)) {
            if ($selectedIndex -ge 1 -and $selectedIndex -le $existingSuggestions.Count) {
                return $existingSuggestions[$selectedIndex - 1]
            }

            Write-Warning "Select a valid option number or paste a path."
            continue
        }

        return $inputValue
    }
}

function Resolve-ServiceBinaryPath {
    param([string]$RequestedPath)

    $candidatePaths = @(Get-ServiceBinaryPathCandidates -RequestedPath $RequestedPath)

    if ([string]::IsNullOrWhiteSpace($RequestedPath)) {
        $RequestedPath = Prompt-ServiceBinaryPath -SuggestedPaths $candidatePaths
        $candidatePaths = @(Get-ServiceBinaryPathCandidates -RequestedPath $RequestedPath)
    }

    foreach ($candidate in $candidatePaths) {
        try {
            $resolved = Resolve-Path -LiteralPath $candidate -ErrorAction Stop
            if (Test-Path -LiteralPath $resolved.Path -PathType Leaf) {
                return $resolved.Path
            }
        }
        catch {
        }
    }

    $searched = $candidatePaths | Select-Object -Unique
    throw "Binary path not found. Checked: $($searched -join ', ')"
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

function Get-ConnectionStringFromAppSettings {
    param([string]$BaseDirectory)

    $productionSettingsPath = Join-Path $BaseDirectory "appsettings.Production.json"
    if (-not (Test-Path -LiteralPath $productionSettingsPath -PathType Leaf)) {
        return $null
    }

    try {
        $settings = Get-Content -LiteralPath $productionSettingsPath -Raw | ConvertFrom-Json
        return $settings.ConnectionStrings.Default
    }
    catch {
        throw "Could not read '$productionSettingsPath'. $($_.Exception.Message)"
    }
}

function Test-BuiltInServiceAccount {
    param([string]$AccountName)

    if ([string]::IsNullOrWhiteSpace($AccountName)) {
        return $true
    }

    $normalized = $AccountName.Trim().ToUpperInvariant()
    return $normalized -in @(
        "LOCALSYSTEM",
        "NT AUTHORITY\\SYSTEM",
        "LOCAL SERVICE",
        "NT AUTHORITY\\LOCAL SERVICE",
        "NETWORK SERVICE",
        "NT AUTHORITY\\NETWORK SERVICE"
    )
}

function Assert-ServicePrerequisites {
    param(
        [string]$BaseDirectory,
        [string]$AccountName
    )

    $isBuiltInAccount = Test-BuiltInServiceAccount -AccountName $AccountName
    $machineJwtSecret = [Environment]::GetEnvironmentVariable('CHRONIQ_JWT_SECRET', 'Machine')
    $machineEnvironment = [Environment]::GetEnvironmentVariable('DOTNET_ENVIRONMENT', 'Machine')
    $connectionString = Get-ConnectionStringFromAppSettings -BaseDirectory $BaseDirectory

    if ($isBuiltInAccount -and [string]::IsNullOrWhiteSpace($machineJwtSecret)) {
        throw @"
Machine-scoped environment variable 'CHRONIQ_JWT_SECRET' is missing.

The Windows service will run under a built-in account by default, so it cannot read user-scoped environment variables.
Run the backend wizard with machine scope before installing the service:

powershell -NoProfile -ExecutionPolicy Bypass -File .\run-config-wizard-backend.ps1 -Apply -UseMachineScope
"@
    }

    if (-not [string]::IsNullOrWhiteSpace($machineEnvironment) -and $machineEnvironment -ne 'Production') {
        Write-Warning "Machine DOTNET_ENVIRONMENT is '$machineEnvironment'. Windows service deployments are expected to run with DOTNET_ENVIRONMENT=Production."
    }
}

Assert-Administrator

$resolvedBinary = Resolve-ServiceBinaryPath -RequestedPath $BinaryPath

if (-not $SkipPrerequisiteChecks) {
    Assert-ServicePrerequisites -BaseDirectory ([System.IO.Path]::GetDirectoryName($resolvedBinary)) -AccountName $Username
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
    Invoke-Sc config $ServiceName start= delayed-auto | Out-Null
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
