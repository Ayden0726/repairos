# WorkshopOS Architecture

**Product:** WorkshopOS — self-hosted repair-shop operations platform for Australian electronics / computer repair businesses.

**Version 1 posture:** Single location, multiple Windows PCs, one central server. Multi-location tables exist but only one location is active in V1.

## High-level topology

```
Windows Client (WinUI 3 / .NET 8)
        │  HTTPS REST + SignalR (JWT)
        ▼
ASP.NET Core API  ──►  PostgreSQL
        │
        ├── Background Worker (backups, notifications, deferred jobs)
        ├── File storage (local disk; S3/MinIO-ready later)
        ├── Optional Redis (cache / SignalR backplane later)
        └── Optional AI provider (Ollama / cloud) — not required for core ops
```

**Hard rule:** The Windows client never connects to PostgreSQL. All data access is through the API.

## Solution layout

```
/apps/windows-client/WorkshopOS.Client     WinUI 3 MVVM desktop app
/services/api/WorkshopOS.Api               ASP.NET Core REST + SignalR + OpenAPI
/services/worker/WorkshopOS.Worker         Background jobs
/services/shared/WorkshopOS.Domain         Entities and domain rules
/services/shared/WorkshopOS.Application    Use cases, interfaces, DTOs
/services/shared/WorkshopOS.Infrastructure EF Core, Identity, JWT, storage
/services/shared/WorkshopOS.Contracts      Shared API contracts for client
/database                                  Migration notes / SQL helpers
/docker                                    Dockerfiles + Compose
/docs                                      Architecture and phase docs
/tests/WorkshopOS.Api.Tests                Integration and unit tests
```

## Layering

| Layer | Responsibility |
| --- | --- |
| Domain | Entities, enums, invariants (no EF / HTTP) |
| Application | Auth, setup, settings, permissions orchestration |
| Infrastructure | EF Core, ASP.NET Identity, JWT, file/backup stubs |
| Api | Controllers, middleware, Swagger, SignalR hubs |
| Client | WinUI shell, DI, API client, secure server URL storage |

## Runtime targets

| Component | OS | Notes |
| --- | --- | --- |
| API / Worker | Linux (Docker) or Windows Server | Primary deploy: Docker on home/business server / Proxmox |
| Client | Windows 10/11 x64 | Installed via MSI/MSIX later (Phase 12) |
| PostgreSQL 16 | Same host or separate | Required |

## Phase 1 scope (this milestone)

Working end-to-end:

1. Repository + solution structure
2. Docker Compose (API, worker, Postgres, Redis optional)
3. EF Core model + migrations for auth, roles, permissions, settings, audit, location
4. First-run setup API + status
5. Login / refresh / logout (JWT)
6. Permission catalogue and role seeding
7. Windows client: connect to server, setup/login, main shell, sidebar, global search shell, theme, user menu
8. Health endpoint
9. Automated API tests
10. CHANGELOG + docs

Modules shown in the sidebar that belong to later phases open a clear **Not yet implemented** page with the phase number. No fake “SMS Sent” or empty CRUD pretending to be live.

## Security baseline (Phase 1)

- Passwords hashed via ASP.NET Identity (PBKDF2)
- JWT access tokens + rotating refresh tokens stored hashed
- HTTPS in production (Caddy/Traefik reverse proxy)
- Secrets via environment variables / `.env`
- Audit events for login, failed login, setup, settings changes
- Rate limiting on auth endpoints

## Branding

Product name is always **WorkshopOS**. The customer business name (e.g. Riverside Tech) is configured at setup and shown under the product name in the client. No business name is hardcoded for production; development seed may use Riverside Tech only when `Seed:Demo=true`.
