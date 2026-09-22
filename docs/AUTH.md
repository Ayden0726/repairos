# Authentication Architecture

## Model

ASP.NET Core Identity stores users and password hashes. WorkshopOS adds:

- Custom `AppUser` / `AppRole`
- Permission claims derived from role → permission catalogue
- JWT access tokens (short-lived, default 15 minutes)
- Refresh tokens (default 12 days), stored as SHA-256 hashes, rotated on use

## First-run

1. Client calls `GET /api/setup/status`
2. If incomplete, shows setup wizard (business + owner)
3. `POST /api/setup` creates Location, BusinessSetting, Owner user with all permissions, marks `setup.completed`
4. Client proceeds to login

Setup cannot run twice.

## Login flow

1. Client POSTs email + password
2. Failed attempts audited + rate limited
3. Success returns `{ accessToken, refreshToken, expiresAt, user }`
4. Client stores tokens in Windows Credential Manager / protected local storage (DPAPI)
5. Server URL stored separately in local app settings

## Authorization

- Controllers use `[Authorize]` plus permission policies (`RequirePermission("customers.view")`)
- Owner flag bypasses permission checks (same as “superuser”)
- Disabled / suspended users cannot obtain tokens

## Password rules (Phase 1)

Minimum 10 characters, at least one upper, one lower, one digit. Reset-by-email lands in a later phase; Phase 1 supports admin disable only.
