# Refurbished Devices Overhaul — Audit & Implementation Plan

**Product:** WorkshopOS / TechBench NQ  
**Module:** Refurbished / Used Tech (`used_devices`, Sell Your Tech, refurb diagnostics, WooCommerce publish)  
**Locale:** Australian English, AUD, Australian date formats, configurable GST  
**Principle:** Extend the existing stack — do not replace working frameworks or duplicate customers, parts, accounting, or inventory.

---

## 1. Audit summary (current state)

### Stack

| Layer | Location |
| --- | --- |
| WinUI client | `apps/windows-client/.../UsedTechPage.xaml` + `UsedTechViewModel` |
| API | `UsedTechController` → `/api/used-tech/*` |
| Domain | `UsedDevice` in `OperationsEntities.cs` |
| Service | `UsedTechService` |
| Permissions | `used.view`, `used.manage` (+ `selltech.*` for inbound sell requests) |
| Related | Sell Your Tech → `CreateRefurbFromPurchaseAsync`; `RefurbDiagnostic*`; `WarrantyRecord.UsedDeviceId`; WooCommerce `PublishRefurbishedAsync` |

### What already works

- CRUD list/detail for refurbished stock with brand, model, IMEI/serial, colour, storage, condition grade, status, purchase/parts/asking/sale prices, margin.
- Status lifecycle: Purchased → Inspection → Parts Required → Repair → Testing → Cleaning → Final Quality Check → Ready for Sale → Listed → Reserved → Sold (plus legacy aliases).
- Condition grades: New refurbished / Excellent / Good / Fair (+ legacy A–D).
- Link to seller customer, buyer customer, and optional repair ticket.
- Soft delete via `ArchivedAt`.
- Sell Your Tech public intake + staff offer calculator (`OfferCalculator`) + convert to `UsedDevice` with stock id `TB-RFB-#####` (currently embedded in Notes).
- Refurb diagnostic checklists/results per used device.
- WordPress/WooCommerce publish path and refurbished status sync.
- Repair QA checklists on tickets (separate from refurb diagnostics).
- Warranties can already reference a used device.
- Audit events: `used.create`, `used.update`, `used.status`, `selltech.refurb`, `website.woo.publish`.

### Gaps vs the target platform

| Area | Gap | Phase 1 |
| --- | --- | --- |
| Acquisition leads | Outbound buying (Marketplace/eBay/Gumtree/trade-in/wholesale). Sell Your Tech is inbound only. | API done; WinUI in Phase 2 |
| Stock identity | First-class `StockId` / SKU / barcode / bin / technician | Done (column + assignment) |
| Security checks | Activation Lock / FRP / ownership / sanitisation gates | Columns done; hard gating Phase 3 |
| Costing | Labour, fees, packaging, warranty allowance, ROI reports | Cost columns + calculator/dashboard done |
| Dashboard | Refurbished KPIs / charts / quick actions | API done; WinUI widgets Phase 2 |
| Inspection UI | Diagnostics API exists; Used Tech page has no grading workflow | Later |
| AI Listing Studio | Generic `IAiService` only — no listing generator | Phase 4 |
| Multi-platform listings | Woo publish only | Phase 4–5 |
| Trade-ins / donor parts | Not modelled | Phase 3 / 6 |
| POS / invoice sale | `PosSaleLine` links `InventoryItemId` only — cannot sell a used device through POS | Phase 5 |
| Staff WinUI beyond stock | No Sell Tech inbox, diagnostics, or publish UI | Phase 2+ |
| Permissions | Fine-grained purchase / publish / financials | Seeded; client gates Phase 2 |

### Confirmed risks (do not break)

1. **Status vocabulary drift** — API pipeline is Purchased→Sold; WinUI still offers only `In stock | Reserved | Listed | Sold`. Server remaps `In stock` → `Ready for Sale`. Phase 2 must widen the client filter/form without removing legacy aliases.
2. **Client manage gate** — WinUI treats `inventory.manage` as equivalent to `used.manage`. Do not tighten server rules without updating the client.
3. **Woo sync matching** — Public `refurbished/sync-status` historically matched via `Notes` substring. Phase 1 adds `StockId`; keep dual-read (StockId then Notes/SKU) until sync is updated.
4. **Publish fallback** — Listing without Woo credentials must still mark `Listed` in WorkshopOS.
5. **Create-refurb idempotency** — Sell Your Tech and acquisitions both rely on linked `UsedDeviceId`; preserve single-convert behaviour.

### Reuse map (do not rebuild)

| Need | Reuse |
| --- | --- |
| Customers / sellers | `Customer` + `SellerCustomerId` |
| Parts & POs | `InventoryItem`, `PurchaseOrder`, `IPurchasingService` |
| Repairs | `RepairTicket` + `QaChecklist` + link `RepairTicketId` |
| Offers / profit math | `OfferCalculator` + `website.offer_defaults` settings |
| Diagnostics | `RefurbDiagnosticChecklist` / `RefurbDiagnosticResult` |
| Warranties | `WarrantyRecord` |
| Notifications | `NotificationTemplate` + `NotificationOutbox` |
| WooCommerce | `IWebsiteIntegrationService.PublishRefurbishedAsync` |
| AI provider settings | `IAiService` + existing settings/secrets patterns (`SecretProtector`) |
| Stock numbering | `DocumentSequences` / `NextRefurbStockIdAsync` pattern |
| Auth / RBAC | `PermissionKeys` catalogue + role defaults |
| Audit | `IAuditService` |

---

## 2. Design decisions

1. **Extend `UsedDevice`** — additive columns only; existing API DTOs remain valid; new fields exposed via extended DTOs and optional query params.
2. **New `AcquisitionLead` aggregate** — separate from Sell Your Tech (inbound). Leads convert into `UsedDevice` when purchased/received, preserving history via `AcquisitionLeadId`.
3. **Shared buy calculator** — generalise `OfferCalculator` into a refurbished buy/profit helper used by acquisitions and Sell Your Tech.
4. **Phased delivery** — each phase ships working backend + tests; UI expands without removing the current Used Tech page until the new shell is ready.
5. **Safe migrations** — additive columns/tables with `Down()`; nullable/defaulted fields; no destructive renames of existing data.
6. **AUD / GST** — financial DTOs use existing GST settings; do not assume GST margin scheme eligibility.
7. **Compliance** — ownership declaration and security-check fields required before Ready for Sale / Listed (enforced from Phase 2+; columns in Phase 1).

---

## 3. Phase plan

### Phase 1 — Analysis & foundations *(this delivery)*

- [x] Architecture audit (this document)
- [x] Extend `UsedDevice` (stock id, location, technician, acquisition link, cost breakdown, security checks, listing/Woo ids)
- [x] `AcquisitionLead` + events + offers tables
- [x] Permissions: acquisition + refurbished financials
- [x] Dashboard API aggregating inventory/pipeline/financial KPIs
- [x] Buy/profit calculator endpoint (shared rules)
- [x] Acquisition CRUD + convert-to-inventory
- [x] Automated tests
- [x] Non-breaking: existing `/api/used-tech` and UsedTechPage continue to work

### Phase 2 — Acquisition & inventory UX

- Buying leads UI, marketplace import (manual/authorised), duplicate detection, purchase receipts, redesigned dashboard widgets, IMEI/barcode entry improvements.
- Align WinUI status options with the full API pipeline (keep legacy aliases).
- Prefer `StockId` in Woo sync-status matching; keep Notes fallback.
- Surface stock id / location / security summary on the existing Used Tech page without a full redesign.

### Phase 3 — Refurbishment operations

- Inspection grading UI, repair ticket integration, parts reservation, QC gates, donor/salvage, device-level costing.

### Phase 4 — OpenAI & Listing Studio

- Secure OpenAI integration, listing generation/chat, photo studio, platform templates, approval workflow.

### Phase 5 — WooCommerce & sales

- Draft/publish sync hardening, listing dashboard, sale/stock updates, financial reports.

### Phase 6 — Advanced automation

- Bulk listings, notifications, trade-ins, warranty ops, supplier analytics, ageing, AI ops assistant.

---

## 4. Phase 1 data model (additive)

### `used_devices` new columns

| Column | Purpose |
| --- | --- |
| `StockId` | Canonical `TB-RFB-#####` |
| `Sku` | Public listing SKU (may equal StockId) |
| `Barcode` | Optional scan code |
| `LocationBin` | Storage location |
| `AssignedTechnicianId` | Staff user |
| `ModelNumber` | Manufacturer model number |
| `Carrier` / `SimConfiguration` | Carrier / SIM-eSIM |
| `AcquisitionLeadId` | Link to buying lead |
| `PurchaseDate` / `ReceivedAt` / `ListedAt` / `SoldAt` | Lifecycle timestamps |
| `LabourCost`, `ShippingInCost`, `ShippingOutCost`, `PackagingCost`, `MarketplaceFees`, `PaymentFees`, `WarrantyAllowance` | Costing |
| `IsPartsOnly` | Parts-donor restriction |
| `FunctionalGrade` | Separate from cosmetic `ConditionGrade` |
| Ownership / lock / FRP / lost-stolen / reset / sanitisation flags + notes | Security checks |
| `WooCommerceProductId`, `ListingUrl` | External listing refs |
| `BatteryHealthPercent` | Optional battery health |

### New tables

- `acquisition_leads` — outbound buying opportunities
- `acquisition_offers` — negotiation offers
- `acquisition_events` — status/conversation history

---

## 5. External credentials required later

| Integration | Needed from |
| --- | --- |
| OpenAI API key | Phase 4 |
| WooCommerce REST keys | Already supported; Phase 5 hardens |
| IMEI / blacklist providers | Only if authorised; never claim unsupported checks |
| Facebook / eBay / Gumtree APIs | Official/authorised only — no scraping |

---

## 6. Testing strategy

- Integration tests via `WorkshopApiFactory` (Postgres migrate).
- Phase 1: acquisition CRUD, convert to used device, dashboard counts, buy calculator, permission seeding.
- Regression: existing used-tech create/list/status and Sell Your Tech create-refurb.

---

## 7. Progress log

| Date | Milestone |
| --- | --- |
| 2026-10-15 | Audit complete; Phase 1 foundations shipped (API 1.2.31) |
| 2026-10-15 | Independent stack audits folded in (status drift, POS gap, Woo sync dual-read, WinUI permission gate) |
