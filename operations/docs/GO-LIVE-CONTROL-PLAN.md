# Go-Live Control Plan (Single Instance, ~20 Tasks/Day)

## Topology (Initial)
- 1 ASP.NET Core API + scheduler/worker process.
- SQL Server with daily backups and tested restore.
- Frontend served separately (Angular build artifact).
- Monitoring: application logs + /health/live + /health/ready.

## Restart Policy
- Run backend as a Windows Service.
- Configure auto-restart on first, second and subsequent failures.
- Reset service failure counter every 24h.
- Keep logs persisted under logs/ for post-mortem analysis.

## Health and Readiness
- Liveness endpoint: /health/live
- Readiness endpoint: /health/ready
- Readiness depends on:
  - database connectivity
  - worker pool operational state (queue pressure / stale executions)

## Host Permission Validation
- Service account can:
  - read application binaries and config
  - write logs/ directory
  - connect to SQL Server
  - execute approved task binaries/scripts paths only

## Controlled Rollout
- Deploy outside peak business hours.
- Run smoke gates and health checks post-deploy.
- Observe queue depth, stale executions, and failed tasks for 60 minutes.
- Roll back if readiness is Unhealthy for sustained period or failure rate spikes.

## Scale Plan (Next Phase)
- Introduce durable queue and distributed lock for multi-instance scheduling.
- Move from in-memory coordination to shared coordination (DB/Redis).
- Add instance-level metrics and per-instance task ownership.
