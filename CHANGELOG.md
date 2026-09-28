# Changelog

## 1.2.16 — 2026-09-28

### Changed
- **Quote pricing**: labour is one fee for the whole job; markup applies once to Σ landed part costs (not per line)
- Quote entity stores job-level `LabourFee`, `MarkupPercent`/`MarkupAmount`, `PartsCostTotal`, `PartsSellTotal`
- QuoteBuilder: remove per-line labour/markup; job Labour NumberBox + Markup %; summary Parts cost → Markup → Parts sell → Labour
- Accepted/frozen quotes unchanged (no recalculation of history)
- Client / API / product version `1.2.16`

## 1.2.15 — 2026-09-27

### Added
- Inventory **component type** for PC parts (CPU, Motherboard, RAM, GPU, Storage, PSU, Case, Cooler, OS, Peripheral, Other) with create/edit + list filter
- Full **PC Build builder** UI (slots, multi RAM/storage, cost/sell/margin summary)
- Inventory reservations tied to **PcBuildId**; exclusive vs repairs; release on cancel; consume on completed/sold

### Changed
- Client / API / product version `1.2.15`
- PC Builds nav under Inventory
- Builds API: get/update/status/delete with reservation validation

## 1.2.14 — 2026-09-27

### Fixed

- **First-run / pre-shell contrast**: Setup, Login, ServerConnect, Bootstrap now use solid Workshop page brushes + explicit text foregrounds (no more light-on-light)
- **ThemeService**: solid background colors applied from preference (no longer pulls light `Default` ThemeDictionary while `RequestedTheme` is Dark)

### Changed

- **Default theme = Dark** when unset in `client-settings.json`
- Compact **Light / Dark** toggle on Setup, Login, ServerConnect, Bootstrap headers and Shell pane footer (next to sign-out)
- Client / product version `1.2.14`

## 1.2.13 — 2026-09-27

### Docs / setup

- **Install path clarified**: server via `get-workshopos.sh`; Windows client via GitHub Release **`WorkshopOS-Setup-x.y.z.exe`** (build-from-source demoted to maintainer notes)
- Rewrote `README.md` + `docs/INSTALL.md` (architecture, pairing, wizard, pricing overview, password reset, clean install, troubleshooting: 404/old server, Smart App Control, divergent git, chmod, Docker, ports, health)
- Improved `scripts/get-workshopos.sh` (banners, `--update`, health, pairing/next-steps, ASCII-safe) + `restart-workshopos.sh` / `continue-workshopos-setup.sh`

### Added

- **Password reset (staff)**: Owner/Admin in **Settings → Users & Roles** → **Reset password…** (same password rules; revokes that user’s sessions)
- **Change password**: logged-in user on **Settings → App** (current + new)
- API: `PUT /api/users/{id}/password` (`staff.manage`), `PUT /api/auth/password` (self)
- `scripts/reset-owner-password.sh` + `WORKSHOPOS_OWNER_PASSWORD_RESET` / `--reset-owner-password` for locked-out owner when you have Docker/server access
- **Clean server install** section in `docs/INSTALL.md` (WSL wipe volumes + reinstall)

### Server

- Product version `1.2.13` on health/discovery; ClientMinVersion `1.2.13`

## 1.2.12 — 2026-09-27

### Added

- **Refurbished** (Used Tech module `used`): full list + create/edit UI under Inventory
  - Device identity: brand, model, category, serial, IMEI, colour, storage, other specs
  - Purchase price, parts/repair cost, total cost, asking price, actual sale price
  - Condition (New refurbished / Excellent / Good / Fair) and status (In stock / Reserved / Listed / Sold)
  - Notes, supplier/source, optional linked customer or repair ticket
  - Search + status filter; margin shown when `pricing.view` (or cost/profit) is allowed
- API: `GET/POST /api/used-tech`, `GET/PUT /api/used-tech/{id}`, status endpoint returns full detail
- EF migration `RefurbishedUsedDeviceFields` (brand/model/category/colour/storage/specs/notes/source/customer/ticket)

### Server

