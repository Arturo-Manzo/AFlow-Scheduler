param(
    [switch]$SkipFrontend,
    [switch]$SkipBackendTests,
    [string]$BuildVersion
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path

function Invoke-GateCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
    }
}

Push-Location $root
try {
    Write-Host "[Gate] Building backend..." -ForegroundColor Cyan
    $buildArgs = @("build", ".\\CHRONIQ.csproj", "--configuration", "Release")
    if ($BuildVersion) {
        $buildArgs += "/p:Version=$BuildVersion"
    }
    Invoke-GateCommand dotnet @buildArgs

    if (-not $SkipBackendTests) {
        Write-Host "[Gate] Running backend tests..." -ForegroundColor Cyan
        $testArgs = @("test", ".\\CHRONIQ.Tests\\CHRONIQ.Tests.csproj", "--configuration", "Release", "--no-restore")
        if ($BuildVersion) {
            $testArgs += "/p:Version=$BuildVersion"
        }
        Invoke-GateCommand dotnet @testArgs
    }

    if (-not $SkipFrontend) {
        Write-Host "[Gate] Building frontend..." -ForegroundColor Cyan
        Push-Location .\frontend
        try {
            Invoke-GateCommand npm ci

            $designSystemPath = ".\projects\ui-design-system"
            if (Test-Path (Join-Path $designSystemPath "angular.json")) {
                Write-Host "[Gate] Building ui-design-system..." -ForegroundColor Cyan
                $designSystemPackage = Join-Path $designSystemPath "projects\ui-design-system\ng-package.json"
                $designSystemArgs = @("ng-packagr", "-p", $designSystemPackage)
                Invoke-GateCommand npx @designSystemArgs
            }

            Invoke-GateCommand npm run build
        }
        finally {
            Pop-Location
        }
    }

    Write-Host "[Gate] Smoke gates completed successfully." -ForegroundColor Green
}
finally {
    Pop-Location
}
