# TechBench NQ Workshop OS — Master Spec V2 Development Plan

**Spec:** Master Development Specification — Version 2  
**Codebase baseline:** WorkshopOS **1.2.41** (`ProductVersions`) — Home / side-hustle workshop profile  
**Date:** 2026-11-10 (Australia/Brisbane)  
**Rules applied:** Inspect first · no greenfield rebuild · no mock buttons · migrations only · preserve data/auth · phased delivery  
**Out of scope:** Board-level / microsoldering repair — will not be built.

This document is the Section 30 **before-implementation** report. It supersedes the high-level status in `docs/IMPLEMENTATION_PLAN.md` for V2 prioritisation; that file remains for historical Phase 1–18 tracking.

---

## 1. Existing architecture summary

| Layer | Stack | Location |
| --- | --- | --- |
| Desktop client | WinUI 3 · .NET 8 · CommunityToolkit.Mvvm | `apps/windows-client/WorkshopOS.Client` |
| API | ASP.NET Core · JWT · permission policies · SignalR hub (connect-only) | `services/api/WorkshopOS.Api` |
| Domain / app / infra | EF Core · PostgreSQL 16 · AES-GCM secrets | `services/shared/WorkshopOS.*` |
| Worker | Outbox drain · expense drafts · heartbeat | `services/worker/WorkshopOS.Worker` |
| Deploy | Docker Compose (+ Pi overlay) · Inno Setup client | `docker/`, `packaging/` |
| Website | Public / portal / staff REST for WordPress/10Web | `WebsiteControllers.cs`, `docs/WORDPRESS_INTEGRATION.md` |

**Invariants to preserve**

- Single-shop self-host with optional multi-site Locations (full SaaS multi-tenant / licensing deferred).
- Australian locale: AUD, GST, FY Jul–Jun.
- Board-level repair is explicitly **won’t do**.
- JWT auth + `PermissionKeys` role catalogue (7 seeded roles).
- Auto-migrate on API/worker start — all schema changes via EF migrations.
- Encrypted integration credentials (`Encryption__Key` ≠ JWT).
- Do **not** drop `pc_builds` / related FKs while product surface is retired.

---

## 2. Existing feature inventory (vs Spec V2)

Status key: **Working** · **Partial** · **Missing** · **Stub**

| Spec § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 2 | Device label printing | **Partial** | Quote/repair HTML print + website QR payload; no Brother/Zebra/templates/batch/preview |
| 3 | Inventory management | **Partial** | Items, adjust/reserve/consume APIs; thin WinUI; no stocktake, multi-bin transfers UI, quarantine model |
| 4 | Suppliers & purchasing | **Partial** | Suppliers + PO create/receive API; GenericList create; receive not in WinUI |
| 5 | Donor / harvesting | **Partial** | `IsPartsOnly` flag; no donor entity / harvest workflow / traceability |
| 6 | Refurbished management | **Partial** | Used-tech CRUD + acquisitions API + cost columns; WinUI status set slim vs server pipeline |
| 7 | Intake photos / condition / signatures | **Partial** | Condition flags on tickets; no photo store; no digital signature |
| 8 | Diagnostics & QC | **Partial** | Repair QA + refurb diagnostics APIs; **no WinUI** |
| 9 | Board-level repair | **Won’t do** | Explicitly out of scope — no microsoldering / board-level workflow |
| 10 | Compatibility database | **Working** | Parts fitment graph API + WinUI (1.2.40); catalogue brand/model also remains |
| 11 | Workflow & time tracking | **Partial** | Status pipeline exists; no labour timers / actual vs estimate ledger |
| 12 | Repeat repair detection | **Partial** | IMEI duplicate hits; no warranty-return / same-fault workflow |
| 13 | Customer portal | **Partial** | Full customer API; WordPress expected; no first-party portal UI |
| 14 | Appointment booking | **Working** | Staff calendar + public booking APIs |
| 15 | Loan devices | **Missing** | — |
| 16 | POS, deposits, refund approvals | **Partial** | POS sales/void/deposits; no refund approval flow; no live Square terminal push |
| 17 | Financial analytics | **Partial** | Dashboard + expenses + reports; COGS fallback can invent 45% — must fix |
| 18 | Website / online store | **Partial** | Woo sync + public APIs; staff WinUI thin |
| 19 | Warranty & returns | **Partial** | Warranty records + portal list; no claims staff UI |
| 20 | Equipment / scheduling / knowledge | **Partial** | Knowledge list exists; equipment register & staff roster **Missing** |
| 21 | Offline sync | **Working** | Client offline queue + `POST /api/sync/push` outbox ack + replay (1.2.40) |
| 22 | DR & restore testing | **Stub** | Backup writes `.sql.marker` — not real `pg_dump` |
| 23 | Integration failure management | **Partial** | Credential tests; no central health/retry dashboard |
| 24 | Advanced activity monitoring | **Partial** | `AuditEvent` writes; no list API/UI |
| 25 | Automated testing & deployment | **Partial** | Strong API xUnit suite; limited E2E/frontend; GH release workflow exists |
| 26 | Commercial / multi-tenant / licensing | **Partial** | Multi-site Locations + inventory bins UI (1.2.40); SaaS tenants / licensing not planned |
| 27–29 | Architecture / security / validation | **Ongoing** | Modular enough to extend; keep permission gates on every new surface |

---

## 3. Missing functionality (priority clusters)

1. **Trust & money integrity** — real backups/restore tests, honest COGS, refund approvals, audit browser.
2. **Stock ops depth** — bins/locations UI, stocktake, reservation UX, PO receive, barcode path.
3. **Shop floor depth** — intake photos, QC checklists UI, time tracking, label printing hardware.
4. **Refurb / donor** — full status pipeline UI, acquisitions WinUI, donor harvest + component traceability.
5. **Customer-facing** — warranty claims, loaners, portal polish, website staff inbox.
6. **Platform** — offline sync and multi-site (done in 1.2.40); SaaS licensing / public API productisation later. Board-level repair is out of scope.

---

## 4. Known bugs / risks / duplication

| Risk | Severity | Action |
| --- | --- | --- |
| Backup markers look like real dumps | High | Replace with `pg_dump` + verification + restore drill |
| Reports use `revenue × 0.45` when COGS missing | High | Fail closed or show “unknown COGS”; never invent margin |
| Docs drift (`DATABASE.md`, FEATURES SignalR, PLATFORM “no POS”) | Med | Refresh docs as Phase 0 |
| Refurbished WinUI statuses ≠ server pipeline | Med | Align enums + UI in refurbished phase |
| POS cannot sell `UsedDevice` | Med | Dedicated sell path in POS phase |
| PcBuild tables retained with Restrict FKs | Med | Keep; never casual drop |
| Dual intake: Sell-tech vs Acquisitions | Low | Unify UX later; keep APIs |
| SignalR hub unused (client polls) | Low | Wire or document as future |

---

## 5. Proposed database changes (by phase — not all at once)

| Phase | Migrations (illustrative) |
| --- | --- |
| A | None required for COGS/audit list; backup metadata table optional |
| B | `stocktake_sessions`, `stocktake_lines`; optional `inventory_locations` hierarchy; quarantine qty if not modelled |
| C | `repair_attachments`, `repair_signatures`, `repair_time_entries` |
| D | `label_templates`, `printer_profiles`, `print_jobs` |
| E | `donor_devices`, `harvested_components`, `component_movements`; expand used-device status if needed |
| F | `loan_devices`, `loan_agreements`; `warranty_claims` |
| G | `integration_health_events`; offline `sync_outbox` (client-side + server ack) |
| H | Multi-site Locations + `inventory_bins` + `part_fitments` + `sync_outbox` (1.2.40). SaaS `tenants` / licensing only after isolation design review — not board-level |

Every change: EF migration · seed permissions · never destructive drop of business data.

---

## 6. Proposed API changes (by phase)

- **A:** `GET /api/audit`, backup download/verify/restore-test endpoints (admin-only).
- **B:** Stocktake CRUD; inventory location CRUD; PO receive WinUI-backed endpoints already exist — extend as needed.
- **C:** Attachment upload; QA checklist UI already has `/api/repairs/{id}/qa`; time entry endpoints.
- **D:** Label render (ZPL/PDF), printer profiles, batch print.
- **E:** Donor/harvest APIs; align used-tech status transitions with permissions.
- **F:** Loaner checkout; warranty claim workflow; refund approval.
- **Later:** Tenant-scoped routes, licence validation, public API keys/scopes.

---

## 7. Proposed frontend changes (by phase)

- Real pages only when API is ready (Section 1.12–1.13).
- Prefer dedicated pages over `GenericListPage` for inventory depth, POs, invoices, warranties, donors.
- Label centre: templates + preview + reprint.
- Repair detail: photos, QC, timer.
- Used Tech: full pipeline + IMEI (already) + grading + costs.
- Settings: printers, backup DR centre, integration health.

---

## 8. Development dependencies

| Dependency | Needed for |
| --- | --- |
| Stable `Encryption__Key` + JWT | All credentialed integrations |
| Real Postgres backups | DR phase |
| Brother/Zebra test printers (or PDF-only first) | Labels |
| Square sandbox (optional) | Live POS later — Manual/EFTPOS reference first |
| WordPress/10Web staging | Website staff surfaces |
| Existing xUnit factory + Postgres | Regression gate every phase |

---

## 9. Implementation plan (controlled phases)

Do **not** run these in parallel as one mega-PR. Each phase ends with: tests green · checklist update · version bump · sync artifact.

| Phase | Name | Spec §§ primarily | Complexity | Exit criteria |
| --- | --- | --- | --- | --- |
| **A** | Ops honesty & foundation | 17, 22, 24, 25, docs | **M** | Real backups path; COGS never invented; audit list; docs corrected |
| **B** | Inventory & purchasing depth | 3, 4 | **L** | Adjust/reserve/stocktake UI; PO receive UI; available qty = on-hand − reserved − quarantined |
| **C** | Repair intake & QC | 7, 8, 11 (partial), 12 | **L** | Photos + condition report; QA UI; repeat-repair banner on intake |
| **D** | Label printing | 2 (+ battery health fields) | **L** | Templates for repair / refurbished / parts; PDF fallback; internal ID barcode; preview/reprint |
| **E** | Refurbished, donor, harvesting | 5, 6 | **XL** | Acquisitions WinUI; full status pipeline; donor → component → inventory traceability |
| **F** | POS, refunds, loaners, warranty | 15, 16, 19 | **XL** | Refund approval; deposit clarity; loan register; warranty claims |
| **G** | Website staff, appointments polish, comms | 13, 14, 18, 20 (knowledge) | **L** | Sell-tech/messages/publish UI; booking capacity rules polished |
| **H** | Analytics, compatibility, offline, multi-site | 10, 17, 21, 23, 26 | **L–XL** | Fitment + offline + sites shipped 1.2.40; §9 board-level won’t do; SaaS tenants deferred |

**Immediate next implementation (when authorised):** **Phase A**, then **Phase B** or **Phase D** (labels) if shop-floor printing is the urgent business need — labels should still not invent battery health (manual / diagnostic source only).

---

## 10. Development complexity (overall)

| Area | Rating |
| --- | --- |
| Completing V2 as written | **XL / multi-year product** for a solo/agent cadence |
| Phase A | M |
| Phase B | L |
| Phase C | L |
| Phase D (labels) | L (PDF first) → XL (native Brother/Zebra + batch) |
| Phase E | XL |
| Phase F | XL |
| Phase G | L |
| Phase H (fitment / offline / sites) | L — shipped 1.2.40 |
| Phase H (SaaS multi-tenant / licensing) | XL — architectural; design review mandatory; board-level won’t do |

---

## 11. Feature checklist (living)

Update status only when code is implemented **and** tested.

| ID | Item | Status |
| --- | --- | --- |
| A1 | Real `pg_dump` backups + verify | Implemented (1.2.38) |
| A2 | Restore-test procedure / isolated drill | Implemented — hash + file verify drill (1.2.39); full DB restore into isolated instance still later |
| A3 | Fix report COGS invention | Implemented (1.2.38) |
| A4 | Audit event list API + Settings UI | Implemented (1.2.38) |
| A5 | Doc refresh (DATABASE, FEATURES SignalR, PLATFORM POS) | Partial — plan + changelog; full doc sweep later |
| B1 | Inventory adjust/reserve/consume WinUI | Partial — adjust + quarantine/bin UI; reserve still needs ticket/build |
| B2 | Location/bin hierarchy + transfers | Partial — bin transfer API + WinUI (1.2.39); multi-level hierarchy later |
| B3 | Stocktake sessions | Implemented (1.2.38) |
| B4 | PO receive WinUI | Implemented (1.2.38) |
| B5 | Barcode scan receive/deduct path | Implemented — barcode lookup API + inventory UI (1.2.39) |
| C1 | Repair photo attachments | Implemented (1.2.39) |
| C2 | Condition report PDF + customer copy | Implemented — HTML print/PDF (1.2.39) |
| C3 | Digital intake signature | Implemented (1.2.39) |
| C4 | Repair QA checklist WinUI | Implemented (1.2.38) |
| C5 | Repeat-repair history banner | Implemented (1.2.38) |
| D1 | Label templates (repair / used / parts) | Implemented — HTML (1.2.38) |
| D2 | PDF fallback + preview | Partial — HTML print/PDF via browser |
| D3 | Printer profiles (network/USB where supported) | Implemented — profiles + PDF fallback (1.2.39); native Brother/Zebra later |
| D4 | Battery health fields (manual + source; never invent) | Implemented (1.2.38) |
| D5 | Batch / reprint | Implemented — batch HTML render (1.2.39) |
| E1 | Acquisitions WinUI | Implemented (1.2.38) |
| E2 | Used-tech full status pipeline UI | Implemented (1.2.38) |
| E3 | Donor devices | Implemented (1.2.39) |
| E4 | Harvest → inventory with donor trace | Implemented (1.2.39) |
| F1 | Refund approval workflow | Implemented (1.2.39) |
| F2 | Loan device register | Implemented (1.2.39) |
| F3 | Warranty claims staff UI | Implemented (1.2.39) |
| F4 | POS sell refurbished | Implemented (1.2.39) |
| G1 | Website staff inbox (sell-tech / messages) | Implemented (1.2.39) |
| H1 | Integration health events | Implemented (1.2.39) |
| H2 | Parts fitment / compatibility database | Implemented (1.2.40) |
| H3 | Offline sync outbox + client queue | Implemented (1.2.40) |
| H4 | Multi-site Locations + inventory bins | Implemented (1.2.40) — multi-site, not SaaS multi-tenant |
| H5 | Board-level repair | **Won’t do** |
| H6 | SaaS multi-tenant / licensing | Not started — deferred XL |

---

## 12. After each phase — report template

1. Changes made  
2. Files modified  
3. Database changes  
4. API changes  
5. Tests performed + results  
6. Known limitations  
7. Remaining work  

Do not mark checklist items **Implemented** or **Tested** without evidence.
