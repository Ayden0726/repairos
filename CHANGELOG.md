# Changelog

## 1.2.34 — 2026-10-22

### Unified release version
- API / product, Windows client, and `ClientMinVersion` all **1.2.34** (no more 1.2.32 client vs 1.2.33 API split)
- Connect / Settings version banners driven by assembly `InformationalVersion` (`ClientAppInfo`) — no more stuck **1.2.25** labels
- Packaging, GitHub sync zip, one-shot push/build scripts, and Inno Setup defaults updated for **1.2.34**
- Sync artifact: `WorkshopOS-1.2.34-github-sync.zip` (agent port **18765**)

### Build fix — Device Identification / IMEI (WSL Docker)
- Restored orphaned IMEI feature that broke `docker compose` builds (`IDeviceIdentificationService` / `SecretProtector` CS0246)
- Wired interface, DI, `SettingKeys.DeviceIdentification`, IMEI DbSets, catalogue Series/Aliases fields, and migrations
- Worker + API publish verified; DeviceIdentification + security tests green

Carries forward from 1.2.32 / 1.2.33: encrypted API credentials, expenses (AU GST), Raspberry Pi 3B+ server stack, settings persistence fixes.

- API / client version `1.2.34` (ClientMinVersion `1.2.34`)

## 1.2.33 — 2026-10-22

### Raspberry Pi / ARM server support
- Multi-arch Dockerfiles (linux/amd64, linux/arm64, linux/arm/v7) on .NET 8 bookworm-slim
- `docker/docker-compose.pi.yml` low-memory overlay (Postgres tune, API/worker mem caps, Redis off)
- `get-workshopos.sh` detects Pi, creates 2 GiB swap when RAM < 2 GB, rejects ARMv6
- Redis moved to optional Compose profile (unused by API/worker)
- Installer also writes `Encryption__Key` into `docker/.env`
- Guide: `docs/RASPBERRY_PI.md`

### GitHub sync packaging
- `docs/GITHUB_SYNC.md` — push to Ayden0726/repairos + deploy servers/Pi
- `scripts/sync-to-github.ps1` / `scripts/sync-to-github.sh` — one-shot commit+push+tag
- Updated `build-github-sync-artifacts.sh` for 1.2.33 overlay zip + git bundle

### Windows client version display
- Connect / bootstrap / Settings banners were stuck on **Client 1.2.25** / **1.2.29** (hardcoded XAML)
- Now read assembly `InformationalVersion` via `ClientAppInfo` so the UI matches `WorkshopOS.Client.csproj` (**1.2.32**)
- Packaging defaults (`build-client.ps1`, Inno Setup, `rebuild-client.ps1`) aligned to **1.2.32**

- API version `1.2.33` (ClientMinVersion still `1.2.32`)

## 1.2.32 — 2026-10-21

### Security & persistent settings
- Documented root causes of settings/API-key “resets” in `docs/SETTINGS_AND_SECURITY.md`
- `SecretProtector`: AES-256-GCM encrypt **and** decrypt, dedicated `Encryption:Key`, key versioning; never silently wipe ciphertext on decrypt failure
- New `integration_credentials` table + `/api/integrations/credentials` (admin-only); secrets never returned in API responses
- Migrates legacy WooCommerce / POS / webhook secrets out of plaintext `settings` JSON into encrypted credentials
- Pricing save no longer invents/overwrites a default business profile
- Settings writes support optional concurrency checks (`expectedUpdatedAt`)
- Docker Compose requires `Encryption__Key` (separate from JWT); files volume used for expense receipts

### Business expenses
- New Finance → **Expenses** module (WinUI + API): categories, GST treatments (AU 10%), dashboard (FY Jul–Jun), recurring drafts (idempotent), CSV export, soft delete, receipt attachments
- Permissions: `expenses.view/manage/delete/export/categories`
- Worker processes due recurring expenses without duplicates

### Tests
- `SecurityExpensesTests` — protector round-trip, GST, credentials masking, POS key not in settings JSON, expenses dashboard/export/recurring

