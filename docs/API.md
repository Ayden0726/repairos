# API Design (Phase 1)

Base URL example: `https://workshop.local/api` (dev: `http://127.0.0.1:5088/api`)

OpenAPI: `/swagger` (Development only by default)

## Auth

| Method | Path | Auth | Description |
| --- | --- | --- | --- |
| GET | `/api/setup/status` | Anonymous | Whether first-run is complete |
| POST | `/api/setup` | Anonymous (only if incomplete) | Create business + owner |
| POST | `/api/auth/login` | Anonymous | Email/password → access + refresh |
| POST | `/api/auth/refresh` | Anonymous | Rotate refresh token |
| POST | `/api/auth/logout` | Bearer | Revoke refresh token |
| GET | `/api/auth/me` | Bearer | Current user, roles, permissions |
| PUT | `/api/auth/password` | Bearer | Change own password (current + new) |

## Staff

| Method | Path | Auth | Description |
| --- | --- | --- | --- |
| GET | `/api/users` | `staff.view` | List staff |
| POST | `/api/users` | `staff.manage` | Create staff |
| PUT | `/api/users/{id}` | `staff.manage` | Update role/status |
| PUT | `/api/users/{id}/password` | `staff.manage` | Admin reset staff password |
## Settings & health

| Method | Path | Auth | Description |
| --- | --- | --- | --- |
| GET | `/api/health` | Anonymous | API + DB readiness |
| GET | `/api/settings/business` | `settings.view` | Business profile |
| PUT | `/api/settings/business` | `settings.manage` | Update profile |
| GET | `/api/settings/modules` | Authenticated | Visible sidebar modules |
| GET | `/api/permissions` | `roles.manage` | Permission catalogue |
| GET | `/api/roles` | `roles.manage` | Roles with permissions |

## Search (shell)

| Method | Path | Auth | Description |
| --- | --- | --- | --- |
| GET | `/api/search?q=` | Authenticated | Grouped results (empty groups until Phase 2+) |

## Service catalogue

| Method | Path | Auth | Description |
| --- | --- | --- | --- |
| GET | `/api/catalogue/categories` | `pricing.view` | Category tree |
| GET | `/api/catalogue/services?q=&category=&deviceType=&brand=&model=&activeOnly=` | `pricing.view` | Fuzzy search |
| POST/DELETE | `/api/catalogue/services[/{id}]` | `pricing.edit_settings` | Upsert / disable |
| POST | `/api/catalogue/import?force=` | `pricing.edit_settings` | Idempotent JSON seed |
| GET/POST/DELETE | `/api/catalogue/favourites[/{id}]` | `pricing.view` | Per-user favourites |
| GET/POST | `/api/catalogue/recent[/{id}]` | `pricing.view` | Recent selections |
| GET/POST/DELETE | `/api/catalogue/bundles[/{id}]` | view / edit_settings | Bundles + expand |
| GET | `/api/catalogue/brands` · `/api/catalogue/models` | `pricing.view` | Device filters |
| POST | `/api/repairs` (+ `servicePricingIds`) | `tickets.create` | Attach service lines |
| POST | `/api/repairs/{id}/service-lines/{lineId}/complete` | `tickets.status` | Per-service complete |

Seed gated by setting `seed.service_catalogue_version` (embedded `service-catalogue.v1.json`).

## Conventions

- JSON camelCase
- ProblemDetails for errors (`application/problem+json`)
- Correlation id header `X-Correlation-Id`
- All mutating endpoints audited when security-relevant
- Pagination: `?page=&pageSize=` for list endpoints (later phases)

## SignalR (Phase 1 stub)

Hub: `/hubs/workshop` — connection accepted for authenticated users. Domain events (ticket status, stock, payments) arrive in Phase 12 live-update work. Phase 1 verifies connect/auth only.
