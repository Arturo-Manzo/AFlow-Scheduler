param(
    [switch]$SkipFrontend,
    [switch]$SkipBackendTests,
    [string]$BuildVersion
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path

Push-Location $root
try {
    Write-Host "[Gate] Building backend..." -ForegroundColor Cyan
    $buildArgs = @("build", ".\\AScheduler.csproj", "--configuration", "Release")
    if ($BuildVersion) {
        $buildArgs += "/p:Version=$BuildVersion"
    }
    & dotnet @buildArgs

    if (-not $SkipBackendTests) {
        Write-Host "[Gate] Running backend tests..." -ForegroundColor Cyan
        $testArgs = @("test", ".\\AScheduler.Tests\\AScheduler.Tests.csproj", "--configuration", "Release", "--no-restore")
        if ($BuildVersion) {
            $testArgs += "/p:Version=$BuildVersion"
        }
        & dotnet @testArgs
    }

    if (-not $SkipFrontend) {
        Write-Host "[Gate] Building frontend..." -ForegroundColor Cyan
        Push-Location .\frontend
        try {
            npm ci
            npm run build
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
