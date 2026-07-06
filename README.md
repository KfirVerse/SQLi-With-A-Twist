# SQLi With A Twist

Intentionally vulnerable web app — a fake **"ad analytics" console**. One real way in. Find it, chain it, capture the flag. There's a twist, and a decoy planted to fool you.

Solution write-up (spoilers): [`steps.md`](steps.md) (English) · [`steps-hebrew.md`](steps-hebrew.md) (עברית).

---

## ⚠️ Read first

- Deliberately vulnerable. Run only on `localhost` or a throwaway VM. Never expose it.
- No real data. Everything is fictional.
- Authorized security education only.

---

## How it runs

The database (Microsoft SQL Server) runs on your **Windows** machine. The lab web app runs in **Docker** and connects to that database. Set up the database once, then one `docker compose up` starts the lab. Same steps for everyone — nothing to choose.

Why Windows: the final step uses `xp_cmdshell`, which only works on SQL Server on Windows.

```
[ Docker: web app ]  --- 1433 --->  [ Windows: SQL Server ]
  http://localhost:8080               StoreDb, VaultDb, the flag
```

---

## Requirements

- **Windows** (a throwaway VM is perfect).
- **SQL Server Developer Edition** (free) + **sqlcmd** (comes with SSMS).
- **Docker Desktop**.
- ~4 GB free disk.

---

## Setup — do these in order

### 1. Install SQL Server

Download **SQL Server 2022 (or 2019) Developer** — free: https://www.microsoft.com/sql-server/sql-server-downloads
Run the installer → **Basic**. Then install **SSMS** (it brings `sqlcmd`).

### 2. Let Docker reach the database

The web app is in Docker, so the database must accept TCP logins:

1. Open **SQL Server Configuration Manager** → *SQL Server Network Configuration* → *Protocols for MSSQLSERVER* → enable **TCP/IP**.
2. Still in TCP/IP → *IP Addresses* → *IPAll* → set **TCP Port = 1433**.
3. Open **SSMS** → right-click the server → *Properties* → *Security* → pick **"SQL Server and Windows Authentication mode"**.
4. Restart the **SQL Server (MSSQLSERVER)** service (Configuration Manager → *SQL Server Services*).
5. Allow inbound **TCP 1433** through Windows Firewall.

### 3. Get the code

```powershell
git clone https://github.com/KfirVerse/SQLi-With-A-Twist.git
cd SQLi-With-A-Twist
copy .env.example .env
```

`.env` holds fictional passwords. Leave them as-is.

### 4. Build the database

Run the seed script with `sqlcmd`. Use the same `WEBAPP_PASSWORD` that's in `.env`:

```powershell
sqlcmd -S localhost -E -C -v WEBAPP_PASSWORD="W3bApp_Passw0rd!" -i db\init.sql
```

This creates `StoreDb` and `VaultDb`, the over-privileged `webapp` login (sysadmin), turns on `xp_cmdshell`, and writes the flag. You'll see `StoreDb ready` printed at the end.

### 5. Start the lab

```powershell
docker compose up --build
```

Docker builds the web app and connects to your Windows database (`host.docker.internal:1433`). When it's up, open **http://localhost:8080**.

Stop it later with `docker compose down`.

---

## Check it works

- **http://localhost:8080** — the console (all data is fictional).
- **http://localhost:8080/inventory** — the price search.
- **http://localhost:8080/search?price=10** → `10,100,101,105,1000,1050`.

---

## The goal 🚩

Poke around. Only **one** input reaches the database — the rest is decoration. Get in, enumerate, escalate, and find the flag. Not every "flag" you find is the real one.

Solution (spoilers): [`steps.md`](steps.md) · [`steps-hebrew.md`](steps-hebrew.md).

---

## If the web app can't connect

- Is SQL Server listening on 1433? Run `Test-NetConnection localhost -Port 1433`.
- Is mixed-mode auth on, and did you restart the service?
- Does the firewall allow inbound 1433?
- Does `WEBAPP_PASSWORD` in `.env` match what you seeded in step 4?
- `sqlcmd` not found? Install SSMS or the SQL command-line tools.

---

## Remediation (how it should have been built)

The lab stacks five weaknesses: unparameterized SQL, verbose error messages, no database isolation, an app login in `sysadmin`, and `xp_cmdshell` enabled. Fixes (per OWASP WSTG / CWE): parameterize and validate all input, return generic errors, isolate databases, give the app least privilege, and keep `xp_cmdshell` off.

---

## License

MIT — see [`LICENSE`](LICENSE). For education; use responsibly and only where you're authorized.
