# Authentication Architecture

## Model

ASP.NET Core Identity stores users and password hashes. WorkshopOS adds:

- Custom `AppUser` / `AppRole`
- Permission claims derived from role → permission catalogue
- JWT access tokens (short-lived, default 15 minutes)
- Refresh tokens (default 12 days), stored as SHA-256 hashes, rotated on use

## First-run

1. Client always opens **ServerConnect** on launch (pairing code, LAN discovery, or URL). No auto-bootstrap splash.
2. After connect, client calls `GET /api/setup/status`
3. If incomplete, shows **SetupPage** — the shop first-run wizard (business profile + owner account)
4. `POST /api/setup` creates Location, BusinessSetting, Owner user with all permissions, marks `setup.completed`
5. Client proceeds to login

Setup cannot run twice. Staff users are created later in the app (**Settings → Users & Roles**), not by a second install wizard.

Client connection settings path (Windows): `%LOCALAPPDATA%\WorkshopOS\client-settings.json`  
Theme preference (`System` / `Light` / `Dark`) is stored in the same file and survives **Change server**.

**Client 1.2.7+** always shows the connect UI on launch. **Change server** / **Clear saved server** wipe connection tokens/URL (JSON, WinRT `LocalSettings`, PasswordVault) but keep the theme.

## Login flow

1. Client POSTs email + password
2. Failed attempts audited + rate limited
3. Success returns `{ accessToken, refreshToken, expiresAt, user }`
4. Client stores tokens in `%LOCALAPPDATA%\WorkshopOS\client-settings.json` (with the server URL)
5. Server URL is cleared via **Clear saved server** / deleting that file

## Authorization

- Controllers use `[Authorize]` plus permission policies (`RequirePermission("customers.view")`)
- Owner flag bypasses permission checks (same as “superuser”)
- Disabled / suspended users cannot obtain tokens

## Password rules (Phase 1)

Minimum 10 characters, at least one upper, one lower, one digit.

### In-app reset / change (no email magic-link)

- **Owner/Admin** with `staff.manage`: **Settings → Users & Roles** → select a staff user → **Reset password…** (sets a new password; revokes that user’s refresh tokens).
- **Any signed-in user**: **Settings → App** → **Change password…** (current password + new; revokes other sessions for that user).
- API:
  - `PUT /api/users/{id}/password` — body `{ "newPassword": "..." }` — requires `staff.manage` (or owner)
  - `PUT /api/auth/password` — body `{ "currentPassword": "...", "newPassword": "..." }` — authenticated self-service

### Emergency owner reset (server access)

If the owner is locked out and no admin can reset from the client:

```bash
cd ~/workshopos
./scripts/reset-owner-password.sh 'YourNewPassword1'
```

Or set env once and recreate the API container:

```bash
export WORKSHOPOS_OWNER_PASSWORD_RESET='YourNewPassword1'
docker compose -f docker/docker-compose.yml up -d --force-recreate api
unset WORKSHOPOS_OWNER_PASSWORD_RESET
docker compose -f docker/docker-compose.yml up -d --force-recreate api
```

Email reset-by-link remains a later phase.