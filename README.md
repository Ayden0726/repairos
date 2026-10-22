# WorkshopOS

Self-hosted repair-shop operations for Australian electronics / computer repair businesses.

**Current version: 1.2.35** (API + Windows client) · Repo: [Ayden0726/repairos](https://github.com/Ayden0726/repairos)

Australian English, **AUD**, **GST**, financial year **July–June**.

## What it is

WorkshopOS runs your shop’s day-to-day work on **your own server**:

- Repair tickets, customers, devices, quotes, invoices, inventory, purchasing, refurbished stock, calendar, reports, backups, and staff roles
- **Business expenses** — GST treatments, suppliers, recurring drafts, CSV export for your accountant ([docs/SETTINGS_AND_SECURITY.md](docs/SETTINGS_AND_SECURITY.md#using-expenses))
- **Encrypted API credentials** under **Settings → Integrations** — WooCommerce, POS/Square, OpenAI, Twilio, SMTP, IMEI, shipping ([docs/SETTINGS_AND_SECURITY.md](docs/SETTINGS_AND_SECURITY.md))
- **POS register** (admin-enabled) with AU EFTPOS guidance — [docs/POS_EFTPOS.md](docs/POS_EFTPOS.md)
- **WordPress / 10Web website API** — WorkshopOS is the source of truth for public quotes, bookings, Sell Your Tech, customer portal, and refurbished listings ([docs/WORDPRESS_INTEGRATION.md](docs/WORDPRESS_INTEGRATION.md))
- A **Windows desktop app** on each shop PC
- A **central server** (Docker) on a Linux PC, NAS, VM, WSL, or **Raspberry Pi 3B+** ([docs/RASPBERRY_PI.md](docs/RASPBERRY_PI.md))

Nothing is required from a SaaS cloud — data stays on your machine / LAN.

### What’s new in 1.2.35

| Area | Change |
| --- | --- |
| Security | AES-256-GCM credential store (`integration_credentials`); dedicated `Encryption__Key` (separate from JWT) |
| Settings | Fixes for settings / API keys appearing to “reset”; no silent wipe of secrets; concurrency-safe saves |
| Expenses | Full expenses module (GST-aware, recurring, reports, WinUI) |
| Raspberry Pi | Multi-arch Docker images + low-memory compose overlay + swap helper for Pi 3B+ |
| GitHub sync | One-shot PowerShell: overlay → push `main` + tag → client zip + `Setup.exe` |
| Version | API + client unified at **1.2.35**; connect banner no longer stuck on 1.2.25 |
| Quotes | Configurable **IMEI lookup** on the quote builder (Settings → Device ID) |
| Dashboard | Per-user modular layout + calendar widget |

## How it works

```
  Windows PCs                         Your server
  -----------                         -----------
  WorkshopOS Client  --REST+SignalR-->  ASP.NET Core API
  (Setup.exe)                           |
                                        +--> PostgreSQL
                                        +--> Worker (jobs / backups)
```

1. **Server** — one command installs Git/Docker if needed, clones the repo, starts API + Postgres.
2. **Client** — download **`WorkshopOS-Setup-x.y.z.exe`** from [GitHub Releases](https://github.com/Ayden0726/repairos/releases), or build with the one-shot script below.
3. **Pair** — open `http://<server>:5088/connect`, enter the `WOS-XXXX` code in the app (or Find on this network).
4. **First PC** — setup wizard creates the business profile + owner account. Other PCs sign in; add staff under **Settings → Users & Roles**.

Features: **[docs/FEATURES.md](docs/FEATURES.md)** · Pricing & quotes: **[docs/PRICING_QUOTES.md](docs/PRICING_QUOTES.md)** · Auth / passwords: **[docs/AUTH.md](docs/AUTH.md)** · Pi: **[docs/RASPBERRY_PI.md](docs/RASPBERRY_PI.md)** · Security: **[docs/SETTINGS_AND_SECURITY.md](docs/SETTINGS_AND_SECURITY.md)** · GitHub push: **[docs/GITHUB_SYNC.md](docs/GITHUB_SYNC.md)**

---

## Push to GitHub + build installer (one command)

**Developer PowerShell for VS 2022.** Needs:

- Cloud Agent port **18765** forwarded (sync zip)
- Git + GitHub auth (`gh auth login` or Credential Manager)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) + WinUI workload
- [Inno Setup 6](https://jrsoftware.org/isdl.php) for `Setup.exe`

```powershell
$r = 'C:\Users\ayden\src\repairos'
$z = "$env:TEMP\WorkshopOS-1.2.35-github-sync.zip"
New-Item -ItemType Directory -Force -Path $r | Out-Null
Invoke-WebRequest -Uri 'http://127.0.0.1:18765/WorkshopOS-1.2.35-github-sync.zip' -OutFile $z
Expand-Archive -Path $z -DestinationPath $r -Force
powershell -ExecutionPolicy Bypass -File "$r\scripts\update-github-and-build-client.ps1"
```

What that does:

1. Overlays the agent sync zip onto your local clone  
2. Commits and pushes to `https://github.com/Ayden0726/repairos` (`main`)  
3. Tags **`v1.2.35`** (release workflow may run)  
4. Builds the Windows client zip **and** `Setup.exe`

Outputs:

| Artifact | Path |
| --- | --- |
| Client zip | `packaging\dist\WorkshopOS-Client-win-x64-v1.2.35.zip` |
| Installer | `packaging\dist\WorkshopOS-Setup-1.2.35.exe` |
| Extracted run | `%USERPROFILE%\Desktop\WorkshopOS-Client-1.2.35\` |

Useful flags:

```powershell
# Build only (already on GitHub)
powershell -ExecutionPolicy Bypass -File "$r\scripts\update-github-and-build-client.ps1" -SkipGitHubPush

# Push only (no Setup.exe)
powershell -ExecutionPolicy Bypass -File "$r\scripts\update-github-and-build-client.ps1" -SkipInstaller -SkipLaunch
```

Push-only helper (after overlay):

```powershell
powershell -ExecutionPolicy Bypass -File "$r\scripts\sync-to-github.ps1"
```

Full sync notes (PowerShell, bash, git bundle): **[docs/GITHUB_SYNC.md](docs/GITHUB_SYNC.md)**

---

## Install the server (one command)

### From Windows (sets up WSL2 + Ubuntu + server)

**PowerShell as Administrator:**

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-server-wsl.ps1
```

### Already on Linux / inside WSL / macOS

The script auto-installs **Git** and **Docker** (Compose v2) on common Linux distros when missing:

```bash
curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash
```

When it finishes:

- Open **`http://127.0.0.1:5088/connect`** (or `http://<lan-ip>:5088/connect`)
- Copy the pairing code (`WOS-XXXX`)
- Re-run / update later: `~/workshopos/scripts/get-workshopos.sh --update`

If you cloned the repo and scripts say “Permission denied”:

```bash
chmod +x scripts/*.sh
```

### Required secrets in `docker/.env`

Set once and keep forever (installer generates them on first run):

```bash
POSTGRES_PASSWORD=...
JWT_SIGNING_KEY=...          # 32+ chars, keep stable
Encryption__Key=...          # 32+ chars, SEPARATE from JWT — keep forever
Encryption__KeyVersion=1
API_PORT=5088
```

**Do not** rotate `Encryption__Key` casually — it unlocks saved API credentials. Details: [docs/SETTINGS_AND_SECURITY.md](docs/SETTINGS_AND_SECURITY.md).

Full guide: **[docs/INSTALL.md](docs/INSTALL.md)**

---

## Raspberry Pi 3B+

Same install command — Pi is auto-detected and gets the low-memory overlay + swap:

```bash
curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash
```

Force Pi mode on any low-RAM host:

```bash
./scripts/get-workshopos.sh --pi
```

| Board | Supported? |
| --- | --- |
| Pi 3B / 3B+ (1 GB) | Yes — use overlay + 2 GB swap; first build is slow |
| Pi 4 / 5 | Yes (recommended) |
| Pi Zero / 1 | No |

Windows clients still run on shop PCs; only the **server** runs on the Pi. See **[docs/RASPBERRY_PI.md](docs/RASPBERRY_PI.md)**.

### Update an existing server or Pi

```bash
cd ~/workshopos
git pull origin main
chmod +x scripts/*.sh
./scripts/restart-workshopos.sh --update
```

---

## Install the Windows client

### Shop PCs (prebuilt installer)

1. Go to **[GitHub Releases](https://github.com/Ayden0726/repairos/releases)**
2. Download **`WorkshopOS-Setup-x.y.z.exe`** (e.g. `WorkshopOS-Setup-1.2.35.exe`)
3. Run the installer on each Windows 10/11 **x64** PC
4. Enter the pairing code from `/connect`, or tap **Find on this network**

Stuck on an old server URL?

```powershell
Remove-Item -Force -ErrorAction SilentlyContinue "$env:LOCALAPPDATA\WorkshopOS\client-settings.json"
```

Then relaunch and pair again.

### Build client only (no GitHub push)

```powershell
powershell -ExecutionPolicy Bypass -File C:\Users\ayden\src\repairos\scripts\install-client-windows.ps1 -WithInstaller
# or:
powershell -ExecutionPolicy Bypass -File C:\Users\ayden\src\repairos\packaging\build-client.ps1 -Configuration Release -Version 1.2.35
```

---

## Settings, credentials & expenses

| Task | Where |
| --- | --- |
| Store / rotate API keys | **Settings → Integrations** (secrets never shown again — only “configured”) |
| Business expenses + GST | **Expenses** in the main nav |
| Recurring expenses | Templates create **draft** expenses on due dates |
| Accountant export | Expenses toolbar → CSV |
| Pricing / tax / services | **Settings → Pricing / Tax / Services** |

If keys used to “disappear” after a redeploy, that was usually a missing or rotated encryption key — fixed in 1.2.35. Read **[docs/SETTINGS_AND_SECURITY.md](docs/SETTINGS_AND_SECURITY.md)**.

## First-run wizard & staff

| Step | Who | What |
| --- | --- | --- |
| Setup wizard | First PC after pairing | Business name / ABN / GST + owner account |
| Login | Every PC | Email + password |
| Staff | Owner / admin | **Settings → Users & Roles** (create, roles, reset password) |
| Change password | Any user | **Settings → App → Change password…** |

Locked out of the owner account (server access only): see **[docs/AUTH.md](docs/AUTH.md)** / `scripts/reset-owner-password.sh`.

## Pricing & quotes (overview)

Server-side pricing (labour, markup, rounding, GST) drives quote totals. Configure under **Settings → Pricing / Tax / Services**. Details: **[docs/PRICING_QUOTES.md](docs/PRICING_QUOTES.md)**.

## Repository layout

```
apps/windows-client/   WinUI 3 desktop app
services/api/          ASP.NET Core API + /connect portal
services/worker/       Background jobs / backups
docker/                Compose + multi-arch Dockerfiles (+ Pi overlay)
packaging/             Client build + Inno Setup
scripts/               Install, sync-to-github, restart, password reset
docs/                  Install, features, Pi, security, GitHub sync
```

## Docs

| Doc | Topic |
| --- | --- |
| [docs/INSTALL.md](docs/INSTALL.md) | Server + client install, clean wipe, troubleshooting |
| [docs/GITHUB_SYNC.md](docs/GITHUB_SYNC.md) | Push overlay to GitHub; update servers / Pi |
| [docs/SETTINGS_AND_SECURITY.md](docs/SETTINGS_AND_SECURITY.md) | Encrypted credentials, settings fixes, expenses |
| [docs/RASPBERRY_PI.md](docs/RASPBERRY_PI.md) | Pi 3B+ server install |
| [docs/FEATURES.md](docs/FEATURES.md) | Feature list |
| [docs/AUTH.md](docs/AUTH.md) | Login, JWT, password reset |
| [docs/PRICING_QUOTES.md](docs/PRICING_QUOTES.md) | Pricing & quotes |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Topology |
| [docs/DOCKER.md](docs/DOCKER.md) | Compose services |
| [docs/POS_EFTPOS.md](docs/POS_EFTPOS.md) | POS / EFTPOS |
| [docs/WORDPRESS_INTEGRATION.md](docs/WORDPRESS_INTEGRATION.md) | WordPress / 10Web |

## Maintainer / advanced

Building the Windows client from source (Visual Studio, WinUI, Inno Setup) is for maintainers publishing a new Release. Shop installs should use **Setup.exe** from Releases. See [apps/windows-client/README.md](apps/windows-client/README.md) and [packaging/README.md](packaging/README.md).
