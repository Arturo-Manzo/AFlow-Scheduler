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

## Config Wizard
- Backend wizard (appsettings + env vars):
  - `powershell -NoProfile -ExecutionPolicy Bypass -File operations/scripts/config/run-config-wizard-backend.ps1`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File operations/scripts/config/run-config-wizard-backend.ps1 -Apply`
- Frontend wizard (frontend/public/config.json):
  - `powershell -NoProfile -ExecutionPolicy Bypass -File operations/scripts/config/run-config-wizard-frontend.ps1`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File operations/scripts/config/run-config-wizard-frontend.ps1 -Apply`
- Optional machine-scope environment variables (backend):
  - `...run-config-wizard-backend.ps1 -Apply -UseMachineScope`
