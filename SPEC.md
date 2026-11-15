# TechBench NQ — Product Specification

**Business:** TechBench NQ — small Australian phone, tablet and computer repair shop (Townsville / NQ).  
**Product:** Original self-hosted repair management application (not RepairTree; not a rebrand of WorkshopOS chrome).  
**Locale:** AUD · `Australia/Brisbane` · configurable GST (shop may or may not be registered).  
**Primary clients:** Windows desktop browsers and tablets.

---

## 1. What already exists in this repository

| Area | State | Relevance to TechBench NQ |
| --- | --- | --- |
| `services/*` WorkshopOS .NET 8 API + worker | Large modular monolith: JWT auth, customers, devices, repairs, quotes, inventory, POS, website, fitment, etc. | Useful reference for domain ideas; **not** the TechBench NQ UI surface. Branding and scope exceed this brief. |
| `apps/windows-client` | WinUI 3 desktop client | Out of scope for “browser-based” first delivery. |
| `apps/web` | Next.js lean **mock** UI (localStorage login, fictional in-memory data) | Starting point for browser UX; must become a real app with server persistence. |
| `docker/` | Compose for WorkshopOS API + Postgres + worker | Pattern to reuse; TechBench M1 ships its own lean Compose for web + Postgres. |
| `docs/*`, WorkshopOS specs | Historical WorkshopOS planning | Superseded for this product by this file + `DATA_MODEL.md` + `IMPLEMENTATION_PLAN.md`. |
| Mockup assets (`techbench-nq-client-mockup-*.jpg`) | Visual tone: dark charcoal + teal, ticket ids `TB-YYYY-######`, command search, repairs list | Functional/layout reference only — **do not** copy proprietary competitor text or assets. |

**Decision for delivery:** Implement TechBench NQ as a **browser modular monolith** (Next.js App Router + PostgreSQL). Keep WorkshopOS code in-tree for later optional reuse; do not require the WinUI client for milestone 1.

**Auth (1.2.42):** Owner may sign in with a **4–8 digit PIN** (bench tablet) or **email + password** (desktop). Credentials are seeded from environment variables only; never embedded in the login UI.

---

## 2. Goals

- Run day-to-day repair intake and ticket tracking on a self-hosted stack.
- Stay simple enough for a small owner-operator shop.
- Grow later into invoicing, inventory, messaging, staff roles, backups and Square — without rewriting core tickets.

---

## 3. Non-goals (this product phase)

- Square / card readers, POS register, discounts engine.
- Inventory, purchasing, refurbished stock.
- Customer SMS/email messaging automation.
- Full multi-staff RBAC beyond owner login (roles come later).
- Public customer portal, WordPress sync, AI features.
- Windows desktop packaging.

---

## 4. Milestone 1 — in scope

1. **Owner login** (server-authenticated session).
2. **Customers** — create and search.
3. **Devices** — linked to a customer; optional IMEI and/or serial.
4. **Repair intake** — reported fault, condition, accessories received, estimate, optional photos.
5. **Ticket statuses:** Received → Diagnosing → Awaiting Approval → Awaiting Parts → In Progress → Ready for Collection → Collected; plus Cancelled.
6. **Timestamped notes** and **status history** on each ticket.
7. **Searchable ticket list** and **ticket detail page**.

### Business rules (M1)

| Rule | Behaviour |
| --- | --- |
| Default labour / service fee | AUD **$50.00**, editable per job |
| Lower-priced tasks | Allowed (estimate and labour may be below $50) |
| Parts markup | Field reserved for later; M1 stores estimate only — no inventory consumption |
| Quotes vs estimate vs payments | **Estimate** on the ticket only in M1; quotes and payments are later milestones and must stay distinct concepts |
| Passcodes | **Not required** at intake; no passcode capture in M1 intake UI |
| Currency | Stored as **integer cents** (never floating-point) |
| Sample data | Fictional AU names/devices only |

---

## 5. Functional detail (M1)

### 5.1 Authentication

- Email + password for the owner account.
- Session cookie (HTTP-only, Secure in production).
- Server rejects unauthenticated API/page access to shop data.
- Seeded owner in development; production: created via first-run or env bootstrap (see assumptions).

### 5.2 Customers

- Fields: first name, last name, phone, email (optional), notes (optional).
- Search by name, phone, email.
- List + create + open detail (devices and tickets for that customer).

### 5.3 Devices

- Belong to one customer.
- Category: Phone | Tablet | Computer | Other.
- Brand, model, optional colour/storage, optional IMEI, optional serial.
- Searchable via ticket/customer flows; standalone device list optional in M1.

### 5.4 Repair tickets

- Ticket number format: `TB-{yyyy}-{######}` (Brisbane year).
- Intake captures: customer, device (existing or inline create), reported fault, condition checklist + free text, accessories received, labour fee (default 5000 cents), estimate (cents, optional), optional photos.
- Status changes append immutable history rows (who, when, from → to, optional comment).
- Notes: timestamped, author = logged-in user, body text (internal for M1).

### 5.5 Photos

- Optional on intake and ticket page.
- Server validates type (JPEG/PNG/WebP) and size.
- Stored outside git on server volume; served only to authenticated users.

### 5.6 Search

- Ticket list filters: query (ticket #, customer name, phone, IMEI/serial, fault text), status.
- Sorted by newest updated first (default).

---

## 6. Non-functional

- Docker Compose: `web` + `postgres`.
- Versioned DB migrations (Prisma).
- Secrets via environment / `.env` (not committed).
- Timezone display: Australia/Brisbane; store instants in UTC.
- GST setting stored for the business (registered flag + rate) but **not** applied to invoices yet (no invoicing in M1).
- Permissions enforced on the server (owner-only is enough for M1).

---

## 7. Assumptions (reversible)

See `IMPLEMENTATION_PLAN.md` § Assumptions. Highlights:

- Single-shop, single location for M1.
- One owner user is enough; additional staff roles deferred.
- “Condition” = structured checklist (powers on, cracks, scratches, liquid indicators) + optional notes.
- “Accessories received” = charger, SIM, case, other (free text).
- Estimate is a single AUD cents amount on the ticket (not a full quote document).
- Photo max 5 per ticket in M1, 5 MB each.

---

## 8. Out of scope until later milestones

Invoicing · inventory · messaging · customer tracking automations · multi-role staff · backups UI · Square · public status page · quotes module as distinct documents.

---

## 9. Success criteria (M1)

- Owner can sign in and sign out.
- Owner can create/search customers and attach devices.
- Owner can intake a repair with estimate and photos, change status, add notes, and see history.
- Ticket list search finds by ticket number and customer phone.
- Automated tests cover auth gate, customer create/search, ticket intake + status transition + note.
- No feature is described as “working” without a verified test or manual check recorded in the milestone report.
