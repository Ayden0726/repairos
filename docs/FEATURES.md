# WorkshopOS features

Self-hosted repair-shop operations for Australian electronics / computer repair businesses.
Stack: **WinUI 3 Windows client** → REST + SignalR → **ASP.NET Core API** → **PostgreSQL**.

Currency and tax are **AUD / GST-inclusive** (10% default, configurable at setup).

## Core (Phases 1–2)

| Feature | Details |
| --- | --- |
| First-run setup | Business profile (name, ABN, address, GST, labour rate) + owner account |
| Auth | JWT login / refresh / logout, hashed refresh tokens, rate limiting |
| Roles & permissions | Owner, Administrator, Manager, Technician, Front Desk, Sales, Read Only |
| Health | `/api/health` (+ authenticated `/api/health/detail`) |
| Users / settings | Staff accounts, business profile, module visibility |
| Customers | Individuals & businesses, contact prefs, create/list/detail |
| Devices | Per-customer devices (category, brand/model, serial/IMEI) |
| Repairs | Intake, ticket numbers `REP-YYYY-#####`, statuses, priorities, assign tech |
| Repair detail | Notes (customer/internal), diagnosis, timeline, encrypted passcode flag |
| Search | Global search across repairs, customers, devices |
| SignalR hub | `/hubs/workshop` for live multi-PC events |

## Operations (Phases 3–12)

| Module | Features |
| --- | --- |
| **Dashboard** | Open/due/awaiting/parts/ready/overdue cards, pipeline, urgent jobs, tech workload, low stock, recent activity, 30-day revenue & gross profit |
| **Quotes** | GST-aware quotes, line items, status workflow (draft → sent → approved/…) |
| **Inventory** | SKUs, on-hand / reserved / available, min stock, adjust, reserve & consume against tickets |
| **Purchasing** | Suppliers, purchase orders, receive lines into stock |
| **Invoices & payments** | Create from repair, record payments/deposits, balances |
| **Notifications** | Per-user inbox; mark read (SMS/email providers optional / “not configured”) |
| **PC Builds** | Build jobs with parts, cost/sell/margin |
| **Used Tech** | Buy-in / refurb / sell pipeline with expected margin |
| **Calendar** | Bookings (drop-off, consult, pickup, etc.) |
| **Knowledge** | Internal KB articles by category/tags |
| **QA** | Per-repair QA checklist results |
| **Reports** | Period summary: revenue, COGS, gross profit, repairs opened/completed, payments by method |
| **AI Assist** | Optional Ollama; disabled by default; never auto-applies suggestions |
| **Backups** | Manual backup records + marker files under configured backup directory |

## Windows client modules

Sidebar: Dashboard, Repairs, Customers, Calendar, Notifications, Quotes, Invoices, Used Tech, Inventory, Purchasing, PC Builds, Knowledge, AI Assist, Reports, Backups, Users, Settings.

## What is intentionally not fake

Empty lists show empty (or an honest “no records yet”). AI returns an explicit disabled message when Ollama is off. SMS/email send only when a provider is configured.
