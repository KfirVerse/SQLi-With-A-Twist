# MSSQL SQLi → RCE Lab

A self-contained, intentionally vulnerable lab that reproduces a **SQL injection → remote code execution** finding against **Microsoft SQL Server**, for hands-on learning and write-ups. One command starts a realistic ASP.NET Core web app (an "ad analytics" console) plus a seeded SQL Server 2019 instance, and the whole exploit chain — from injection to error-based extraction to `xp_cmdshell` RCE — is reproducible.

---

## ⚠️ Safety & scope — read first

- This app is **deliberately vulnerable**. Run it **only on `localhost` / an isolated Docker network**. Both containers publish to `127.0.0.1` only. **Never expose it to the internet or a shared network.**
- It contains **no real data** — every product, name, and credential is **fictional**.
- Purpose: **authorized security education** and reproducing an already-remediated finding. Do not point these techniques at systems you are not authorized to test.
- The credentials in `.env.example` are throwaway lab values, safe to publish because the stack is localhost-only. `.env` itself is git-ignored.

---

## What you need

- **Docker Desktop** (with Docker Compose v2 — bundled). On Windows it uses the WSL2 backend automatically; nothing else to install.
- ~2.5 GB free disk for the SQL Server image.

Check it's ready:

```bash
docker --version
docker compose version
```

---

## Quick start

```bash
git clone https://github.com/<your-username>/mssql-sqli-to-rce-lab.git
cd mssql-sqli-to-rce-lab
cp .env.example .env        # Windows CMD: copy .env.example .env
docker compose up --build
```

First boot pulls the SQL Server image and seeds the database (a minute or two). When the logs show the web app listening, open:

- **http://localhost:8080** — the dashboard (fictional "AdVantage" ad console)
- **http://localhost:8080/inventory** — the product/price search UI
- **http://localhost:8080/search?price=10** — the raw vulnerable API (returns matching prices as CSV)

Stop it:

```bash
docker compose down          # stop
docker compose down -v        # stop and wipe the database
```

---

## What it demonstrates

The site looks like a normal analytics console. The **only** database-backed surface is the inventory price search, and it's injectable:

```
GET /search?price=<value>
```

Server-side (see `web/Pages/Search.cshtml.cs`), the `price` value is concatenated straight into the SQL — no parameters, no validation:

```csharp
// VULNERABLE ON PURPOSE
var sql = $"SELECT TOP 100 price FROM dbo.products " +
          $"WHERE active = 1 AND CAST(price AS varchar(30)) LIKE '{price}%'";
```

From that one flaw the lab walks the full chain:

1. **Detection** — `price=10'` returns a 500 with a raw SQL error ("Unclosed quotation mark…"), confirming injection.
2. **Boolean-blind** — `' OR 1=1--` returns all prices, `' OR 1=2--` returns none (both HTTP 200).
3. **Error-based extraction** — the selected column is numeric, so pushing a string through `CONVERT(int, …)` throws a 500 whose message **contains the value**. Leaks `@@version`, `DB_NAME()`, `SYSTEM_USER`, table/column/database names via `STRING_AGG`.
4. **Remote code execution** — stacked queries run `xp_cmdshell`, capture the output into a table, and exfiltrate it through the same error channel. Works because the web login is a **`sysadmin`** and `xp_cmdshell` is enabled.

> The API returns numbers (prices) directly, but strings must be pulled through the error channel — the classic MSSQL error-based pattern.

**Exploitation guide (copy-paste payloads + sqlmap for each step):**
[`docs/price-injection-cheatsheet.md`](docs/price-injection-cheatsheet.md)

**Full end-to-end RCE walkthrough:**
[`docs/exploitation-walkthrough.md`](docs/exploitation-walkthrough.md)

> **RCE identity note:** SQL Server runs **on Linux** here, so `xp_cmdshell` executes as the **`mssql`** service account (`whoami` → `mssql`). On a real **Windows** host the same technique typically yields `nt authority\system`.

---

## The three deliberate misconfigurations

| Layer | Misconfiguration | Where |
|-------|------------------|-------|
| App code | `price` concatenated into SQL, no parameters/validation | `web/Pages/Search.cshtml.cs` |
| App config | Verbose errors on — raw `SqlException` text returned to the client | `web/Program.cs` |
| Database | App login `webapp` is in the `sysadmin` role; `xp_cmdshell` enabled | `db/init.sql` |

---

## Architecture

