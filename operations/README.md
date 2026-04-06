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
