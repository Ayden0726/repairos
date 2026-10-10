# Windows Client Navigation (TechBench NQ)

**Version 1.2.32+** — 700+ phone model catalogue with search on New Ticket / Quotes; POS is admin-gated (off by default); repair import, deposits, and discount presets. Custom PC Builds removed from the product surface.

## Shell chrome (minimal)

- Narrow collapsible sidebar (~196px)
- Flat nav (no nested groups)
- Top: search (Ctrl+K), **+ Repair**, notifications, Settings gear
- Brand: **TechBench NQ** + business name
- Compact theme toggle + Sign out in pane footer

## Sidebar (flat)

Dashboard · Repairs · Bookings · Customers · Quotes · Invoices · **POS** (when enabled) · Inventory · Refurbished · Reports

Repair status filters stay on the Repairs page. AI / Knowledge / Purchasing / Staff / Backups remain available via Settings or module visibility — not cluttering the primary rail.

## Removed

- **Custom PC Builds** — no nav item, no builder pages in the shell. Historical `pc_builds` / `custom_pc_requests` tables retained. Desktop repairs use ordinary repair tickets.

## Command centre commands

New Repair · New Customer · New Booking · Create Quote · Create Invoice · **Open POS** · Special Order · Trade-In · Stock Adjustment

## Module visibility

Administrators enable/hide modules via Settings → POS / module visibility (`ModuleVisibility`). **POS defaults off** until an admin enables it. See [POS_EFTPOS.md](POS_EFTPOS.md).
