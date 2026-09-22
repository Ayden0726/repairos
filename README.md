# WorkshopOS

Self-hosted repair-shop operations platform for Australian electronics / computer repair businesses.

**Architecture (Phase 1 complete):** Windows WinUI 3 client → HTTPS REST + SignalR → ASP.NET Core API → PostgreSQL.

The Windows client never talks to PostgreSQL directly.

## Repository layout

```
apps/windows-client/WorkshopOS.Client   WinUI 3 desktop app (Windows 10/11 x64)
services/api/WorkshopOS.Api             ASP.NET Core API + Swagger + SignalR hub
services/worker/WorkshopOS.Worker       Background worker
services/shared/                        Domain, Application, Infrastructure, Contracts
docker/                                 Compose + Dockerfiles
docs/                                   Architecture and phase design
tests/WorkshopOS.Api.Tests              Integration tests
```

Design docs: [Architecture](docs/ARCHITECTURE.md) · [Roadmap](docs/ROADMAP.md) · [API](docs/API.md) · [Auth](docs/AUTH.md)

## Phase 1 status

Working now:

- First-run setup (business + owner)
- Login / refresh / logout (JWT)
- Roles & permissions catalogue
- Health endpoint
- Settings business profile read
- Windows shell: connect → setup/login → sidebar → search shell → Settings / Users
- Later modules open a clear **Not yet implemented (Phase N)** page — no fake data

Not in Phase 1: repairs, inventory, quotes, invoices, live dashboard metrics, SMS, AI, installer.

## Run the API (Linux / macOS / Windows)

Needs .NET 8 SDK and PostgreSQL 16.

```bash
# create DB once
createdb workshopos_net   # or use docker/compose postgres

cd services/api/WorkshopOS.Api
# edit appsettings.json ConnectionStrings + Jwt:SigningKey
dotnet run
```

API: [http://127.0.0.1:5088](http://127.0.0.1:5088) · Swagger: `/swagger` · Health: `/api/health`

```bash
dotnet test WorkshopOS.sln
```

## Run with Docker

```bash
cp docker/.env.example docker/.env
# set POSTGRES_PASSWORD and JWT_SIGNING_KEY
docker compose -f docker/docker-compose.yml --env-file docker/.env up -d --build
```

## Windows client

Build on a Windows 10/11 x64 machine with Visual Studio 2022 + Windows App SDK / WinUI workload:

```powershell
dotnet restore apps/windows-client/WorkshopOS.Client/WorkshopOS.Client.csproj
dotnet build apps/windows-client/WorkshopOS.Client/WorkshopOS.Client.csproj -c Debug
dotnet run --project apps/windows-client/WorkshopOS.Client/WorkshopOS.Client.csproj
```

1. Enter server URL (`http://127.0.0.1:5088` for local API)
2. Complete setup (or sign in if already set up)
3. Use the sidebar — Settings and Users work; other modules show Phase placeholders

## Branding

Product name is always **WorkshopOS**. Business name (ABN, logo fields, accent) comes from setup — nothing is hardcoded for production tenants.

## Next

Phase 2: customers, devices, repair tickets, intake, timeline. See [docs/ROADMAP.md](docs/ROADMAP.md).
