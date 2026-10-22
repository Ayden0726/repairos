# WorkshopOS

Self-hosted repair-shop operations for Australian electronics / computer repair businesses.

**Current version: 1.2.33**

## What it is

WorkshopOS runs your shop’s day-to-day work on **your own server**:

- Repair tickets, customers, devices, quotes, invoices, **business expenses** (GST-aware), inventory, purchasing, refurbished stock, calendar, reports, backups, and staff roles
- **Encrypted API credentials** under Settings → Integrations — [docs/SETTINGS_AND_SECURITY.md](docs/SETTINGS_AND_SECURITY.md)
- **POS register** (admin-enabled) with AU EFTPOS guidance — [docs/POS_EFTPOS.md](docs/POS_EFTPOS.md)
- **WordPress / 10Web website API** — WorkshopOS is the source of truth for public quotes, bookings, Sell Your Tech, customer portal, and refurbished listings ([docs/WORDPRESS_INTEGRATION.md](docs/WORDPRESS_INTEGRATION.md))
- A **Windows desktop app** on each shop PC
- A **central server** (Docker) on a Linux PC, NAS, VM, WSL, or **Raspberry Pi 3B+** ([docs/RASPBERRY_PI.md](docs/RASPBERRY_PI.md))

Nothing is required from a SaaS cloud — data stays on your machine / LAN.

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
2. **Client** — download **`WorkshopOS-Setup-x.y.z.exe`** from [GitHub Releases](https://github.com/Ayden0726/repairos/releases) (do **not** compile WinUI yourself).
3. **Pair** — open `http://<server>:5088/connect`, enter the `WOS-XXXX` code in the app (or Find on this network).
4. **First PC** — setup wizard creates the business profile + owner account. Other PCs sign in; add staff under **Settings → Users & Roles**.

Features: **[docs/FEATURES.md](docs/FEATURES.md)** · Pricing & quotes: **[docs/PRICING_QUOTES.md](docs/PRICING_QUOTES.md)** · Auth / passwords: **[docs/AUTH.md](docs/AUTH.md)** · Pi: **[docs/RASPBERRY_PI.md](docs/RASPBERRY_PI.md)** · Security: **[docs/SETTINGS_AND_SECURITY.md](docs/SETTINGS_AND_SECURITY.md)**

### Push this tree to GitHub

If you received a sync zip / agent overlay, follow **[docs/GITHUB_SYNC.md](docs/GITHUB_SYNC.md)** — includes PowerShell and bash push commands, then how to update both shop servers and Raspberry Pis from `main`.

## Install the server (one command)

### From Windows (sets up WSL2 + Ubuntu + server)

**PowerShell as Administrator:**

```powershell
iwr http://127.0.0.1:28765/install-server-wsl.ps1 -OutFile $env:TEMP\install-server-wsl.ps1
powershell -ExecutionPolicy Bypass -File $env:TEMP\install-server-wsl.ps1
```

Or from a clone: `powershell -ExecutionPolicy Bypass -File .\scripts\install-server-wsl.ps1`

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

Full guide (clean install, troubleshooting, ports): **[docs/INSTALL.md](docs/INSTALL.md)**

## Install the Windows client

### Sync GitHub + build client zip + Setup.exe (one command)

**Developer PowerShell for VS 2022.** Needs agent sync on port **28765**, GitHub auth, [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), and [Inno Setup 6](https://jrsoftware.org/isdl.php) for the installer.

```powershell
$r='C:\Users\ayden\src\repairos'; $z="$env:TEMP\repairos-github-sync.zip"
New-Item -ItemType Directory -Force -Path $r | Out-Null
Invoke-WebRequest -Uri 'http://127.0.0.1:28765/repairos-github-sync.zip' -OutFile $z
Expand-Archive -Path $z -DestinationPath $r -Force
powershell -ExecutionPolicy Bypass -File "$r\scripts\update-github-and-build-client.ps1"
```

Outputs:

- Pushes overlay to `https://github.com/Ayden0726/repairos` (`main`)
- `packaging\dist\WorkshopOS-Client-win-x64-v1.2.30.zip`
- `packaging\dist\WorkshopOS-Setup-1.2.30.exe` (needs Inno Setup 6)
- Extracts + launches `Desktop\WorkshopOS-Client-1.2.30\WorkshopOS.Client.exe`

Skip GitHub push or installer if needed:

```powershell
powershell -ExecutionPolicy Bypass -File "$r\scripts\update-github-and-build-client.ps1" -SkipGitHubPush
powershell -ExecutionPolicy Bypass -File "$r\scripts\update-github-and-build-client.ps1" -SkipInstaller
```

### Build client only (no GitHub push)

```powershell
powershell -ExecutionPolicy Bypass -File C:\Users\ayden\src\repairos\scripts\install-client-windows.ps1 -WithInstaller
```

### Shop PCs (prebuilt installer)

1. Go to **[GitHub Releases](https://github.com/Ayden0726/repairos/releases)**
2. Download **`WorkshopOS-Setup-x.y.z.exe`** (e.g. `WorkshopOS-Setup-1.2.30.exe`)
3. Run the installer on each Windows 10/11 **x64** PC
4. Enter the pairing code from `/connect`, or tap **Find on this network**

Stuck on an old server URL?

```powershell
Remove-Item -Force -ErrorAction SilentlyContinue "$env:LOCALAPPDATA\WorkshopOS\client-settings.json"
```

Then relaunch and pair again.

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
docker/                Compose + Dockerfiles
packaging/             Maintainer: client build + Inno Setup
scripts/               get-workshopos.sh, restart, password reset
docs/                  Install, features, architecture
```

## Docs

| Doc | Topic |
| --- | --- |
| [docs/INSTALL.md](docs/INSTALL.md) | Server + client install, clean wipe, troubleshooting |
| [docs/FEATURES.md](docs/FEATURES.md) | Feature list |
| [docs/AUTH.md](docs/AUTH.md) | Login, JWT, password reset |
| [docs/PRICING_QUOTES.md](docs/PRICING_QUOTES.md) | Pricing & quotes |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Topology |
| [docs/DOCKER.md](docs/DOCKER.md) | Compose services |

## Maintainer / advanced

Building the Windows client from source (Visual Studio, WinUI, Inno Setup) is **optional** and only for maintainers publishing a new Release. Shop installs should use **Setup.exe** from Releases. See [apps/windows-client/README.md](apps/windows-client/README.md) and [packaging/README.md](packaging/README.md).
