# Database Entity Model (Phase 1 + forward-looking)

PostgreSQL via EF Core. Soft deletes where business records must remain recoverable. Concurrency tokens on mutable financial entities from Phase 4+.

## Phase 1 tables

### Identity & access

| Entity | Purpose |
| --- | --- |
| `AppUser` | Staff account (extends Identity user): name, phone, status, IsOwner, LocationId |
| `AppRole` | Named role (Owner, Administrator, Manager, Technician, FrontDesk, Sales, ReadOnly) |
| `Permission` | Catalogue key (e.g. `customers.view`) |
| `RolePermission` | Role ↔ permission |
| `RefreshToken` | Hashed refresh tokens, expiry, revoke |
| `AuditEvent` | Immutable security / change log |

### Business configuration

| Entity | Purpose |
| --- | --- |
| `Location` | Workshop site (V1: single default location) |
| `BusinessSetting` | Key/value JSON settings (profile, GST, numbering, theme accent, modules) |
| `ModuleVisibility` | Per-role or global hide of sidebar modules |

## Forward-looking entities (schema stubs from Phase 2+)

Customers, CustomerAddresses, Devices, DeviceModels, Manufacturers, RepairTickets, RepairEvents, RepairNotes, RepairParts, RepairLabour, Quotes, QuoteItems, InventoryItems, InventoryTransactions, InventoryReservations, Suppliers, PurchaseOrders, PurchaseOrderItems, Invoices, InvoiceItems, Payments, Bookings, Notifications, NotificationTemplates, Attachments, PcBuilds, PcBuildParts, RefurbishmentJobs, KnowledgeArticles, QaTemplates, QaResults.

Phase 1 does **not** create all of these tables yet — only what authentication, setup, and settings need. Later phases add migrations incrementally.

## Indexes & constraints

- Unique email on users
- Unique role key, permission key
- Unique setting key
- Index audit by entity type + entity id + createdAt
- FK from user → location, role permissions → role/permission

## Multi-location

`Location` exists from day one. Inventory, users, and jobs will carry `LocationId` when those modules land. V1 setup creates one location and does not expose multi-site UI.
