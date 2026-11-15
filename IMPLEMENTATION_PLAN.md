# TechBench NQ — Implementation Plan

## Assumptions (reversible)

| # | Assumption | Why reversible |
| --- | --- | --- |
| A1 | Ship M1 as Next.js App Router + Prisma + Postgres in `apps/web`, Docker Compose `web`+`db` | Can later add a separate API process without changing tables |
| A2 | Single owner account for M1; role string `owner` | Add staff/roles tables later |
| A3 | Session via iron-session or sealed cookie (no Redis) | Swap to Redis-backed sessions later |
| A4 | Ticket photos on local disk volume `/data/uploads` | Move to object storage later |
| A5 | Condition = four nullable booleans + notes | Can expand checklist without breaking tickets |
| A6 | Accessories = charger/SIM/case booleans + other text | Same |
| A7 | Default labour 5000 cents from `business_settings` | Editable per ticket and in settings later |
| A8 | GST stored but unused in M1 UI calculations | Invoicing milestone consumes it |
| A9 | Dev seed: fictional owner + sample customers/devices/tickets | `SEED_DEMO=true` |
| A10 | WorkshopOS .NET stack remains in repo but is **not** required to run TechBench M1 | Optional bridge later |
| A11 | No device passcode fields in M1 intake | Can add encrypted optional field later |
| A12 | Ticket search uses SQL `ILIKE` | Upgrade to trigram/FTS if needed |

**Missing requirements resolved by assumption:** precise photo limits (A4/A5 → max 5 × 5MB); whether estimate includes labour (estimate is separate optional field; labour_fee_cents always present); whether “Collected” is terminal (yes, alongside Cancelled).

---

## Milestones

### M1 — Core intake & tickets (this delivery)

Owner auth · customers · devices · repair intake · statuses · notes · history · search · photos · Docker · migrations · tests.

### M2 — Money documents (later)

Quotes as distinct documents · invoices · payments · GST on documents · keep estimate on ticket separate.

### M3 — Inventory & parts markup (later)

Stock · consume on job · markup rules · low-stock alerts.

### M4 — People & ops (later)

Staff roles/permissions · messaging · customer tracking · backups · Square Readers.

---

## M1 technical slices

1. **Docs** — `SPEC.md`, `DATA_MODEL.md`, this plan.  
2. **Platform** — Prisma schema, migrations, Docker Compose, env example, upload volume.  
3. **Auth** — password hash, login/logout, server guards.  
4. **Customers & devices** — CRUD/search APIs + UI.  
5. **Tickets** — intake, list/search, detail, status change + history, notes, photos.  
6. **Seed** — fictional AU demo data.  
7. **Verify** — automated tests + manual smoke; report.

---

## Quality gates

- Server-side permission checks on every mutating route.
- Zod (or equivalent) validation on inputs.
- Upload MIME sniff + extension allow-list; no executable types.
- No `number`/`float`/`decimal` for money in DB — integer cents only.
- Do not ship fake Square/SMS buttons that look live.
- Do not mark M1 complete until listed tests pass.

---

## M1 verification checklist

- [x] Login rejects bad password; session required for `/tickets`.
- [x] Create customer; search by phone finds them.
- [x] Create device with IMEI; visible on customer.
- [x] Intake ticket with fault, condition, accessories, labour ≠ default, estimate, photo.
- [x] Status transition writes history row.
- [x] Note appears with timestamp and author.
- [x] Ticket list search by ticket number and customer name.
- [x] Unauthenticated photo URL returns 401/redirect.

Verified 2026-11-14 via `npm test` with `TECHBENCH_BASE_URL=http://127.0.0.1:3847` (9/9 passing).