- API / client version `1.2.32` (ClientMinVersion `1.2.32`)

## 1.2.31 — 2026-10-15

### Refurbished Devices Phase 1 foundations
- Audit and phased plan: `docs/REFURBISHED_OVERHAUL.md`
- Additive `used_devices` columns: stock id / SKU, location, technician, acquisition link, cost breakdown, security checks, listing refs
- New acquisition (outbound buying) aggregate: leads, offers, events — `/api/acquisitions/*`
- Refurbished dashboard KPIs — `GET /api/used-tech/dashboard`
- Buy / profit calculator with AUD recommendations — `POST /api/used-tech/buy-calculator`
- Permissions: `acquisitions.view/manage`, `used.view_costs`, `used.view_financials`, `used.approve_purchase`, `used.publish`
- New devices and Sell Your Tech conversions assign canonical `TB-RFB-#####` stock ids
- Existing Used Tech CRUD and WinUI page remain compatible (ClientMinVersion stays `1.2.30`)

### Tests
- `RefurbishedPhase1Tests` — calculator, acquisition → inventory convert, dashboard, stock id assignment

- API version `1.2.31` (ClientMinVersion `1.2.30`)

## 1.2.30 — 2026-10-09

### Added
- **POS repair payoffs**: import open repair tickets (`GET /api/pos/repairs-payable`), pay full balance or a deposit
- **POS discounts**: Staff / Family & Friends / Loyalty / Other presets (configurable under Settings → POS)
- PosSale stores `RepairTicketId`, discount label/percent/amount, and `IsDeposit`
- Migration `PosSaleRepairDiscount` (repair + discount columns on `pos_sales`)
- New-ticket / quote builder filters catalogue by device type (Phone / Tablet / Laptop / PC)

### Changed
- API / client version `1.2.30` (ClientMinVersion `1.2.30`)
- Repair invoice creation prefers accepted quote total when present

## 1.2.29 — 2026-10-08

### Added
- **POS register** (Phase 6 slice): admin-gated module (off by default), counter sales with invoice + stock deduction, void/restock
- Settings → **POS**: enable toggle, payment provider (None / Manual EFTPOS / Tyro / Square / Stripe Terminal / Linkly), merchant/terminal IDs, API key storage
- Module visibility API `PUT /api/settings/modules`; POS visibility stays in sync with enable toggle
- POS APIs: `/api/pos/sales`, `/api/pos/providers`, `/api/settings/pos`
- WinUI **POS** page (cart, inventory pick, checkout, today’s sales)
- Docs: `docs/POS_EFTPOS.md` — AU EFTPOS brands and how to connect

### Changed
- API / client version `1.2.29` (ClientMinVersion `1.2.29`)
- Permissions: `pos.view`, `pos.sell`, `pos.manage` (Front Desk / Sales can sell; Owner/Admin manage)

## 1.2.28 — 2026-10-07

### Removed
- **Custom PC Builds** from product surface: sidebar, modules catalogue, `/api/builds`, `/api/custom-pc`, and `POST /api/public/custom-pc` (HTTP 410). Database tables retained (no data drop). Desktop computers use ordinary repair tickets.

### Added
- Platform analysis + phased implementation plan: `docs/PLATFORM_ANALYSIS.md`, `docs/IMPLEMENTATION_PLAN.md`
- TechBench NQ shell IA: Repairs filters, Bookings, CRM, Sales, Inventory, Insights (AI + Knowledge wired)
- Command centre (Ctrl+K): expanded global search + command shortcuts with result navigation

### Changed
- API / client version `1.2.28` (ClientMinVersion `1.2.28`)
- Client nav docs updated (`docs/CLIENT_NAVIGATION.md`)

## 1.2.27 — 2026-10-06

### Added
- Custom PC staff workflow: list/status + convert website request → PC build
- Refurb diagnostic checklist/result APIs; full refurb status machine (Purchased → Sold)
- Public repair messaging by tracking + PIN; Woo/refurb sold-reserved sync-back endpoint
- Outbound webhooks (`/api/website/webhooks`) and WooCommerce REST product push when credentials set
- Configurable website booking hours/capacity; worker drains notification outbox
- Repair status changes auto-queue SMS/email templates + emit `repair.status_changed` webhook

