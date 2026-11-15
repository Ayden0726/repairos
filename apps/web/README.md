# TechBench NQ — Repair management (web) · 1.2.42

Browser-based repair intake and ticket tracking for TechBench NQ (AUD, Australia/Brisbane).

## Milestone 1 scope (home workshop)

- Owner sign-in with **PIN** or **password**
- Customers (create + search)
- Devices (optional IMEI / serial)
- Repair intake (fault, condition, accessories, labour fee, estimate, photos)
- Ticket statuses with history and notes
- Searchable ticket list and detail pages

Later milestones (not in this build): invoicing, inventory, messaging, staff roles, backups, Square.

## Local development

Requirements: Node 22+, PostgreSQL 16+.

```bash
cp .env.example .env
# Set DATABASE_URL, SESSION_SECRET (32+ chars),
# SEED_OWNER_EMAIL, SEED_OWNER_PASSWORD, optional SEED_OWNER_PIN

npm install --legacy-peer-deps
npx prisma migrate deploy
npm run db:seed
npm run dev
```

Open http://127.0.0.1:3847

Owner credentials are created only when no owner exists, from your `.env` — they are never shipped in the UI.

## Docker Compose

```bash
export SESSION_SECRET="$(openssl rand -hex 24)"
export POSTGRES_PASSWORD="$(openssl rand -hex 16)"
export SEED_OWNER_EMAIL="you@example.com"
export SEED_OWNER_PASSWORD="$(openssl rand -base64 18)"
export SEED_OWNER_PIN="2468"   # optional, 4–8 digits
docker compose up --build
```

## Tests

```bash
npm test
# With the app running (credentials from your env):
set -a && source .env && set +a
TECHBENCH_BASE_URL=http://127.0.0.1:3847 npm test
```

## Money

All amounts are integer cents (`BigInt` / `BIGINT`). Quotes, estimates and payments stay distinct — M1 stores labour fee + optional estimate only.
