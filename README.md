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
dotnet build .\CHRONIQ.csproj
```

### 2) Run Backend

```powershell
dotnet run --project .\CHRONIQ.csproj
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
powershell -NoProfile -ExecutionPolicy Bypass -File .\run-config-wizard-backend.ps1 -Apply -UseMachineScope
powershell -NoProfile -ExecutionPolicy Bypass -File .\operations\scripts\infra\install-windows-service.ps1 -ServiceName CHRONIQ
```

Use `-Force` to update an existing service after deploying a new release. The script sets delayed automatic startup and configures Windows to restart the service after crashes.

Important:

- The service runs in `Production`, so `CHRONIQ_JWT_SECRET` must exist at machine scope when using the default built-in service account.
- The installer now validates this and stops early with guidance instead of surfacing a generic Windows error 1053.
- Avoid `LocalDB` for built-in service accounts. Prefer a SQL Server instance, or install the service with a specific user account that owns the LocalDB instance.
- When running as a Windows Service, rolling file logs are written to `%ProgramData%\CHRONIQ\logs` to avoid write-permission failures under protected install folders.

### Frontend Wizard

Use this to configure frontend runtime `config.json` for IIS static hosting (backend API URL).

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\run-config-wizard-frontend.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\run-config-wizard-frontend.ps1 -Apply
```

### In-Place Upgrade

Use the upgrade script to update an existing installation without overwriting its operational configuration values. The release package contributes new keys and defaults through versioned templates, while the installed environment keeps its current values such as connection strings, Kestrel URL, CORS origins, SMTP settings, and frontend backend URL.

Preview:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\operations\scripts\upgrade\upgrade-installation.ps1 -InstallPath "D:\CHRONIQ" -PackagePath "D:\release\CHRONIQ-backend-vX.Y.Z" -Preview
```

Apply:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\operations\scripts\upgrade\upgrade-installation.ps1 -InstallPath "D:\CHRONIQ" -PackagePath "D:\release\CHRONIQ-backend-vX.Y.Z" -Apply
```

Optional frontend package/install paths can be provided when frontend files are hosted separately.

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