### Changed
- Sell Your Tech statuses validated against the full workflow list
- API / product version `1.2.27` (ClientMinVersion remains `1.2.25`)
- WordPress docs aligned to real DTO field names

## 1.2.26 — 2026-10-06

### Added
- **WordPress / website API** — WorkshopOS remains the single source of truth; WordPress/10Web calls dedicated REST endpoints (no WP repair DB).
- Public API (`X-WorkshopOS-Key`, rate-limited): repair quote, booking, sell-device, custom PC, repair status (PIN or secure link), quote approve/decline, booking availability, same-day parts hint.
- Customer portal API (JWT `portal=customer`): login/register, repairs, quotes, devices, invoices, warranties, messages, sell requests.
- Sell Your Tech module: requests, offer calculator + Settings defaults, send offer, one-click create refurb (`TB-RFB-#####`).
- Messaging threads (customer-visible vs internal), warranty records, notification templates + outbox, quote approval audit (revision-aware).
- Website staff API: API keys, WooCommerce settings/publish stub, dashboard cards, QR payloads, notification template CRUD.
- Refurb diagnostic checklist seeds; extended repair statuses for website workflow.
- Docs: [docs/WORDPRESS_INTEGRATION.md](docs/WORDPRESS_INTEGRATION.md) (endpoints, auth, errors, webhooks, plugin strategy, env vars).

### Changed
- Editing an accepted quote resets approval (`Draft`) and bumps revision so old approvals cannot be reused silently.
- API / product version `1.2.26` (ClientMinVersion remains `1.2.25`)

## 1.2.25 — 2026-09-29

### Fixed
- **Calendar blank grid + “An unexpected error occurred.”**: bookings list is safer (SQL projection, UTC query dates); calendar still draws the month grid if the bookings call fails; API/client now surface the real exception `detail` instead of only the generic title.

### Changed
- API / product version `1.2.25` (ClientMinVersion `1.2.25`)


## 1.2.24 — 2026-09-28

### Added
- **Calendar page**: real month/week grid for bookings (replaces flat GenericList). Click a day to schedule; tap a booking to edit or cancel.
- Booking API: `GET /api/bookings/{id}`, `PUT /api/bookings/{id}`, `POST /api/bookings/{id}/cancel`.

### Changed
- API / product version `1.2.24` (ClientMinVersion `1.2.24`)

## 1.2.23 — 2026-09-28

### Added
- **Quote Builder Parts**: Service and Inventory ComboBoxes are searchable — type to filter by name / SKU / code, Enter submits the match.
- **Login**: press Enter in email or password to Sign in.
- **Settings → App → Start with Windows**: registers WorkshopOS in the current-user Run key so it opens at Windows sign-in. Preference survives Clear Connection. Setup.exe also offers an optional startup shortcut task.

### Changed
- API / product version `1.2.23` (ClientMinVersion `1.2.23`)

## 1.2.22 — 2026-09-28

### Fixed
- **CS0101 / CS0111 duplicate `CatalogueServiceRow`**: zip overlay from 1.2.21 did not delete orphan `ServicePickerControl.xaml(.cs)` on disk, so both the old UserControl and `ServiceCataloguePickerLogic.cs` compiled. Rebuild script now force-deletes those files and wipes `obj`/`bin` after overlay.
- `CatalogueServiceRow` remains defined only in `ServiceCataloguePickerLogic.cs`.

### Changed
- API / product version `1.2.22` (ClientMinVersion `1.2.22`)

## 1.2.21 — 2026-09-28

### Fixed
- **XamlCompiler / `dotnet publish` exit 1 (nuclear)**: removed `ServicePickerControl` UserControl entirely. New Ticket + Quote Builder use inline TextBox/Button/ComboBox (`DisplayMemberPath` only — no DataTemplate, no x:Bind on picker, no NumberBox, no UserControl).
- Quote Builder money fields: all `NumberBox` → `TextBox` (`JobLabourFeeText`, line `QuantityText` / `PartCostText` / …) to avoid DataTemplate + NumberBox + x:Bind compiler failures.
- Added `packaging/dump-xaml-errors.ps1` to print real WMC/CS/XamlCompiler lines from `publish-last.log`.