```
┌────────────────────────┐        ┌────────────────────────────────┐
│  web  (ASP.NET Core 8)  │  1433  │  db  (SQL Server 2019, CU21+)  │
│  Kestrel :8080          ├───────▶│  login: webapp  (sysadmin)     │
│  /inventory  (price UI) │        │  db: StoreDb / dbo.products     │
│  /search     (vuln API) │        │  xp_cmdshell: ENABLED           │
│  minimal raw SQL errors │        │  ~55 fictional products         │
└──────────┬─────────────┘        └────────────────────────────────┘
           │ 127.0.0.1:8080                       127.0.0.1:1433
           ▼
        browser / Burp Suite / sqlmap
```

- **web** — .NET 8 Razor Pages, `Microsoft.Data.SqlClient`, Kestrel on `:8080`. Dashboard, campaigns and reports pages use static fictional data (not injectable); only `/search` touches the database. Optionally spoofs `Server: Microsoft-IIS/10.0` / `X-Powered-By: ASP.NET` headers (`FakeIisHeaders`) so Burp captures look like a Windows/.NET target — cosmetic only.
- **db** — official SQL Server 2019 image, `2019-latest`. `xp_cmdshell` is only supported on SQL Server *on Linux* from **2019 CU21** onward, so a pre-CU21 image can't run the RCE step; `2019-latest` (CU32) is used to guarantee it. Seeded on boot with round-number product prices, the over-privileged `webapp` login, and `xp_cmdshell` enabled.

---

## Repository layout

```
mssql-sqli-to-rce-lab/
├── README.md                          # this file
├── docker-compose.yml                 # db + web, localhost-only
├── .env.example                       # copy to .env; fictional passwords
├── .gitignore                         # ignores .env, bin/, obj/
├── LICENSE                            # MIT
├── db/
│   ├── Dockerfile                     # SQL Server 2019 (CU21+) + sqlcmd
│   ├── entrypoint.sh                  # boot SQL, wait, apply init.sql
│   └── init.sql                       # schema, ~55 products, webapp sysadmin, xp_cmdshell
├── web/
│   ├── Dockerfile
│   ├── SqliRceLab.csproj
│   ├── Program.cs                     # minimal verbose-error handler + fake IIS headers
│   ├── appsettings.json
│   └── Pages/
│       ├── Index / Campaigns / Reports  # decorative dashboard (safe)
│       ├── Inventory.cshtml(.cs)       # price search UI
│       └── Search.cshtml.cs            # the vulnerable /search API
└── docs/
    ├── price-injection-cheatsheet.md   # step-by-step payloads + sqlmap
    └── exploitation-walkthrough.md     # full RCE walkthrough
```

---

## Remediation (how it should have been built)

1. **Parameterize and validate.** Never concatenate input. Reject non-numeric `price`, use a parameter:
   ```csharp
   using var cmd = new SqlCommand(
       "SELECT TOP 100 price FROM dbo.products WHERE active = 1 AND price = @price", conn);
   cmd.Parameters.Add("@price", SqlDbType.Int).Value = price;
   ```
2. **Least privilege.** The app login must never be `sysadmin` — grant only `SELECT` on the needed tables.
3. **Keep `xp_cmdshell` disabled** (`EXEC sp_configure 'xp_cmdshell', 0; RECONFIGURE;`).
4. **Don't leak errors in production** — return a generic error page, log details server-side.

---

## Publishing to GitHub

`.env` is git-ignored, so no secrets are committed. From the repo folder:

```bash
git init
git add .
git commit -m "MSSQL SQLi-to-RCE lab"
git branch -M main
git remote add origin https://github.com/<your-username>/mssql-sqli-to-rce-lab.git
git push -u origin main
```

Create the empty repo first at https://github.com/new (or with the GitHub CLI: `gh repo create mssql-sqli-to-rce-lab --public --source=. --push`).

---

## Troubleshooting

- **`xp_cmdshell … not supported by this edition` in the db logs** — you're on a pre-CU21 image; `db/Dockerfile` uses `2019-latest` to avoid this.
- **Login failed for `webapp`** — ensure `.env` exists and passwords have no single quotes; recreate with `docker compose down -v` then `up --build`.
- **Port already in use** — change the host side of the port mappings in `docker-compose.yml` (e.g. `127.0.0.1:18080:8080`).
- **Burp can't start its proxy on 8080** — the lab uses 8080; point Burp's proxy listener at a different port, or use Burp Repeater (which connects directly to the target).

---

## License

MIT — see [`LICENSE`](LICENSE). Provided for education; use responsibly and only where authorized.
