# Changelog

## 1.2.1 — 2026-09-24

### Added

- SimplyPrint-style one-command server install: `scripts/get-workshopos.sh`
- Public `/api/discovery` + `/connect` pairing portal (LAN pairing code `WOS-XXXX`)
- Windows connect screen: pairing code, Find on this network, manual URL
- Windows client build docs with download links for every prerequisite

## 1.2.0 — 2026-09-22

### Added

- Phase 3–12 operations API: dashboard, quotes, inventory/POs, invoices/payments, notifications, bookings, knowledge, PC builds, used tech, QA, reports, AI assist, backups
- EF migration `Phase3to12Operations`
- WinUI pages: Dashboard, Inventory, Reports, Generic list modules, Backups, AI Assist; shell routes all modules
- Packaging: `packaging/build-client.ps1`, Inno Setup script, `scripts/install-server.sh`, `scripts/publish-server.sh`
- GitHub Actions CI + Release workflows (server tarball + Windows client zip)
- Docs: FEATURES, INSTALL; README overhaul for distribution

## 1.1.0-phase2 — 2026-09-22

### Added

- Customers API (list/search, create/update, detail with devices and recent repairs)
- Devices API (per-customer list, create/update)
- Repair tickets with numbering (`REP-YYYY-#####`), statuses, types, priorities
- Intake fields (condition, accessories, encrypted passcode)
- Immutable repair timeline events and customer/internal notes
- Assign technician, change status, update diagnosis
- Global search returns repairs, customers, and devices
- WinUI pages: Customers, Customer detail, Repairs, New repair intake, Repair detail
- Integration test covering customer → device → repair → status → timeline → search

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
