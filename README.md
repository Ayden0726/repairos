# WorkshopOS

Self-hosted repair-shop operations for Australian electronics / computer repair businesses.

**Current version: 1.2.41** (API + Windows client) · Repo: [Ayden0726/repairos](https://github.com/Ayden0726/repairos)

Australian English, **AUD**, **GST**, financial year **July–June**.

## What it is

WorkshopOS runs your shop’s day-to-day work on **your own server** — and can also run as a **home / side-hustle** profile with a slimmer nav:

- Repair tickets, customers, devices, quotes, invoices, inventory, purchasing, refurbished stock, calendar, reports, backups, and staff roles
- **Home / side hustle mode** — Settings → App (or first-run setup): slim nav, guided job intake, job templates, Parts + Money, beginner tips
- **Business expenses** — GST treatments, suppliers, recurring drafts, CSV export for your accountant ([docs/SETTINGS_AND_SECURITY.md](docs/SETTINGS_AND_SECURITY.md#using-expenses))
- **Encrypted API credentials** under **Settings → Integrations** — WooCommerce, POS/Square, OpenAI, Twilio, SMTP, IMEI, shipping ([docs/SETTINGS_AND_SECURITY.md](docs/SETTINGS_AND_SECURITY.md))
- **POS register** (admin-enabled) with AU EFTPOS guidance — [docs/POS_EFTPOS.md](docs/POS_EFTPOS.md)
- **WordPress / 10Web website API** — WorkshopOS is the source of truth for public quotes, bookings, Sell Your Tech, customer portal, and refurbished listings ([docs/WORDPRESS_INTEGRATION.md](docs/WORDPRESS_INTEGRATION.md))
- A **Windows desktop app** on each shop PC
- A **central server** (Docker) on a Linux PC, NAS, VM, WSL, or **Raspberry Pi 3B+** ([docs/RASPBERRY_PI.md](docs/RASPBERRY_PI.md))

Nothing is required from a SaaS cloud — data stays on your machine / LAN.

### What’s new in 1.2.41

| Area | Change |
| --- | --- |
| Home profile | Full shop ↔ Home / side hustle toggle (setup + Settings → App) |
| Slim nav | Home hides POS, bookings, multi-site, staff, fitment, etc. until Full shop |
| Guided intake | Step labels, job templates, solo (no tech assign) on New repair |
| Parts / Money | Inventory + Reports retitled; simple money summary API |
| Tips | Dismissible beginner tips on Home, New repair, Parts |
| Version | API + client **1.2.41** |

Carries forward 1.2.40: fitment, offline sync, multi-site locations/bins.