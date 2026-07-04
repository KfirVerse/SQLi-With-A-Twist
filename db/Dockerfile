# ============================================================================
#  Database image for the MSSQL "SQLi -> RCE" lab.
#
#  IMPORTANT (platform constraint):
#  xp_cmdshell is NOT supported on SQL Server on Linux until 2019 CU21.
#  Earlier images (e.g. the CU14 build 15.0.4188.2 used in the original
#  Windows write-up) fail init.sql with:
#      "The specified option 'xp_cmdshell' is not supported by this edition"
#  So for the RCE step to work on Linux/Docker we must use CU21 or later.
#  `2019-latest` (currently CU32, build 15.0.44xx) is used here to guarantee
#  xp_cmdshell support. @@version still leaks fine — the exact build is
#  cosmetic. To pin instead, use e.g. mcr.microsoft.com/mssql/server:2019-CU31-ubuntu-20.04
#  (any tag >= CU21). Do NOT use a pre-CU21 tag if you want the RCE step.
# ============================================================================
FROM mcr.microsoft.com/mssql/server:2019-latest

USER root

# Install the SQL command-line tools (sqlcmd) so the entrypoint can seed the DB.
# Some images already bundle these; installing is safe and guarantees v18 exists.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl gnupg ca-certificates apt-transport-https \
    && curl -sSL https://packages.microsoft.com/keys/microsoft.asc \
        | gpg --dearmor -o /etc/apt/trusted.gpg.d/microsoft.gpg \
    && curl -sSL https://packages.microsoft.com/config/ubuntu/20.04/prod.list \
        -o /etc/apt/sources.list.d/mssql-release.list \
    && apt-get update \
    && ACCEPT_EULA=Y apt-get install -y --no-install-recommends mssql-tools18 unixodbc \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

ENV PATH="/opt/mssql-tools18/bin:${PATH}"

COPY entrypoint.sh /usr/local/bin/entrypoint.sh
COPY init.sql      /usr/local/bin/init.sql
RUN chmod +x /usr/local/bin/entrypoint.sh \
    && chown mssql:root /usr/local/bin/entrypoint.sh /usr/local/bin/init.sql

# Drop back to the unprivileged account the base image runs SQL Server under.
USER mssql

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