### Changed
- API / product version `1.2.21` (ClientMinVersion `1.2.21`)
- Catalogue API unchanged; picker UI is intentionally simpler

## 1.2.20 — 2026-09-28

### Fixed
- **XamlCompiler / `dotnet publish` exit 1**: rewrite `ServicePickerControl` without `DataTemplate`, `x:Bind`, or `NumberBox` (DisplayMemberPath + TextBox workflow). Previous string-row `x:Bind` fix was not enough on Windows publish.
- Rebuild script dumps `packaging\out\logs\publish-last.log` tail on failure (`Get-Content … -Tail 80`).

### Changed
- API / product version `1.2.20` (ClientMinVersion `1.2.20`)

## 1.2.19 — 2026-09-28

### Added
- **Service Catalogue**: seeded Phone / Tablet / Laptop & Mac / PC Repair / PC Upgrades / Custom PCs with codes (e.g. `PHONE-SCREEN-REPLACE`, `PC-SSD-UPGRADE`)
- Tables: `service_categories`, `service_bundles`, `service_favourites`, `service_recent_selections`, `device_brands`, `device_models`, `repair_service_lines`; extended `service_pricing`
- API `/api/catalogue/*` — categories, fuzzy service search, CRUD, favourites/recent, bundles, brands/models, idempotent import (`seed.service_catalogue_version`)
- WinUI **ServicePicker** on New Ticket + Quote Builder; Settings → Services full admin
- Multi-service repair lines + per-line complete; warranty days on ticket/quote print
- Permission `pricing.manage_catalogue` (writes also accept `pricing.edit_settings`)

### Fixed
- **XamlCompiler MSB3073** on ServicePicker: DataTemplates no longer `x:Bind` `decimal` / `int?` (`DefaultLabourFee`, `EstimatedMinutes`) to `Run.Text` — use string-only `CatalogueServiceRow` helpers (same rule as Quote Builder NumberBox `double` vs `decimal`)
- ServicePicker NumberBoxes stay unbound (code-behind reads `double` Value); removed unused WPF `System.Windows.Input` using

### Server / Client
- API / product version `1.2.19` (ClientMinVersion `1.2.19`)

## 1.2.18 — 2026-09-28

### Fixed
- **Customer quote print** hardcodes literal `$` + 2 decimals in the HTML template itself (never `ToString("C")` / CultureInfo — that produced `¤` under invariant culture on the server). Deploy fingerprint: `<!-- workshopos-currency:$ -->`
- Print layout is job-level only: Description / Qty / **Amount** (parts at sell, labour stripped from lines) + one **Labour / service** row — never Price/Total with labour baked into line totals
- Dashboard revenue cards no longer use culture `ToString("C")`

### Server
- API / product version `1.2.18` on health (ClientMinVersion remains `1.2.16`)

## 1.2.16 — 2026-09-28

### Fixed
- **Customer quote print** (`GET /api/quotes/{id}/print`): money uses `$` (or business currency symbol) — never culture `¤`; A4 `@page { size: A4; margin: 12mm }` with ~190mm sheet width
- Print lists **parts** (customer sell amounts, no per-line labour) plus one **Labour / service** line; summary Parts / Labour / Tax / Total matches job-level pricing. No cost, markup %, or profit on the customer doc

### Changed
- **Quote pricing**: labour is one fee for the whole job; markup applies once to Σ landed part costs (not per line)
- Quote entity stores job-level `LabourFee`, `MarkupPercent`/`MarkupAmount`, `PartsCostTotal`, `PartsSellTotal`
- QuoteBuilder: remove per-line labour/markup; job Labour NumberBox + Markup %; summary Parts cost → Markup → Parts sell → Labour
- **Quote Builder**: displayed prices use business currency symbol (default `$`)
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
