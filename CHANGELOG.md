# Changelog

## 1.0.0-phase1 — 2026-09-22

### Added

- Solution restructure: ASP.NET Core API, worker, shared libraries, WinUI 3 client, Docker, docs
- PostgreSQL schema via EF Core (`InitialPhase1`): users, roles, permissions, refresh tokens, locations, settings, audit
- First-run setup API and Windows setup wizard
- JWT login / refresh / logout with hashed refresh tokens and rate limiting
- Permission catalogue and system roles (Owner, Administrator, Manager, Technician, Front Desk, Sales, Read Only)
- Health endpoint, OpenAPI/Swagger, SignalR hub stub (`/hubs/workshop`)
- Windows client shell: server connect, login, sidebar navigation, global search shell, Settings/Users
- Explicit **Not yet implemented** pages for modules belonging to later phases
- Integration tests for setup → login → `/api/auth/me`

### Removed

- Previous Next.js web prototype (replaced by client/server architecture per product direction)
