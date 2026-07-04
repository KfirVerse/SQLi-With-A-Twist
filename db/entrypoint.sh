#!/usr/bin/env bash
# ============================================================================
#  MSSQL "SQLi -> RCE" Lab - database container entrypoint
#  Boots SQL Server, waits until it is accepting connections, then applies
#  init.sql (idempotent).  Finally hands control back to the sqlservr process
#  so the container stays alive.
# ============================================================================
set -euo pipefail

SA_PASSWORD="${MSSQL_SA_PASSWORD:-${SA_PASSWORD:-}}"
WEBAPP_PASSWORD="${WEBAPP_PASSWORD:-}"
INIT_SQL="/usr/local/bin/init.sql"

if [[ -z "${SA_PASSWORD}" ]]; then
    echo "ERROR: MSSQL_SA_PASSWORD (or SA_PASSWORD) is not set." >&2
    exit 1
fi
if [[ -z "${WEBAPP_PASSWORD}" ]]; then
    echo "ERROR: WEBAPP_PASSWORD is not set." >&2
    exit 1
fi

# ---- Locate sqlcmd (prefer v18, fall back to v17) --------------------------
SQLCMD=""
SQLCMD_TLS=()
for candidate in /opt/mssql-tools18/bin/sqlcmd /opt/mssql-tools/bin/sqlcmd; do
    if [[ -x "${candidate}" ]]; then
        SQLCMD="${candidate}"
        # mssql-tools18's sqlcmd encrypts by default and needs -C to trust the
        # container's self-signed certificate.
        [[ "${candidate}" == *tools18* ]] && SQLCMD_TLS=(-C)
        break
    fi
done
if [[ -z "${SQLCMD}" ]]; then
    echo "ERROR: sqlcmd not found in the image." >&2
    exit 1
fi
echo "Using sqlcmd: ${SQLCMD} ${SQLCMD_TLS[*]:-}"

# ---- Start SQL Server in the background ------------------------------------
echo "Starting SQL Server..."
/opt/mssql/bin/sqlservr &
SQL_PID=$!

# ---- Wait until SQL Server is ready ----------------------------------------
echo "Waiting for SQL Server to accept connections..."
for i in $(seq 1 60); do
    if "${SQLCMD}" -S localhost -U sa -P "${SA_PASSWORD}" "${SQLCMD_TLS[@]}" \
            -l 2 -Q "SELECT 1" >/dev/null 2>&1; then
        echo "SQL Server is up (after ${i} attempt(s))."
        break
    fi
    if [[ "${i}" -eq 60 ]]; then
        echo "ERROR: SQL Server did not become ready in time." >&2
        exit 1
    fi
    sleep 2
done

# ---- Seed the database (idempotent) ----------------------------------------
echo "Applying ${INIT_SQL} ..."
"${SQLCMD}" -S localhost -U sa -P "${SA_PASSWORD}" "${SQLCMD_TLS[@]}" \
    -b -v WEBAPP_PASSWORD="${WEBAPP_PASSWORD}" -i "${INIT_SQL}"
echo "Database initialization finished."

# ---- Keep the container alive on the SQL Server process --------------------
wait "${SQL_PID}"
