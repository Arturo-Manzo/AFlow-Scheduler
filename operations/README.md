# Operations Hub

This folder centralizes operational artifacts that were previously scattered across the repository.

## Structure
- docs/
  - GO-NO-GO-CHECKLIST.md
  - GO-LIVE-CONTROL-PLAN.md
  - DATABASE-PRODUCTION-OPERATIONS.md
- scripts/
  - config/
    - run-config-wizard-backend.ps1
    - run-config-wizard-frontend.ps1
  - upgrade/
    - upgrade-installation.ps1
  - release/
    - smoke-gates.ps1
  - infra/
    - install-windows-service.ps1
    - preflight-host-check.ps1
  - database/
    - backup-database.ps1
    - restore-database.ps1
  - dev/
    - start.ps1
  - smtp/
    - test-smtp.ps1

## Notes
- GitHub Actions workflow remains under .github/workflows by platform requirement.
- SQL schema and migration scripts remain under Database/ because they are part of application data model versioning.

## Release Integrity
- The release workflow publishes CHRONIQ backend and frontend ZIP artifacts plus CHECKSUMS.sha256.
- The release workflow also publishes SIGNING-STATUS.txt so operators can confirm whether binaries were signed in that run.
- Verify download integrity before execution:
  - `Get-FileHash -Algorithm SHA256 .\CHRONIQ-backend-vX.Y.Z.zip`
  - `Get-FileHash -Algorithm SHA256 .\CHRONIQ-frontend-vX.Y.Z.zip`
  - Compare values with CHECKSUMS.sha256 from the same release.
- Optional code-signing gate is controlled by repository variable RELEASE_SIGNING_ENABLED.
  - When true and secrets are configured, backend binaries are signed and verified.
  - When true but secrets are missing/invalid, release continues unsigned and SIGNING-STATUS.txt records the reason.

## Config Wizard
- Backend wizard (appsettings + env vars):
  - `powershell -NoProfile -ExecutionPolicy Bypass -File operations/scripts/config/run-config-wizard-backend.ps1`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File operations/scripts/config/run-config-wizard-backend.ps1 -Apply`
- Frontend wizard (frontend/public/config.json):
  - `powershell -NoProfile -ExecutionPolicy Bypass -File operations/scripts/config/run-config-wizard-frontend.ps1`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File operations/scripts/config/run-config-wizard-frontend.ps1 -Apply`
- Optional machine-scope environment variables (backend):
  - `...run-config-wizard-backend.ps1 -Apply -UseMachineScope`

## Windows Service
- Install or update the backend service from an elevated PowerShell session:
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\run-config-wizard-backend.ps1 -Apply -UseMachineScope`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File operations/scripts/infra/install-windows-service.ps1 -ServiceName CHRONIQ`
    - If you omit `-BinaryPath`, the installer prompts for the `.exe` location and suggests detected candidates.
  - Add `-Force` to update an existing service definition after deploying a new binary.
- Default startup is delayed automatic, with restart-on-failure configured for crashes.
- The default built-in service account cannot read user-scoped `CHRONIQ_JWT_SECRET`, and it should not use `LocalDB`.
- Service file logs are written to `%ProgramData%\CHRONIQ\logs`.
- If scheduled tasks access UNC shares, run the service under a domain account that has direct permissions:
  - `...install-windows-service.ps1 -ServiceName CHRONIQ -BinaryPath "D:\Deploy\CHRONIQ\CHRONIQ.exe" -Username "DOMAIN\svc-chroniq" -Password "..." -Force`

## Upgrade
- Use `operations/scripts/upgrade/upgrade-installation.ps1` for in-place upgrades of existing installations.
- The script:
  - creates timestamped backups of backend/frontend config plus service metadata
  - merges installed config with versioned templates so new keys are added without replacing existing values
  - applies pending SQL migrations tracked in `dbo.SchemaMigrations`
  - updates backend binaries and refreshes the Windows service with `-Force`
  - writes an upgrade log under `%ProgramData%\CHRONIQ\upgrade-logs`
- Preview example:
  - `powershell -NoProfile -ExecutionPolicy Bypass -File operations/scripts/upgrade/upgrade-installation.ps1 -InstallPath "D:\CHRONIQ" -PackagePath "D:\release\CHRONIQ-backend-vX.Y.Z" -Preview`
- Apply example:
  - `powershell -NoProfile -ExecutionPolicy Bypass -File operations/scripts/upgrade/upgrade-installation.ps1 -InstallPath "D:\CHRONIQ" -PackagePath "D:\release\CHRONIQ-backend-vX.Y.Z" -Apply`
