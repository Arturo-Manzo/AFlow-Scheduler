# Operations Hub

This folder centralizes operational artifacts that were previously scattered across the repository.

## Structure
- docs/
  - GO-NO-GO-CHECKLIST.md
  - GO-LIVE-CONTROL-PLAN.md
  - DATABASE-PRODUCTION-OPERATIONS.md
- scripts/
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
