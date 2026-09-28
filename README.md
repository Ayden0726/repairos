# WorkshopOS

Self-hosted repair-shop operations for Australian electronics / computer repair businesses.

**Current version: 1.2.15+**

## What it is

WorkshopOS runs your shop’s day-to-day work on **your own server**:

- Repair tickets, customers, devices, quotes, invoices, inventory, purchasing, PC builds, refurbished stock, calendar, reports, backups, and staff roles
- A **Windows desktop app** on each shop PC
- A **central server** (Docker) you host on a Linux PC, NAS, VM, or WSL

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

Features: **[docs/FEATURES.md](docs/FEATURES.md)** · Pricing & quotes: **[docs/PRICING_QUOTES.md](docs/PRICING_QUOTES.md)** · Auth / passwords: **[docs/AUTH.md](docs/AUTH.md)**

## Install the server (one command)

Needs a Linux / WSL / macOS host. The script auto-installs **Git** and **Docker** (Compose v2) on common Linux distros when missing:

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

1. Go to **[GitHub Releases](https://github.com/Ayden0726/repairos/releases)**
2. Download **`WorkshopOS-Setup-x.y.z.exe`** (e.g. `WorkshopOS-Setup-1.2.15.exe`)
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
