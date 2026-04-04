###############################################################################
# AScheduler - Start Backend + Frontend
# Usage: .\start.ps1           -> Starts both backend and frontend
#        .\start.ps1 -Backend  -> Starts only the .NET backend
#        .\start.ps1 -Frontend -> Starts only the Angular frontend
###############################################################################

param(
    [switch]$Backend,
    [switch]$Frontend
)

$ProjectRoot = $PSScriptRoot

# If no flags specified, start both
if (-not $Backend -and -not $Frontend) {
    $Backend = $true
    $Frontend = $true
}

Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  AScheduler - Development Server Launcher"    -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

$jobs = @()

# --- Backend (.NET 8) ---
if ($Backend) {
    Write-Host "[Backend]  Starting .NET API (dotnet run)..." -ForegroundColor Green
    $backendJob = Start-Process -PassThru -NoNewWindow -FilePath "dotnet" `
        -ArgumentList "run", "--project", "$ProjectRoot\AScheduler.csproj" `
        -WorkingDirectory $ProjectRoot
    $jobs += $backendJob
    Write-Host "[Backend]  PID: $($backendJob.Id)" -ForegroundColor DarkGreen
}

# --- Frontend (Angular 21) ---
if ($Frontend) {
    Write-Host "[Frontend] Starting Angular (ng serve)..." -ForegroundColor Yellow
    $frontendJob = Start-Process -PassThru -NoNewWindow -FilePath "cmd.exe" `
        -ArgumentList "/c", "npm", "start" `
        -WorkingDirectory "$ProjectRoot\frontend"
    $jobs += $frontendJob
    Write-Host "[Frontend] PID: $($frontendJob.Id)" -ForegroundColor DarkYellow
}

Write-Host ""
Write-Host "---------------------------------------------" -ForegroundColor DarkGray
Write-Host "  Press Ctrl+C to stop all servers" -ForegroundColor DarkGray
Write-Host "---------------------------------------------" -ForegroundColor DarkGray
Write-Host ""

# Trap Ctrl+C to gracefully stop all processes
try {
    # Wait for any process to exit
    while ($true) {
        foreach ($job in $jobs) {
            if ($job.HasExited) {
                $name = if ($job.StartInfo.FileName -eq "dotnet") { "Backend" } else { "Frontend" }
                Write-Host "[$name] Process exited with code $($job.ExitCode)" -ForegroundColor Red
            }
        }
        
        # If all processes have exited, break
        if (($jobs | Where-Object { -not $_.HasExited }).Count -eq 0) {
            Write-Host "All processes have stopped." -ForegroundColor Red
            break
        }
        
        Start-Sleep -Seconds 2
    }
}
finally {
    Write-Host "`nShutting down..." -ForegroundColor Magenta
    foreach ($job in $jobs) {
        if (-not $job.HasExited) {
            $name = if ($job.StartInfo.FileName -eq "dotnet") { "Backend" } else { "Frontend" }
            Write-Host "  Stopping [$name] (PID: $($job.Id))..." -ForegroundColor DarkMagenta
            Stop-Process -Id $job.Id -Force -ErrorAction SilentlyContinue
        }
    }
    Write-Host "Done. Goodbye!" -ForegroundColor Cyan
}
