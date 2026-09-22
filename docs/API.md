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

## Conventions

- JSON camelCase
- ProblemDetails for errors (`application/problem+json`)
- Correlation id header `X-Correlation-Id`
- All mutating endpoints audited when security-relevant
- Pagination: `?page=&pageSize=` for list endpoints (later phases)

## SignalR (Phase 1 stub)

Hub: `/hubs/workshop` — connection accepted for authenticated users. Domain events (ticket status, stock, payments) arrive in Phase 12 live-update work. Phase 1 verifies connect/auth only.
