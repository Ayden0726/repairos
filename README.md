# WorkshopOS

Self-hosted repair-shop operations platform for Australian electronics / computer repair businesses.

**Architecture:** Windows WinUI 3 client → HTTPS REST + SignalR → ASP.NET Core API → PostgreSQL.

The Windows client never talks to PostgreSQL directly.

**Live API (this environment):** [WorkshopOS API health](http://127.0.0.1:5088/api/health)

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
scripts/                 Server install & publish helpers
docs/                    Architecture, features, install
tests/                   Integration tests
.github/workflows/       CI + GitHub Release artifacts
```

## Install server (Docker)

```bash
chmod +x scripts/install-server.sh
./scripts/install-server.sh
```

Or:

```bash
cd docker
cp .env.example .env   # set POSTGRES_PASSWORD + JWT_SIGNING_KEY
docker compose --env-file .env up -d --build
```

API: [http://127.0.0.1:5088](http://127.0.0.1:5088) · Swagger `/swagger` · Health `/api/health`

Step-by-step (tarball + GitHub Releases): **[docs/INSTALL.md](docs/INSTALL.md)**

## Build the Windows client

**Must be done on Windows 10/11 x64** with .NET 8 + Windows App SDK / WinUI.

```powershell
# Dev
dotnet run --project apps\windows-client\WorkshopOS.Client\WorkshopOS.Client.csproj

# Portable zip + optional Inno installer → packaging\dist\
.\packaging\build-client.ps1 -Configuration Release -Version 1.2.0
```

1. Install / unzip the client on shop PCs  
2. Enter server URL (`http://<server>:5088`)  
3. Complete setup (or sign in)  
4. Use the sidebar — all Phase 1–12 modules are wired  

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
