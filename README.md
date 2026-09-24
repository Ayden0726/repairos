# WorkshopOS

Self-hosted repair-shop operations platform for Australian electronics / computer repair businesses.

**Architecture:** Windows WinUI 3 client → HTTPS REST + SignalR → ASP.NET Core API → PostgreSQL.

The Windows client never talks to PostgreSQL directly.

**Live API (this environment):** [WorkshopOS API health](http://127.0.0.1:5088/api/health) · [Connect portal](http://127.0.0.1:5088/connect)

## Features

Full catalogue: **[docs/FEATURES.md](docs/FEATURES.md)**

| Area | Includes |
| --- | --- |
| Auth & setup | First-run wizard, JWT, roles/permissions, users, business settings |
| Workshop | Customers, devices, repair intake/tickets/timeline, dashboard, calendar, notifications |
| Sales | Quotes (GST), invoices & payments, used tech |
| Stock | Inventory, reservations, suppliers, purchase orders |
| Services | PC builds, knowledge base, QA checklists, optional AI (Ollama) |
| Management | Reports, backups, module visibility |

## Repository layout

```
apps/windows-client/     WinUI 3 desktop app (Windows 10/11 x64)
services/api/            ASP.NET Core API + Swagger + SignalR
services/worker/         Background worker
services/shared/         Domain, Application, Infrastructure, Contracts
docker/                  Compose + Dockerfiles
packaging/               Windows client build + Inno Setup script
scripts/                 One-command server install & publish helpers
docs/                    Architecture, features, install
tests/                   Integration tests
.github/workflows/       CI + GitHub Release artifacts
```

## Install server (one command)

On a Linux host with Docker:

```bash
curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash
```

That clones the repo, starts API + PostgreSQL + worker, then prints a **pairing code** and the `/connect` URL.

When it finishes, open:

- Same machine: `http://127.0.0.1:5088/connect`
- Phone / PC on the same Wi‑Fi: `http://<server-lan-ip>:5088/connect`

Optional flags: `bash -s -- --dir ~/workshopos --port 5088`

From a local clone you can also run `./scripts/install-server.sh` (wraps the same installer).

Manual Compose: **[docs/INSTALL.md](docs/INSTALL.md)**

## Connect the Windows client

1. Install / unzip **WorkshopOS Client** on a shop PC (see below).
2. Open the app and either:
   - Enter the **pairing code** from `/connect` → **Connect with code**, or
   - Tap **Find on this network** (same LAN as the server), or
   - Paste the server URL manually (`http://<server>:5088`).
3. First PC completes the setup wizard (business + owner). Later PCs sign in.

Discovery API (no auth): `GET /api/discovery` · Connect portal: `GET /connect`

## Build the Windows client

**Must be done on Windows 10/11 x64** with .NET 8 + Windows App SDK / WinUI.

```powershell
# Dev
dotnet run --project apps\windows-client\WorkshopOS.Client\WorkshopOS.Client.csproj

# Portable zip + optional Inno installer → packaging\dist\
.\packaging\build-client.ps1 -Configuration Release -Version 1.2.0
```

Details: [apps/windows-client/README.md](apps/windows-client/README.md) · [docs/INSTALL.md](docs/INSTALL.md)

## Put downloads on GitHub

1. Create the public GitHub repo (Cursor **Create repo** if needed) and push `main`.
2. Tag: `git tag v1.2.0 && git push origin v1.2.0`
3. Actions publishes **server tarball** + **client zip** on the Release page.
4. Shops download the installer/zip and the server bundle from **Releases**.

Manual server pack (no tag): `./scripts/publish-server.sh 1.2.0`

## Develop / test API

Needs .NET 8 SDK and PostgreSQL 16 (or Docker postgres).

```bash
cd services/api/WorkshopOS.Api
dotnet run
dotnet test WorkshopOS.sln
```

## Branding

Product name is always **WorkshopOS**. Business name, ABN, accent colour come from setup — nothing is hardcoded for production tenants.
