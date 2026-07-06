# Windows setup — database on Windows (enables the full RCE chain)

`xp_cmdshell` is **not supported on SQL Server on Linux**, so the all-Docker lab (`docker-compose.yml`)
can't run the final command-execution step. To reproduce the write-up end to end — enable
`xp_cmdshell` through the injection and run OS commands — run the **database on Windows**.

There is no official Windows *container* image for SQL Server 2019/2022, so "DB on Windows" means
installing **SQL Server Developer Edition** (free) on your Windows host. The web app still runs in
Docker and connects to the host database.

> This is the detailed version of **Path A** in the [README](../README.md). Same safety rules:
> localhost only, fictional data, authorized learning.

---

## 1. Install SQL Server Developer Edition

1. Download **SQL Server 2019 (or 2022) Developer** — free:
   https://www.microsoft.com/sql-server/sql-server-downloads
2. Run the installer → **Basic** (or Custom). Note the instance name (default `MSSQLSERVER`).
3. Install **SQL Server Management Studio (SSMS)** or the `sqlcmd` tools if you don't have them.

## 2. Enable SQL auth + TCP (so the Docker web app can log in)

1. **SQL Server Configuration Manager** → *SQL Server Network Configuration* →
   *Protocols for MSSQLSERVER* → enable **TCP/IP**. In TCP/IP → *IP Addresses* → *IPAll*, set
   **TCP Port = 1433**. Restart the *SQL Server (MSSQLSERVER)* service.
2. Enable **Mixed Mode** auth: in SSMS → server *Properties* → *Security* →
   "SQL Server and Windows Authentication mode" → OK → restart the service.
3. Allow inbound TCP **1433** through Windows Firewall (so the Docker container can reach the host).

## 3. Seed the database

From the repo folder, using `sqlcmd` (Windows auth `-E`, or `-U sa -P <sa-pw>`):

```cmd
sqlcmd -S localhost -E -C -v WEBAPP_PASSWORD="W3bApp_Passw0rd!" -i db\init.sql
```

Use the same `WEBAPP_PASSWORD` value that's in your `.env`. The script:
- creates `StoreDb` (~55 products) and `VaultDb` (with the decoy `Flag` table),
- creates login `webapp` and adds it to **sysadmin**,
- **enables `xp_cmdshell`** (works on Windows), and
- writes the real flag to **`C:\Users\Public\Flag.txt`** via `xp_cmdshell`.

## 4. Start the web app (Docker) pointed at the host DB

```cmd
copy .env.example .env   & rem if you don't have .env yet
docker compose -f docker-compose.windows.yml up --build
```

Browse to **http://localhost:8080**. If the web container can't connect, re-check step 2
(TCP 1433 + firewall) and that `WEBAPP_PASSWORD` matches what you seeded.

> No .NET SDK? This runs the app in Docker. If you'd rather run it on the host and have the .NET 8
> SDK: `cd web && set ConnectionStrings__StoreDb=Server=localhost,1433;Database=StoreDb;User ID=webapp;Password=...;Encrypt=False;TrustServerCertificate=True && dotnet run`.

---

## Now exploit it

With the database on Windows, the full chain works — including `xp_cmdshell` and the real flag at
`C:\Users\Public\Flag.txt`. The complete, step-by-step exploitation (identical to the write-up) is in:

- [`../steps.md`](../steps.md) — English
- [`../steps-hebrew.md`](../steps-hebrew.md) — עברית

Windows command notes: use `whoami`, `dir` (not `ls`), `type` (not `cat`).

---

## Notes

- The all-Docker lab (`docker-compose.yml`) is fine for the SQL injection and database enumeration,
  but **not** the `xp_cmdshell` RCE step or the real on-disk flag — those need this Windows setup.
- The decoy `VaultDb.dbo.Flag` (reachable by SQLi) only taunts; the real flag requires command execution.
