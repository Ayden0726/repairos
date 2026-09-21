# WorkshopOS

Self-hosted operations software for a single technology repair shop. Tickets sit at the centre: intake, diagnosis, parts, time, invoices, pickup, warranty and reporting all share the same customers, inventory, audit log and permissions.

The stack is Next.js, TypeScript, Prisma, PostgreSQL, Redis and Caddy. Nothing in the core workflow requires a cloud vendor. SMS, email, Square and Ollama are optional integrations.

## What staff use every day

- **New Job** — quick intake in under a minute, plus full intake when needed
- **Tickets** — Kanban and table views, SLA warnings, device labels, encrypted PINs
- **Customers** — individuals and businesses, devices, quotes, invoices, messages
- **Inventory** — reservations, serials, purchase orders, stocktake, PC components
- **Custom PC builds** — parts, advisory compatibility, deposits and handover notes
- **Used tech** — buy-in valuation, refurbishment tickets, ready-for-sale stock
- **Calendar & roster** — drop-offs, consultations, shifts, due dates, supplier deliveries
- **Finance** — Australian GST, invoices, deposits, refunds, Square as an optional provider
- **Settings** — searchable admin centre for staff accounts, roles, statuses, job types, integrations, backups and health

## Local development

You need Node 22, PostgreSQL 16 and Redis 7.

```bash
cp .env.example .env
# point DATABASE_URL at local Postgres, generate SESSION_SECRET and ENCRYPTION_KEY
npm install
npx prisma migrate deploy
npx tsx prisma/seed.ts
npm run dev
```

The app listens on **http://127.0.0.1:47821**.

### Functional preview from the setup wizard

The seeded demo skips setup and signs you in as Riverside Tech. To walk the real first-run path, start from an **empty** database and **do not** run the seed.

```bash
cp .env.example .env
# set DATABASE_URL, REDIS_URL, SESSION_SECRET and ENCRYPTION_KEY
# leave SEED_DEMO=false
npm install
npx prisma migrate reset --force --skip-seed
npm run dev
```

Open **http://127.0.0.1:47821**. You should land on `/setup`, not login.

1. Business name (required). Phone, email, ABN and address are optional.
2. Keep GST ticked if you are registered. Labour defaults to `$110/hr` and diagnostic fee to `$89`.
3. Owner name, email and password. Password must be **at least 10 characters** with upper, lower and a number (for example `Workshop!2026`).
4. **Complete setup**. You are sent to login.
5. Sign in with **that** owner email and password (not the Maya demo user).
6. The shop is empty. Add a customer, click **New Job**, assign it to yourself, then run the job as the technician.

If you already seeded the demo, `/setup` redirects to login. Reset with `npx prisma migrate reset --force --skip-seed` (or `npm run db:fresh`) and start `npm run dev` again.

Docker is the same idea: keep `SEED_DEMO=false` in `.env`, run `docker compose up -d --build`, then `docker compose exec app npx prisma migrate deploy`. Do not run `prisma/seed.ts`. Open the site and complete `/setup`.

Demo staff (password `Riverside!2026`):

| Role | Email |
| --- | --- |
| Owner | maya@riversidetech.com.au |
| Manager | tom@riversidetech.com.au |
| Technician | priya@riversidetech.com.au |
| Technician | jack@riversidetech.com.au |
| Front desk | sophie@riversidetech.com.au |

A fresh database without `SEED_DEMO` opens the first-run wizard and blocks normal use until an owner account exists.

### Solo owner who is also the technician

Use the owner account created in setup as your everyday login. Owner has every permission, so you can intake jobs, assign them to yourself, run timers and diagnostics, invoice and take payment from one account. You do not need a second “technician” login for yourself.

When someone else joins, open **Settings → Users → Add staff account**, pick a role, set a temporary password and give it to them in person. There is no invite email.

| Role | What they can do |
| --- | --- |
| Owner | Created once at setup. Full shop, including staff and settings. |
| Admin | Second full-access login if you want a backup. |
| Manager | Operations without role, update or privacy changes. |
| Technician | Workshop work only. No pricing, refunds or settings. |
| Front desk | Intake, customers, bookings and taking payment. |

```bash
npm run typecheck
npm test
npm run lint
```

## Production install (Linux + Docker)

1. Copy this repository to the server.
2. Copy `.env.example` to `.env` and set strong `SESSION_SECRET`, `ENCRYPTION_KEY` and `POSTGRES_PASSWORD` values.
3. Set `DOMAIN` to the shop hostname.
4. Put the host behind HTTPS. Caddy terminates TLS in `docker-compose.yml`.
5. Start the stack:

```bash
docker compose up -d --build
docker compose exec app npx prisma migrate deploy
```

Do **not** publish port 3000 / 47821 straight to the public internet. Use Caddy (HTTPS) or a private network such as Tailscale / WireGuard. The application still requires passwords and optional 2FA.

### Updates

1. `docker compose exec app npm run backup` or Settings → Backup now
2. `git pull`
3. `docker compose up -d --build`
4. `docker compose exec app npx prisma migrate deploy`
5. Open Settings → System health

Roll back by checking out the previous image tag and restoring the safety backup if a migration cannot be reversed.

### Backups and restore

- Automatic destination: `BACKUP_DIR` (local disk or a mounted NAS)
- Manual: Settings → Backup now, or `npx tsx scripts/backup.ts`
- Restore: `./scripts/restore.sh /path/to/archive.tar`  
  The script writes a safety dump first and waits before applying SQL. Restore is destructive and owner-only.

### Optional integrations

| Integration | Notes |
| --- | --- |
| SMS | Provider abstraction. Console logger ships by default; set HTTP endpoint + token in Settings |
| Email | SMTP via Nodemailer |
| Square | Payments stay pending until Square confirms |
| Ollama | Local only. Customer names, phones, emails, payments and device PINs are stripped |
| MinIO / S3 | Set `FILE_STORAGE_DRIVER=s3` and the `S3_*` variables |

### Printers

Configure A4, label and receipt profiles in Settings. Documents open a print dialog with shop branding. USB barcode scanners type into search and stocktake fields.

## Security notes

- Passwords: Argon2id
- Sessions: httpOnly cookies, never localStorage
- TOTP secrets and device PINs: AES-256-GCM
- Device PINs are deleted when a job is completed
- Permissions are checked in services, not only in the UI
- Audit log records price, stock, refund, reveal and staff changes
- The owner remains responsible for Privacy Act / APP configuration and tax record retention

## Architecture

```
src/app            UI and route handlers
src/server         Domain services, auth, RBAC, integrations
src/worker         SMS, email, backup and SLA jobs
prisma             Schema, migrations, seed
docker             Production image, Caddy, entrypoint
```

WorkshopOS is one shop, one brand. It is not a multi-tenant SaaS product.
