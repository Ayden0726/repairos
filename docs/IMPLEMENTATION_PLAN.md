# TechBench NQ Platform — Implementation Plan

**Basis:** `docs/PLATFORM_ANALYSIS.md` (WorkshopOS 1.2.27 codebase)  
**Product direction:** Original TechBench NQ repair-business OS (RepairTree-class breadth; not a clone).  
**Rules:** Reuse existing APIs; no fake buttons; no destructive data loss; Custom PC Builds removed from product surface (tables retained).

---

## Immediate decisions

| Decision | Action |
| --- | --- |
| Custom PC Builds | Remove from nav, modules catalogue, public/staff Custom PC APIs, and website convert-to-build. Keep `pc_builds` / `custom_pc_requests` tables (no drop migration). Desktop repairs use normal repair tickets. |
| Navigation | Restructure around real modules only. Future modules (POS, Marketing, Gift Cards…) appear only when they have real pages + APIs. |
| Search | Expand server search + Command Centre (Ctrl+K) with results navigation and command shortcuts. |
| Branding | Keep WorkshopOS engine name in API; client chrome emphasises business name (TechBench NQ when configured). |

---

## Phase roadmap (aligned to product brief)

| Phase | Focus | Status |
| --- | --- | --- |
| **1** | Shell, nav IA, permissions-aware modules, design tokens, global search / command centre; remove Custom PC surface | **In progress (this release)** |
| **2** | Customers / devices CRM depth (duplicates, IMEI history, segments foundation) | Next |
| **3** | Repair ticket architecture (board/kanban, intake wizard, photos, signatures, calendar overlays) | Planned |
| **4** | Price book, quotes, digital approvals, deposits, checklists, warranty claims | Planned (quotes/approvals partially exist) |
| **5** | Inventory depth, barcodes, stocktake, special orders, suppliers | Planned (inventory/PO partial) |
| **6** | Full POS, registers, held sales, refunds, exchanges | **Started (1.2.30)** — enable gate, sales, stock, repair deposits/payoffs, discount presets, EFTPOS docs; held sales/refunds later |
| **7** | Invoices, pay-by-link, credit notes, statements, recurring | Planned |
| **8** | SMS/email providers, public tracker polish, review requests | Planned (templates/outbox exist) |
| **9** | Bookings public page + embed | Planned (bookings API + calendar exist) |
| **10** | Gift cards, store credit, trade-ins UI | Planned (refurbished/sell-tech API partial) |
| **11** | Reporting centre + AI analyst (read-only) + AI diagnosis | Planned |
| **12** | Marketing campaigns / consent / audiences | Planned |
| **13** | Multi-location transfers / stock requests | Planned |
| **14** | Accounting (Xero) integrations | Planned |
| **15** | Security (2FA, sessions, anomaly feed), backups real dump | Planned |
| **16** | Sandbox + CSV import wizards | Planned |
| **17** | Optional B2B webshop / social scheduler | Planned |
| **18** | Performance, a11y, E2E repair→POS→warranty test, polish | Continuous |

---

## Phase 1 deliverables

1. Analysis docs (`PLATFORM_ANALYSIS.md`, this plan).
2. Custom PC removed from client nav/modules/public Custom PC intake/staff Custom PC API.
3. Sidebar IA: Dashboard · Repairs (filtered views) · Bookings · Customers · Quotes · Invoices · Inventory · Refurbished · Reports · AI · Knowledge · Settings.
4. Expanded `GET /api/search` (tickets, customers, devices/IMEI/serial, invoices, quotes, inventory SKU, bookings, refurbished, suppliers, staff).
5. Command Centre: Ctrl+K opens search; commands for New Repair / New Customer / New Booking / Create Quote; hit navigation into real pages.
6. Wire AI Assist + Knowledge to real pages (no Placeholder).
7. Version bump + tests updated.

---

## Risks to watch

- Do not drop `pc_builds` while `inventory_reservations.PcBuildId` exists.
- WordPress plugin may still call `POST /api/public/custom-pc` — return clear **410 Gone** with message to use repair quote / ordinary desktop repair intake.
- Existing WinUI installs: module list change is server-driven; rebuild client for Command Centre UI.
