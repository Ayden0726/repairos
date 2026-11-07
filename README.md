# WorkshopOS

Self-hosted repair-shop operations for Australian electronics / computer repair businesses.

**Current version: 1.2.40** (API + Windows client) · Repo: [Ayden0726/repairos](https://github.com/Ayden0726/repairos)

Australian English, **AUD**, **GST**, financial year **July–June**.

## What it is

WorkshopOS runs your shop’s day-to-day work on **your own server**:

- Repair tickets, customers, devices, quotes, invoices, inventory, purchasing, refurbished stock, calendar, reports, backups, and staff roles
- **Business expenses** — GST treatments, suppliers, recurring drafts, CSV export for your accountant ([docs/SETTINGS_AND_SECURITY.md](docs/SETTINGS_AND_SECURITY.md#using-expenses))
- **Encrypted API credentials** under **Settings → Integrations** — WooCommerce, POS/Square, OpenAI, Twilio, SMTP, IMEI, shipping ([docs/SETTINGS_AND_SECURITY.md](docs/SETTINGS_AND_SECURITY.md))
- **POS register** (admin-enabled) with AU EFTPOS guidance — [docs/POS_EFTPOS.md](docs/POS_EFTPOS.md)
- **WordPress / 10Web website API** — WorkshopOS is the source of truth for public quotes, bookings, Sell Your Tech, customer portal, and refurbished listings ([docs/WORDPRESS_INTEGRATION.md](docs/WORDPRESS_INTEGRATION.md))
- A **Windows desktop app** on each shop PC
- A **central server** (Docker) on a Linux PC, NAS, VM, WSL, or **Raspberry Pi 3B+** ([docs/RASPBERRY_PI.md](docs/RASPBERRY_PI.md))

Nothing is required from a SaaS cloud — data stays on your machine / LAN.

### What’s new in 1.2.40

| Area | Change |
| --- | --- |
| Fitment | Parts ↔ device compatibility database + WinUI |
| Offline | Client mutation queue + server sync outbox ack/replay |
| Multi-site | Workshop Locations + inventory bins (Settings → Sites) |
| Scope | Board-level repair **won’t do**; multi-site not SaaS multi-tenant |
| Version | API + client **1.2.40** |

Carries forward 1.2.39: donors, loans, refunds, POS refurbished, repair media, printers.