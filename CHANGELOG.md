# Changelog

## 1.2.6 — 2026-09-25

### Added

- WinUI **New ticket**: **Existing customer** checkbox — checked picks/search customers; unchecked shows inline create (first/last name, phone, email)
- New-customer path calls `POST /api/customers` then creates the repair with the new id
- Customer search empty / loading / error status on the New ticket form

## 1.2.5 — 2026-09-25

### Added

- WinUI **Tickets** helpdesk UI: Work → Tickets, shell **New ticket** CTA, list filters (status / priority / tech / overdue), ticket detail with status workflow chips, notes timeline (internal flag), priority change, customer link, printable job sheet (`GET /api/repairs/{id}/print`)
- Repair list API priority filter (`?priority=`) and `POST /api/repairs/{id}/priority`

### Fixed

- Dark mode uses solid Workshop theme brushes (page / surface / border / text) instead of blurry translucent grey cards
- Settings → Users & Roles role dropdown: WinUI `DisplayMemberPath=Name` conflict fixed via `DisplayLabel`; roles load from `GET /api/roles` with clearer errors


## 1.2.2 — 2026-09-24

### Fixed

- Windows client **always** opens **ServerConnect** (pairing / URL) on launch — Bootstrap is never the initial page, so a stale saved URL cannot hang on **Connecting to server…**
- **Change server** / **Clear saved server** wipe JSON, WinRT `LocalSettings`, and PasswordVault credentials
- Connect screen shows a clear **Client 1.2.2** banner; packaging default version / build script **v5**

## 1.2.1 — 2026-09-24

### Fixed

- Windows client no longer hangs forever on **Connecting to server…**; bootstrap probes the saved URL for ~4s then always opens pairing/connect (manual button available immediately)
- No settings / cleared `%LOCALAPPDATA%\WorkshopOS` → skip Bootstrap splash and open **ServerConnect** immediately (server does not need to be running yet)
- Deleting the settings folder no longer resurrects a dead URL from WinRT `LocalSettings` (that was re-triggering the connecting splash after a “clear”)
- Client settings moved to `%LOCALAPPDATA%\WorkshopOS\client-settings.json` (easy reset); Change server from Login/Settings
- Splash and connect screens show **Client 1.2.1** so you can confirm you launched the new build

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
