# Chroniq

Chroniq is a dependency-aware job orchestration platform built with ASP.NET Core and Angular.
It automates scheduled and on-demand task pipelines with parallel execution, execution history, RBAC, and operational observability.

## What You Get

- ASP.NET Core backend (`net8.0`) for orchestration APIs, execution control, and logging
- Angular frontend (`v21`) for administration and monitoring
- SQL Server-backed persistence and operational logs
- Release scripts for smoke checks, infrastructure checks, and environment configuration

## Quick Start (Source)

### Prerequisites

- .NET SDK 8.x
- Node.js 20+
- npm 11+
- SQL Server / LocalDB for backend data

### 1) Build Backend

```powershell
dotnet build .\AScheduler.csproj
```

### 2) Run Backend

```powershell
dotnet run --project .\AScheduler.csproj
```

### 3) Run Frontend

```powershell
Set-Location .\frontend
npm install
npm run start
```

## Configure Release Artifacts

The release pipeline ships plug-and-play artifacts. Each ZIP now includes:

- Matching config wizard (`run-config-wizard-backend.ps1` or `run-config-wizard-frontend.ps1`)
- Full operations toolkit under `operations/` (`scripts/`, `docs/`, and `operations/README.md`)
- Release also includes `CHECKSUMS.sha256` for artifact integrity verification

Run each script from its artifact folder.

### Verify Artifact Integrity

Before executing release artifacts, validate SHA256 checksums against `CHECKSUMS.sha256` from the same release.

```powershell
Get-FileHash -Algorithm SHA256 .\CHRONIQ-backend-vX.Y.Z.zip
Get-FileHash -Algorithm SHA256 .\CHRONIQ-frontend-vX.Y.Z.zip
```

Both hashes must match the entries in `CHECKSUMS.sha256`.

### Backend Wizard

Use this to configure backend `appsettings.Production.json`, Kestrel URL, connection string, and backend environment variables.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\run-config-wizard-backend.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\run-config-wizard-backend.ps1 -Apply
```

Optional machine scope for environment variables:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\run-config-wizard-backend.ps1 -Apply -UseMachineScope
```

### Backend Windows Service

Install the published backend as a Windows Service from an elevated PowerShell session:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\operations\scripts\infra\install-windows-service.ps1 -ServiceName CHRONIQ -BinaryPath .\AScheduler.exe
```

Use `-Force` to update an existing service after deploying a new release. The script sets delayed automatic startup and configures Windows to restart the service after crashes.

### Frontend Wizard

Use this to configure frontend runtime `config.json` for IIS static hosting (backend API URL).

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\run-config-wizard-frontend.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\run-config-wizard-frontend.ps1 -Apply
```

## Operations Reference

Operational docs and scripts are under:

- `operations/README.md`
- `operations/docs/`
- `operations/scripts/`

## Notes

- Backend bind URL priority is controlled by Kestrel endpoint settings in appsettings.
- The backend wizard updates Kestrel URL in appsettings (instead of writing `ASPNETCORE_URLS`).
- Frontend release artifact is IIS-ready static output from `frontend/dist/frontend/browser`.
- Frontend wizard remains relevant because it updates `config.json` without requiring a rebuild.
