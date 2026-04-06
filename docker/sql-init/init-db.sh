#!/usr/bin/env bash
set -euo pipefail

if [[ -z "${SA_PASSWORD:-}" ]]; then
  echo "SA_PASSWORD is not set"
  exit 1
fi

DB_NAME="${SQLSERVER_DB:-CHRONIQDB}"
HOST="${SQLSERVER_HOST:-sqlserver}"
PORT="1433"

if [[ -x "/opt/mssql-tools18/bin/sqlcmd" ]]; then
  SQLCMD_BIN="/opt/mssql-tools18/bin/sqlcmd"
elif [[ -x "/opt/mssql-tools/bin/sqlcmd" ]]; then
  SQLCMD_BIN="/opt/mssql-tools/bin/sqlcmd"
else
  echo "sqlcmd binary not found in expected paths"
  exit 1
fi

SQLCMD="${SQLCMD_BIN} -S ${HOST},${PORT} -U sa -P ${SA_PASSWORD}"

echo "Waiting for SQL Server at ${HOST}:${PORT}..."
for i in $(seq 1 60); do
  if ${SQLCMD} -Q "SELECT 1" >/dev/null 2>&1; then
    echo "SQL Server is ready"
    break
  fi
  sleep 2
  if [[ $i -eq 60 ]]; then
    echo "SQL Server did not become ready in time"
    exit 1
  fi
done

echo "Ensuring database '${DB_NAME}' exists..."
${SQLCMD} -Q "IF DB_ID(N'${DB_NAME}') IS NULL CREATE DATABASE [${DB_NAME}];"

run_script() {
  local script_path="$1"
  echo "Running $(basename "${script_path}")"
  "${SQLCMD_BIN}" -S "${HOST},${PORT}" -U sa -P "${SA_PASSWORD}" -d "${DB_NAME}" -i "${script_path}"
}

run_script /database/010_Create_Security.sql
run_script /database/020_Create_Departments_And_Boxes.sql
run_script /database/030_Create_Tasks_And_Dependencies.sql
run_script /database/040_Create_Executions_And_Logs.sql
run_script /database/050_Create_Audit_And_View.sql
run_script /database/060_Create_Notification_Settings.sql
run_script /database/070_Create_Application_Logs.sql
run_script /database/080_Optimize_Execution_Queries.sql
run_script /database/090_Validate_Integrity_And_Continuity.sql

echo "Database initialization completed"