- Product version `1.2.12` on health/discovery; ClientMinVersion `1.2.12`

## 1.2.11 — 2026-09-27

### Fixed

- **404 Pricing / Services / Staff / Roles**: friendly “Server outdated — update/restart WorkshopOS server” when `/api/pricing/*`, `/api/users`, or `/api/roles` are missing on an old Docker image
- **Empty list pages**: Invoices, Purchase Orders, Calendar/Bookings, PC Builds, Used Tech now have in-app Create panels (no more “create from the API” stub)

### Added

- Default roles seed (Owner, Administrator, Manager, Technician, Front Desk, Sales, Read Only) preserved across restarts; Owner/Admin can **create/edit custom roles** with permission checkboxes (`POST/PUT /api/roles`)
- Settings → Connection **API strength** meter (latency + `/api/health` + version); `GET /api/system/info`; `POST /api/system/restart` (commands when `ALLOW_PROCESS_RESTART` is off)
- `scripts/restart-workshopos.sh` (+ `--update` for git pull + rebuild)
- Shell footer shows **display name + role** above Sign out

### Server

- Product version `1.2.11` on health/discovery; ClientMinVersion `1.2.11`

## 1.2.10 — 2026-09-26

### Fixed

- **Settings persistence**: Pricing / Tax / Services save now PUT then reload from GET; clear success/error feedback; NaN-safe NumberBox reads
- Business tax update syncs `pricing.settings` Tax + `gst` setting so quotes see the same GST
- Settings storage writes camelCase JSON; corrupt setting JSON no longer silently falls back without error
- ApiClient sends camelCase JSON; clearer 403 messages when save is forbidden

### Tests

- PUT `/api/pricing/settings` then GET returns the same values (including raw `settings` jsonb)
- PUT `/api/settings/business` tax then pricing mirror matches

## 1.2.9 — 2026-09-26

### Fixed

- **Quote Builder customers**: load/search existing customers from `GET /api/customers` (same pattern as New Ticket), show name · phone · email · id, keep create-new path
- **Quote pricing settings**: open Quote Builder loads `GET /api/pricing/settings`, applies markup/labour defaults to new lines, shows active settings summary; preview/create send null overrides so **server saved settings** apply (not sticky hardcoded 20%/50)
- Settings → Pricing ComboBox load/save more reliable for markup method and rounding

### Tests

- Preview uses saved pricing settings after PUT `/api/pricing/settings`
- Calculator applies custom defaults when overrides are null

## 1.2.8 — 2026-09-26

### Fixed

- WinUI **XamlCompiler MSB3073**: Quote Builder `NumberBox` TwoWay `x:Bind` used `decimal` (NumberBox.Value is `double`); line drafts now use `double` + string summary helpers; no `x:Name` inside line DataTemplate
- `build-client.ps1` **v6** logs XamlCompiler/MSBuild details to `packaging/out/logs/publish-last.log` on failure

### Added

- **Repair Quote & Automatic Pricing System**: `PricingCalculator`, pricing settings (`pricing.settings`), markup tiers, service pricing catalogue
- Quote statuses Draft|Sent|Viewed|Accepted|Declined|Expired|Converted|Cancelled; revisions + audit log; frozen accepted quotes
- APIs: pricing settings/tiers/services/preview; quote create/update/send/accept/decline/revise/convert/print; dashboard quote analytics
- WinUI **Quote Builder** (Work → Quotes), Create Quote from repair detail, Settings → Pricing / Services / Tax
- Permissions: `pricing.view_cost`, `pricing.view_profit`, `pricing.change_markup`, `pricing.override_labour`, `pricing.apply_discount`, `pricing.override_price`, `pricing.approve_low_margin`, `pricing.edit_settings`

## 1.2.7 — 2026-09-25

### Added

- Shell header **bell** icon (replaces Alerts text) with notifications flyout + unread badge
- Auto-dismissing **InfoBar** banner when new notifications arrive (poll every ~20s)
- Dashboard time-of-day greeting with technician display name
- Dashboard **My open tickets** quick view (assigned to current user) with empty state

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
