# TechBench NQ — Data Model (Milestone 1)

PostgreSQL 16. Money columns are **integer cents** (`BIGINT`). Timestamps are `TIMESTAMPTZ` (UTC). Soft-delete where business records must remain recoverable.

---

## Entity relationship (M1)

```
User ──< Session (optional / cookie store)
User ──< TicketNote
User ──< TicketStatusHistory
User ──< Ticket (created_by)

Customer ──< Device ──< Ticket
Customer ──< Ticket
Ticket ──< TicketNote
Ticket ──< TicketStatusHistory
Ticket ──< TicketPhoto

BusinessSettings (singleton row / key-value)
```

---

## Tables

### `users`

| Column | Type | Notes |
| --- | --- | --- |
| id | UUID PK | |
| email | CITEXT UNIQUE | Login |
| password_hash | TEXT | bcrypt |
| pin_hash | TEXT NULL | bcrypt of 4–8 digit PIN (1.2.42) |
| display_name | TEXT | |
| role | TEXT | M1: `owner` only |
| created_at | TIMESTAMPTZ | |
| updated_at | TIMESTAMPTZ | |

### `customers`

| Column | Type | Notes |
| --- | --- | --- |
| id | UUID PK | |
| first_name | TEXT | Required |
| last_name | TEXT | Optional |
| phone | TEXT | Indexed; search |
| email | TEXT | Optional |
| notes | TEXT | Optional |
| created_at | TIMESTAMPTZ | |
| updated_at | TIMESTAMPTZ | |
| archived_at | TIMESTAMPTZ NULL | Soft delete |

### `devices`

| Column | Type | Notes |
| --- | --- | --- |
| id | UUID PK | |
| customer_id | UUID FK → customers | |
| category | TEXT | `phone` \| `tablet` \| `computer` \| `other` |
| brand | TEXT | |
| model | TEXT | |
| colour | TEXT NULL | |
| storage | TEXT NULL | e.g. 128GB |
| imei | TEXT NULL | Indexed |
| serial | TEXT NULL | Indexed |
| notes | TEXT NULL | |
| created_at | TIMESTAMPTZ | |
| updated_at | TIMESTAMPTZ | |
| archived_at | TIMESTAMPTZ NULL | |

### `tickets`

| Column | Type | Notes |
| --- | --- | --- |
| id | UUID PK | |
| ticket_number | TEXT UNIQUE | `TB-2026-000001` |
| customer_id | UUID FK | |
| device_id | UUID FK NULL | Set after device create/link |
| status | TEXT | See enum below |
| reported_fault | TEXT | Required |
| condition_powers_on | BOOLEAN NULL | |
| condition_cracks | BOOLEAN NULL | |
| condition_scratches | BOOLEAN NULL | |
| condition_liquid | BOOLEAN NULL | |
| condition_notes | TEXT NULL | |
| accessory_charger | BOOLEAN | Default false |
| accessory_sim | BOOLEAN | |
| accessory_case | BOOLEAN | |
| accessory_other | TEXT NULL | |
| labour_fee_cents | BIGINT | Default **5000** ($50) |
| estimate_cents | BIGINT NULL | Distinct from quotes/payments (later) |
| created_by_id | UUID FK → users | |
| created_at | TIMESTAMPTZ | |
| updated_at | TIMESTAMPTZ | |
| archived_at | TIMESTAMPTZ NULL | |

**Status enum (exact):**  
`received` · `diagnosing` · `awaiting_approval` · `awaiting_parts` · `in_progress` · `ready_for_collection` · `collected` · `cancelled`

### `ticket_notes`

| Column | Type | Notes |
| --- | --- | --- |
| id | UUID PK | |
| ticket_id | UUID FK | |
| author_id | UUID FK → users | |
| body | TEXT | |
| created_at | TIMESTAMPTZ | Immutable |

### `ticket_status_history`

| Column | Type | Notes |
| --- | --- | --- |
| id | UUID PK | |
| ticket_id | UUID FK | |
| actor_id | UUID FK NULL | |
| from_status | TEXT NULL | Null on create |
| to_status | TEXT | |
| comment | TEXT NULL | |
| created_at | TIMESTAMPTZ | Immutable |

### `ticket_photos`

| Column | Type | Notes |
| --- | --- | --- |
| id | UUID PK | |
| ticket_id | UUID FK | |
| stored_name | TEXT | Filesystem name under upload root |
| original_name | TEXT | Sanitised display name |
| content_type | TEXT | image/jpeg \| png \| webp |
| size_bytes | INT | |
| uploaded_by_id | UUID FK | |
| created_at | TIMESTAMPTZ | |

### `business_settings`

| Column | Type | Notes |
| --- | --- | --- |
| id | INT PK | Singleton `1` |
| business_name | TEXT | Default TechBench NQ |
| timezone | TEXT | `Australia/Brisbane` |
| currency | TEXT | `AUD` |
| gst_registered | BOOLEAN | Default **false** (do not assume registered) |
| gst_rate_bp | INT | Basis points; 1000 = 10.00% when registered |
| default_labour_fee_cents | BIGINT | 5000 |
| ticket_seq_year | INT | Brisbane year for numbering |
| ticket_seq_next | INT | Next sequence value |

### `document_sequences` (optional alternative)

If preferred over columns on `business_settings`, a `document_sequences(key, year, next_value)` table may own ticket numbering. M1 may use either; migration locks the choice.

---

## Indexes

- `customers(phone)`, `customers(lower(first_name), lower(last_name))`
- `devices(imei)`, `devices(serial)`, `devices(customer_id)`
- `tickets(ticket_number)`, `tickets(status)`, `tickets(updated_at DESC)`, `tickets(customer_id)`
- Full-text or `ILIKE` on `reported_fault` acceptable for M1 volume

---

## Explicitly not in M1 schema

Invoices, payments, quote documents, inventory, stock movements, message logs, Square credentials, backup jobs, multi-location, role permissions matrix.

---

## Mapping note vs WorkshopOS

WorkshopOS already has richer `Customer` / `Device` / `RepairTicket` / `RepairNote` / `RepairEvent` entities (decimal money, passcode fields, many statuses). TechBench NQ M1 uses this **narrower** model with integer cents and the status set named in `SPEC.md`. Do not reuse WorkshopOS decimal money columns for new TechBench tables.
