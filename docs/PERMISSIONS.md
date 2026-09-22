# Permission Structure

Permissions are string keys, grouped for UI. Roles are sets of permissions. Custom roles can be added later (`roles.manage`).

## System roles (seeded)

| Role | Key | Intent |
| --- | --- | --- |
| Owner | `owner` | Founding account; all permissions; cannot be narrowed |
| Administrator | `administrator` | Full admin |
| Manager | `manager` | Operations; no role / privacy / update control |
| Technician | `technician` | Workshop jobs |
| Front Desk | `front_desk` | Intake, customers, payments |
| Sales | `sales` | Quotes, used tech, limited customers |
| Read Only | `read_only` | View-only operational data |

## Catalogue (Phase 1 subset + reserved keys)

Phase 1 enforces the keys that exist in code. Additional keys are reserved for later modules so the client can show/hide UI consistently.

### Staff & admin

- `staff.view` / `staff.manage`
- `roles.manage`
- `settings.view` / `settings.manage`
- `audit.view`
- `backups.manage`
- `integrations.manage`

### Reserved for later phases (seeded, unused until modules land)

Tickets, customers, devices, inventory, quotes, invoices, payments, pricing, reports, builds, used, bookings, knowledge, AI, incidents, privacy — same key style as documented in the product spec (`tickets.view`, `customers.manage`, …).

## Client behaviour

Navigation items check permissions. Missing permission → hide or disable. Server always re-checks; UI hiding is not security.
