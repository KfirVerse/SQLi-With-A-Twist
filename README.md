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

The database (Microsoft SQL Server) runs on your **Windows** machine. The lab web app runs in **Docker** and connects to it. Install the tools once, seed the database, then one `docker compose up` starts the lab. Same steps for everyone — nothing to choose.

Why Windows: the final step uses `xp_cmdshell`, which only works on SQL Server on Windows.

```
[ Docker: web app ]  --- 1433 --->  [ Windows: SQL Server ]
  http://localhost:8080               StoreDb, VaultDb, the flag
```

**You need:** Windows 10/11 (a throwaway VM is perfect) and ~8 GB free disk. Everything else is installed below.

---

## 1. Install everything (winget — all from the terminal)

`winget` is the built-in Windows package manager (Windows 10/11). Open **Terminal / CMD as Administrator** and run these one by one:

```cmd
winget install -e --id Git.Git
winget install -e --id Docker.DockerDesktop
winget install -e --id Microsoft.SQLServer.2022.Developer
winget install -e --id Microsoft.SQLServerManagementStudio
```

That installs Git, Docker Desktop, SQL Server 2022 Developer (free), and SSMS.

Then:

- **Reboot** (Docker Desktop needs WSL2), then **start Docker Desktop** once and let it finish first-run setup (wait until the whale icon is steady).
- `sqlcmd` comes with SSMS. If step 4 says it's missing, find and install it: `winget search sqlcmd`.
- No `winget`? Install **App Installer** from the Microsoft Store, then reopen the terminal.
- Prefer clicking instead of winget? Direct downloads: [SQL Server Developer](https://www.microsoft.com/sql-server/sql-server-downloads) · [SSMS](https://aka.ms/ssms) · [Docker Desktop](https://www.docker.com/products/docker-desktop/) · [Git](https://git-scm.com/download/win).

The SQL Server install creates a default instance named **MSSQLSERVER**. Next, switch on network access.

---

## 2. Configure SQL Server (turn on TCP + SQL logins)

The web app runs in Docker, so SQL Server must accept TCP logins:

1. Open **SQL Server Configuration Manager** (installed with SQL Server; search the Start menu).
2. *SQL Server Network Configuration* → *Protocols for MSSQLSERVER* → right-click **TCP/IP** → **Enable**.
3. Double-click **TCP/IP** → *IP Addresses* tab → scroll to **IPAll** → set **TCP Port = 1433**.
4. Open **SSMS**, connect to `localhost` → right-click the server → *Properties* → *Security* → choose **"SQL Server and Windows Authentication mode"** → OK.
5. Back in Configuration Manager → *SQL Server Services* → right-click **SQL Server (MSSQLSERVER)** → **Restart**.
6. Open the firewall port (admin CMD):

```cmd
netsh advfirewall firewall add rule name="SQL 1433" dir=in action=allow protocol=TCP localport=1433
```

---

## 3. Get the code

```cmd
git clone https://github.com/KfirVerse/SQLi-With-A-Twist.git
cd SQLi-With-A-Twist
copy .env.example .env
```

`.env` holds fictional passwords — leave them as-is.

---

## 4. Build the database

Run the seed script with `sqlcmd`. Use the same `WEBAPP_PASSWORD` that's in `.env`:

```cmd
sqlcmd -S localhost -E -C -v WEBAPP_PASSWORD="W3bApp_Passw0rd!" -i db\init.sql
```

This creates `StoreDb` and `VaultDb`, the over-privileged `webapp` login (sysadmin), turns on `xp_cmdshell`, and writes the flag. You'll see `StoreDb ready` printed at the end.

Quick check that it worked:

```cmd
sqlcmd -S localhost -E -C -Q "SELECT name FROM sys.databases WHERE name IN ('StoreDb','VaultDb');"
```

---

## 5. Start the lab

Make sure Docker Desktop is running, then:

```cmd
docker compose up --build
```

Docker builds the web app and connects to your Windows database (`host.docker.internal:1433`). When it's up, open **http://localhost:8080**. Stop it later with `docker compose down`.

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

## If something doesn't work

- **`winget` not recognized** → install **App Installer** from the Microsoft Store, reopen the terminal.
- **Docker error / "daemon not running"** → start Docker Desktop, wait until it's green, retry.
- **`sqlcmd` not found** → `winget search sqlcmd` and install it, then reopen the terminal.
- **Web app can't reach the database:**
  - Is SQL Server listening on 1433? `sqlcmd -S localhost -E -C -Q "SELECT 1"` should print `1`.
  - Is mixed-mode auth on, and did you restart the service (step 2.4–2.5)?
  - Did the firewall rule get added (step 2.6)?
  - Does `WEBAPP_PASSWORD` in `.env` match what you seeded in step 4?

---

## Remediation (how it should have been built)

The lab stacks five weaknesses: unparameterized SQL, verbose error messages, no database isolation, an app login in `sysadmin`, and `xp_cmdshell` enabled. Fixes (per OWASP WSTG / CWE): parameterize and validate all input, return generic errors, isolate databases, give the app least privilege, and keep `xp_cmdshell` off.

---

## License

MIT — see [`LICENSE`](LICENSE). For education; use responsibly and only where you're authorized.
