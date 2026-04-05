# Go/No-Go Checklist

## 1) Build and Test Gates
- Backend build in Release mode completed successfully.
- Backend tests completed successfully.
- Frontend build completed successfully.
- Release smoke-gates script completed successfully.

## 2) Security and Compliance Gates
- JWT secret comes from environment or secret store in production.
- CORS origins are explicit and production-safe.
- HTTPS pipeline consistency validated (HttpsRedirection vs CORS origin schemes).
- Department isolation checks validated for sensitive endpoints.
- No plaintext credentials committed in repository.

## 3) Data and Continuity Gates
- Latest backup is available and restore was validated.
- Integrity script 090_Validate_Integrity_And_Continuity.sql passes.
- Execution query optimization script 080 has been applied in staging/prod.

## 4) Observability and Operations Gates
- /health/live endpoint returns Healthy.
- /health/ready endpoint returns Healthy or Degraded with accepted rationale.
- Alert channels are active for stale executions and queue growth.
- Runbooks for DB, SMTP, and stuck tasks are available to on-call.

## 5) Release Decision
- Owner:
- Date/Time (UTC):
- Decision: GO / NO-GO
- Notes:
