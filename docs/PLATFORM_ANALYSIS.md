# WorkshopOS (TechBench NQ) — Platform Architecture Analysis

**Product:** WorkshopOS — self-hosted repair-shop operations for Australian electronics / computer repair businesses (TechBench NQ branding via website config).  
**Analyzed version:** codebase at **1.2.27+** (see `CHANGELOG.md`, `README.md`).  
**Scope:** Windows client nav, API controllers, EF model, PcBuild / CustomPc coupling, gaps vs a full RepairShop OS surface.

Status legend used below:

| Status | Meaning |
| --- | --- |
| **Working** | Dedicated client UI + API with real CRUD / workflow (not fake empty success) |
| **Partial** | API and/or thin client (e.g. `GenericListPage`); core path works, depth missing |
| **Stub** | Placeholder UI, marker implementation, or API present with no staff client path |

---

## 1. Architecture summary

### Stack

| Layer | Technology | Path |
| --- | --- | --- |
| Desktop client | WinUI 3 / .NET 8, MVVM (`CommunityToolkit.Mvvm`) | `apps/windows-client/WorkshopOS.Client` |
| API | ASP.NET Core REST + OpenAPI + SignalR hub | `services/api/WorkshopOS.Api` |
| Worker | .NET hosted service (DB heartbeat + notification outbox) | `services/worker/WorkshopOS.Worker` |
| Domain / app / infra | Clean-ish layering | `services/shared/WorkshopOS.{Domain,Application,Infrastructure,Contracts}` |
| Database | PostgreSQL 16 via EF Core | `WorkshopDbContext` + `Persistence/Migrations/*` |
| Deploy | Docker Compose | `docker/` |
| Packaging | Inno Setup Setup.exe (GitHub Releases) | `packaging/` |

Currency / tax posture: **AUD / GST** (configurable at setup). Client **never** talks to PostgreSQL (`docs/ARCHITECTURE.md`).

### Solution projects

```
apps/windows-client/WorkshopOS.Client
services/api/WorkshopOS.Api
services/worker/WorkshopOS.Worker
services/shared/WorkshopOS.Domain
services/shared/WorkshopOS.Application
services/shared/WorkshopOS.Infrastructure
services/shared/WorkshopOS.Contracts
tests/WorkshopOS.Api.Tests
```

### How the WinUI client talks to the API

1. **Pair / discover** — `GET /api/discovery`, `/connect` pairing code (`WOS-XXXX`), optional LAN find (`DiscoveryController.cs`).
2. **Persist connection** — `%LOCALAPPDATA%\WorkshopOS\client-settings.json` via `AppSettingsStore` (`apps/windows-client/WorkshopOS.Client/Services/ApiClient.cs` region / settings store).
3. **Auth** — `POST /api/auth/login` → JWT access + hashed refresh; `ApiClient` attaches `Authorization: Bearer` and refreshes on expiry.
4. **REST** — typed `GetAsync` / `PostAsync` / `PutAsync` / `DeleteAsync` against relative paths (`api/...`).
5. **Modules** — shell loads `GET /api/settings/modules` → builds sidebar from `ShellViewModel.NavGroups` (`ViewModels.cs`) + `ShellPage.BuildNavigation()`.
6. **Live updates** — API maps SignalR hub `/hubs/workshop` (`Program.cs`, `Hubs/WorkshopHub.cs`), but the **client has no HubConnection** (grep empty). Notifications use **HTTP polling** (~20s) in `ShellViewModel`.
7. **Print** — quote/repair HTML from API (`/print`) opened in WinUI print flow.

Topology (from `docs/ARCHITECTURE.md`):

```
WinUI Client ──HTTPS REST (+ JWT)──► ASP.NET Core API ──► PostgreSQL
                                         │
                                         ├── Worker (outbox / heartbeat)
                                         ├── Optional Redis (compose; not required for core)
                                         └── Optional Ollama AI
```

---

## 2. Feature map

### 2.1 Client sidebar modules (`ShellViewModel.NavGroups` + `ShellPage.NavigateToModule`)

Source of truth for **what appears in the nav**:  
`apps/windows-client/WorkshopOS.Client/ViewModels/ViewModels.cs` (`NavGroups`) and  
`apps/windows-client/WorkshopOS.Client/Views/ShellPage.xaml.cs`.

| Nav key | Title | Client page | Primary API | Status |
| --- | --- | --- | --- | --- |
| `dashboard` | Dashboard | `DashboardPage` | `GET /api/dashboard` | **Working** |
| `repairs` | Tickets | `RepairsPage`, `NewRepairPage`, `RepairDetailPage` | `/api/repairs/*` | **Working** |
| `quotes` | Quotes | `QuotesPage`, `QuoteBuilderPage` | `/api/quotes/*`, `/api/pricing/*` | **Working** |
| `invoices` | Invoices | `GenericListPage` → `api/invoices` | `/api/invoices`, `/payments` | **Partial** — list + create-from-repair; no invoice detail / payment UI |
| `calendar` | Calendar | `CalendarPage` | `/api/bookings/*` | **Working** |
| `inventory` | Inventory | `InventoryPage` | `/api/inventory/*` | **Partial** — list + create; no adjust / reserve / consume UI |
| `builds` | PC Builds | `PcBuildsPage`, `PcBuildBuilderPage` | `/api/builds/*` | **Working** — parts + stock reservation |
| `purchasing` | Purchasing | `GenericListPage` → `api/purchase-orders` | `/api/purchase-orders`, `/api/suppliers` | **Partial** — create PO; receive is API-only |
| `used` | Refurbished | `UsedTechPage` | `/api/used-tech/*` | **Working** |
| `customers` | Customers | `CustomersPage`, `CustomerDetailPage` | `/api/customers`, `/api/devices` | **Working** |
| `reports` | Reports | `ReportsPage` | `GET /api/reports/summary` | **Partial** — summary only (roadmap: CSV/PDF later) |

**Chrome (not in `NavGroups`):**

| Surface | Status |
| --- | --- |
| Settings gear → `SettingsPage` (business, pricing, services, tax, users, backups, connection) | **Working** (pricing/catalogue rich) |
| Notifications bell → flyout + `GenericListPage` (`api/notifications`) | **Partial** — inbox + mark-read; no SignalR push |
| New ticket shortcut → `NewRepairPage` | **Working** |

### 2.2 Modules returned by API but not in slim sidebar

From `SettingsService.GetModulesAsync` in `services/shared/WorkshopOS.Infrastructure/Services/AppServices.cs` (all currently `Implemented: true`):

| Key | API modules flag | Client | Status |
| --- | --- | --- | --- |
| `notifications` | yes | Bell / GenericList | **Partial** |
| `knowledge` | yes | **No** `NavigateToModule` case → falls through to `PlaceholderPage` | **Stub** (client); API `GET/POST /api/knowledge` exists |
| `ai` | yes | `AiAssistPage` exists but **not wired** in `ShellPage` switch → `PlaceholderPage` if navigated by key | **Stub** (nav); API `POST /api/ai/assist` |
| `backups` | yes | Settings pivot / `BackupsPage` | **Partial** — marker file, not `pg_dump` |
| `users` / `settings` | yes | Settings hub | **Working** |

### 2.3 `PlaceholderPage` usage

| File | Role |
| --- | --- |
| `apps/windows-client/WorkshopOS.Client/Views/PlaceholderPage.xaml(.cs)` | “Not yet implemented — Phase N” |
| `ShellPage.NavigateToModule` | Navigates here when `implemented == false` **or** when key has no `switch` case |
| `App.xaml.cs` | DI registration |

With current `GetModulesAsync`, every catalogue module is `Implemented: true`. Practical hits today: **`knowledge` / `ai`** (true + missing switch), or offline/unknown keys.

### 2.4 API controllers — route inventory

Files under `services/api/WorkshopOS.Api/Controllers/`:

#### `ApiControllers.cs`

| Route prefix | Endpoints |
| --- | --- |
| `api/setup` | `GET status`, `POST` |
| `api/auth` | `login`, `refresh`, `logout`, `me`, `PUT password` |
| `api/settings` | `GET/PUT business`, `GET modules` |
| `api` | `GET health`, `GET permissions`, `GET search` |
| `api/roles` | `GET`, `POST`, `PUT {id}` |
| `api/system` | `GET info`, `POST restart` |
| `api/users` | `GET`, `POST`, `PUT {id}`, `PUT {id}/password` |

#### `WorkshopControllers.cs`

| Route prefix | Endpoints |
| --- | --- |
| `api/customers` | list, get, create, update |
| `api/devices` | list, create, update |
| `api/repairs` | lookups, list, get, print, create, status, priority, assign, notes, diagnosis, complete service-line |

#### `OperationsControllers.cs`

| Route prefix | Endpoints |
| --- | --- |
| `api/dashboard` | `GET` |
| `api/quotes` | CRUD-ish + send/accept/decline/revise/convert/print |
| `api/pricing` | settings, preview, tiers, services |
| `api/invoices` | list, get, from-repair, payments |
| `api/inventory` | list, create, adjust, reserve, consume, release |
| `api/suppliers` | list, create |
| `api/purchase-orders` | list, create, receive |
| `api/notifications` | list, mark read |
| `api/bookings` | list, get, create, update, cancel |
| `api/knowledge` | list, create |
| `api/builds` | list, get, create, update, status, delete |
| `api/used-tech` | list, get, create, update, status |
| `api/repairs/{ticketId}/qa` | list, post result |
| `api/reports` | `GET summary` |
| `api/ai` | `POST assist` |
| `api/backups` | list, create |
| `api/health` | `GET detail` |

#### `CatalogueController.cs` — `api/catalogue/*`

Categories, services, import, favourites, recent, bundles, brands, models.

#### `DiscoveryController.cs`

`GET api/discovery`, `GET connect`, `POST api/discovery/regenerate-code`.

#### `WebsiteControllers.cs`

| Area | Prefix | Notes |
| --- | --- | --- |
| Public site | `api/public/*` | repair-quote, booking, sell-device, custom-pc, status, approve/decline, availability, messaging, refurbished sync |
| Customer portal | `api/customer/*` | login/register, repairs, quotes, devices, invoices, warranties, messages, sell-requests |
| Sell Your Tech (staff) | `api/sell-tech/*` | list, offer, send, create-refurb, offer-defaults |
| Messaging (staff) | `api/messages/*` | threads, reply, ensure for repair |
| Website admin | `api/website/*` | API keys, WooCommerce, dashboard, QR, templates, webhooks, booking settings |
| Custom PC (staff) | `api/custom-pc/*` | list, get, status, convert-to-build |
| Refurb diagnostics | `api/refurb-diagnostics/*` | checklists, results |

**Client UI for website / sell-tech / custom-pc / messages / QA:** **absent** (API-only as of 1.2.27). Status for those staff workflows in the WinUI app: **Stub**.

---

## 3. Database

### 3.1 Major `DbSet`s (`WorkshopDbContext.cs`)

Path: `services/shared/WorkshopOS.Infrastructure/Persistence/WorkshopDbContext.cs`.

**Identity & config:** `Users`, `Roles`, `Permissions`, `RolePermissions`, `RefreshTokens`, `Locations`, `Settings`, `AuditEvents`

**CRM / repairs:** `Customers`, `Devices`, `RepairStatuses`, `RepairTypes`, `RepairPriorities`, `DocumentSequences`, `RepairTickets`, `RepairEvents`, `RepairNotes`, `RepairServiceLines`

**Quotes / pricing / catalogue:** `Quotes`, `QuoteLines`, `QuoteRevisions`, `QuoteAuditEntries`, `MarkupTiers`, `ServicePricings`, `ServiceCategories`, `ServiceBundles`, `ServiceBundleItems`, `ServiceFavourites`, `ServiceRecentSelections`, `DeviceBrands`, `DeviceModels`

**Finance / stock:** `Invoices`, `InvoiceLines`, `Payments`, `InventoryItems`, `InventoryTransactions`, `InventoryReservations`, `Suppliers`, `PurchaseOrders`, `PurchaseOrderLines`

**Ops:** `Notifications`, `Bookings`, `KnowledgeArticles`, `PcBuilds`, `PcBuildParts`, `UsedDevices`, `QaChecklists`, `BackupRecords`

**Website / portal:** `CustomerPortalAccounts`, `WebsiteApiKeys`, `SellDeviceRequests`, `SellDeviceOffers`, `SellDeviceEvents`, `ConversationThreads`, `ConversationMessages`, `Warranties`, `NotificationTemplates`, `NotificationOutboxItems`, `QuoteApprovals`, `CustomPcRequests`, `RefurbDiagnosticChecklists`, `RefurbDiagnosticResults`

### 3.2 Key entities (high signal)

| Entity | Table | Notes |
| --- | --- | --- |
| `RepairTicket` | `repair_tickets` | `REP-…` numbering; PIN / public access token for website tracking |
| `Quote` + lines | `quotes`, `quote_lines` | Job-level markup/labour; revisions; freeze on accept |
| `InventoryItem` + `InventoryReservation` | stock + reservations | Reservation ties to **either** `TicketId` **or** `PcBuildId` |
| `PcBuild` + `PcBuildPart` | `pc_builds`, `pc_build_parts` | Soft-delete; statuses Quoted→Sold; `ReservationId` on parts |
| `CustomPcRequest` | `custom_pc_requests` | Website intake; optional `PcBuildId` after convert |
| `UsedDevice` | `used_devices` | Refurb / sell stock; Woo publish path |
| `SellDeviceRequest` / `Offer` | sell-tech funnel | Offer calculator → optional refurb create |
| `BusinessSetting` | `settings` (jsonb) | Profile, GST, pricing, module visibility, Woo credentials |

**Doc drift:** `docs/DATABASE.md` still describes many Phase-2+ tables as “schema stubs” and lists entities (`Attachments`, `RepairParts`, `RepairLabour`, `CustomerAddresses`, …) that are **not** present as DbSets. Prefer `WorkshopDbContext` over that doc.

### 3.3 Migrations (chronological)

| Migration | Focus |
| --- | --- |
| `20260922084928_InitialPhase1` | Auth, roles, settings, audit, location |
| `20260922090941_Phase2CustomersDevicesRepairs` | CRM + tickets |
| `20260922094502_Phase3to12Operations` | Quotes, inventory, builds, bookings, etc. |
| `20260926024421_QuotePricingSystem` | Pricing depth |
| `20260927090621_RefurbishedUsedDeviceFields` | Used tech fields |
| `20260927132046_PcBuildComponentReservations` | Nullable ticket FK; `PcBuildId` on reservations; component type |
| `20260928075157_JobLevelQuotePricing` | Job-level quote maths |
| `20260928103239_ServiceCatalogue` | Catalogue graph |
| `20261006075033_WebsiteWordPressIntegration` | Portal, sell-tech, `custom_pc_requests`, warranties, outbox, … |

---

## 4. What already works well

- **Auth / setup / roles** — First-run setup, JWT + refresh, seven seeded roles, custom roles, permission catalogue (`PermissionKeys.cs`).
- **Repairs** — Intake, ticket numbers, status/priority/assign, notes, diagnosis, service lines, print, dashboard pipeline.
- **Quotes & pricing** — Server-side `PricingCalculator` / preview, markup tiers, GST, quote builder UI, send/print/convert, revision-aware website approval.
- **Service catalogue** — Categories, bundles, favourites, brands/models, seed JSON (`api/catalogue/*` + Settings → Services).
- **Inventory core** — SKUs, on-hand/reserved/available, reservations against tickets **and** PC builds (API + build UI).
- **PC Builds** — Dedicated builder UI; reserve/consume/release stock on status transitions; API tests (`PcBuildReservationTests.cs`).
- **Customers / devices** — List, create, detail.
- **Calendar** — Month/week booking grid (post-1.2.24).
- **Refurbished / used tech** — Staff CRUD + margin fields; website publish path.
- **Website API (1.2.26+)** — Public + portal + staff sell-tech/messages/Woo/webhooks; WorkshopOS as source of truth (`docs/WORDPRESS_INTEGRATION.md`).
- **Reports summary** — Revenue / COGS / GP / repair counts / payments by method.
- **Ops honesty** — Empty lists stay empty; AI disabled message; SMS/email skip when providers missing (`FEATURES.md`).

---

## 5. Partial implementations

| Area | Evidence |
| --- | --- |
| **Invoices / payments** | API: from-repair + record payments. Client: GenericList create only — no balance UI / refunds UI. |
| **Purchasing** | Create PO (+ supplier) in GenericList; `POST …/receive` not exposed in client. |
| **Inventory depth** | Adjust / reserve / consume exist on API; Inventory page is create+list. |
| **Notifications** | In-app inbox + outbox queue; SMS/SMTP adapters skip with “not configured”; no client SignalR. |
| **Backups** | Writes `.sql.marker` placeholder (`BackupService`), not full dump/restore. |
| **AI Assist** | Optional Ollama; page exists; not in sidebar / shell switch. |
| **Knowledge** | API list/create; client Placeholder. |
| **QA** | `api/repairs/{id}/qa` — no dedicated client surface found. |
| **Reports** | Summary only; export called out as later in `ROADMAP.md`. |
| **SignalR** | Hub accepts connections only (`WorkshopHub`); no domain event broadcast; client unused. |
| **WooCommerce** | Settings + publish implemented with graceful fallback when credentials missing. |
| **Worker** | Outbox drain + heartbeat only (`WorkshopOS.Worker/Program.cs`). |
| **Multi-location** | `Location` table; V1 single active site (`ARCHITECTURE.md`). |
| **Docs** | `CLIENT_NAVIGATION.md` / `DATABASE.md` / `PERMISSIONS.md` lag code (slim nav, live permissions, full schema). |

---

## 6. Clearly missing vs a full RepairShop OS vision

Categorized from **absence** in entities, `PermissionKeys`, API routes, FEATURES/ROADMAP — not speculative product planning.

### Absent (no schema / API / client)

| Category | Notes |
| --- | --- |
| **POS / retail checkout** | No register, tender UI, cash drawer, or retail sale entity beyond invoice-from-repair |
| **Gift cards** | No entities or permissions |
| **Marketing campaigns** | No campaign/list/blast module (transactional templates ≠ marketing) |
| **Loyalty / store credit** | Not present |
| **Attachments / media store** | Mentioned as forward-looking in old `DATABASE.md`; no `Attachments` DbSet (website stores URLs only) |
| **Separate RepairParts / RepairLabour ledgers** | Replaced conceptually by quote lines + `RepairServiceLine` |
| **Multi-store ops UI** | Schema hook only |
| **Staff client for website ops** | Sell-tech, custom-pc inbox, messages, website keys — API only |
| **Time clock / payroll / accounting export** | Not present |
| **Hardware integrations** | No barcode scanner / payment terminal modules in repo |

### Intentionally deferred / optional (documented)

| Category | Notes |
| --- | --- |
| SMS / email delivery providers | Outbox retained; skip until configured |
| Ollama AI | Off by default |
| Redis / S3 | Compose-ready / “later” in architecture |
| PDF polish / CSV export | Called out in `ROADMAP.md` / invoice phase notes |
| Full `pg_dump` backups | Marker plumbing only |

---

## 7. Technical debt

1. **Doc drift** — `CLIENT_NAVIGATION.md` still lists WORKSHOP/SALES/STOCK groupings and Knowledge/AI in primary nav; code uses slim `NavGroups` (1.2.7+). `DATABASE.md` understates current schema. `API.md` still says SignalR is Phase-1 connect-only while FEATURES implies live events.
2. **`Implemented: true` vs shell switch** — Knowledge/AI marked implemented in modules API but open `PlaceholderPage` — misleading for admins toggling module visibility.
3. **GenericList dual-purpose** — Invoices/POs share a create-panel VM; easy to stall feature depth (payments, receive, edit).
4. **No concurrency tokens** — `DATABASE.md` promised them for financial entities; grep shows none (`IsConcurrencyToken` / `xmin` unused).
5. **SignalR unused by client** — Polling duplicates a hub that does nothing with domain events.
6. **Backup false confidence** — Marker files can look like backups in Settings.
7. **Permission doc vs code** — `PERMISSIONS.md` still says many keys are “reserved unused”; `PermissionKeys.Catalogue` is fully seeded and enforced on controllers.
8. **WinUI packaging fragility** — CHANGELOG shows repeated XamlCompiler / overlay rebuild issues (1.2.21–1.2.22 era); Setup.exe remains the supported install path.
9. **Search UX** — Shell search hits `api/search` but only updates a status string (no results navigation UI depth).
10. **Website staff surface gap** — Rich 1.2.26+ API with zero WinUI screens increases ops dependence on WordPress admin / raw API.

---

## 8. Migration risks — `PcBuild` / `CustomPc`

### Dual models

| Concept | Entity | Table | Origin |
| --- | --- | --- | --- |
| Internal build job | `PcBuild` | `pc_builds` | Staff / convert |
| Website lead | `CustomPcRequest` | `custom_pc_requests` | `POST /api/public/custom-pc` |

Link: `CustomPcRequest.PcBuildId` → `PcBuild` (migration `WebsiteWordPressIntegration`). Convert: `CustomPcRequestService.ConvertToBuildAsync` → `IPcBuildService.CreateAsync` (`WebsiteWorkflowServices.cs`).

### Reservation coupling (high risk)

Migration `PcBuildComponentReservations`:

- `inventory_reservations.TicketId` became **nullable**
- Added `PcBuildId` + composite index `(PcBuildId, Status)`
- FK to `pc_builds` with **Restrict** delete
- Parts gained `Quantity`, `ReservationId`; inventory gained `ComponentType`

Runtime rules (`PcBuildService` / inventory reserve):

- Exactly one of `TicketId` / `PcBuildId`
- Status transitions **consume** or **release** reservations (Completed/Sold vs Cancelled)
- Editing parts releases and re-reserves — concurrent edits or partial migration failures can orphan reserved qty

**Risks if refactoring tables:**

1. Dropping/merging `pc_builds` without rewriting reservations breaks stock available maths.
2. Soft-delete (`PcBuild.ArchivedAt`) vs hard FK Restrict — archive must release reservations first (service does; raw SQL must not skip).
3. `CustomPcRequest` is **not** `SoftDeleteEntity`; deleting builds referenced by website requests needs FK strategy (currently optional FK, no cascade in migration).
4. Status vocabularies differ (`CustomPcRequest.Status` free string vs build Quoted/Reserved/Building/…) — reporting joins need care.
5. Convert-to-build is idempotent on existing `PcBuildId` but does not sync later website field edits into the build.
6. Future “unify CustomPc into PcBuild” migration must move reservation FKs, part rows, website references, and public DTO contracts together (`WebsiteFlowTests`, `PcBuildReservationTests`).

### Recommended caution

Treat `pc_builds` + `inventory_reservations.PcBuildId` + `custom_pc_requests.PcBuildId` as a **single migration unit**. Prefer additive columns over renames until website plugin + WinUI builder versions are coordinated.

---

## 9. Permissions snapshot

Catalogue: `services/shared/WorkshopOS.Domain/Security/PermissionKeys.cs`.

Groups include: tickets, customers, devices, inventory (+ POs), quotes/invoices/payments/pricing, reports, builds, used, bookings, staff/roles, knowledge, AI, settings, integrations, **website**, **selltech**, **messages**, backups, audit, incidents, privacy.

Website-era keys (`website.manage`, `selltech.*`, `messages.*`) exist server-side; WinUI does not yet gate nav items for those modules (no nav items).

---

## 10. Source index (quick)

| Topic | Path |
| --- | --- |
| Shell nav | `apps/windows-client/WorkshopOS.Client/Views/ShellPage.xaml.cs` |
| Nav groups | `apps/windows-client/WorkshopOS.Client/ViewModels/ViewModels.cs` |
| Module catalogue | `AppServices.GetModulesAsync` in `…/Services/AppServices.cs` |
| Controllers | `services/api/WorkshopOS.Api/Controllers/*.cs` |
| DbContext | `services/shared/WorkshopOS.Infrastructure/Persistence/WorkshopDbContext.cs` |
| Permissions | `services/shared/WorkshopOS.Domain/Security/PermissionKeys.cs` |
| PcBuild entity | `…/Domain/Entities/OperationsEntities.cs` |
| CustomPc entity | `…/Domain/Entities/WebsiteEntities.cs` |
| Product features | `docs/FEATURES.md`, `docs/ROADMAP.md`, `docs/WORDPRESS_INTEGRATION.md` |
| Architecture | `docs/ARCHITECTURE.md` |

---

## 11. Phase 1 execution note (1.2.28)

Completed in-repo:

- Custom PC Builds removed from modules, client nav, `/api/builds`, `/api/custom-pc`, public custom-pc (410 Gone); tables kept.
- Shell IA regrouped; AI + Knowledge wired; repair filtered nav keys.
- Command centre + expanded `GET /api/search`.
- Plan: `docs/IMPLEMENTATION_PLAN.md`.

*Prefer this document + IMPLEMENTATION_PLAN over older phase stubs until DATABASE.md is refreshed.*
