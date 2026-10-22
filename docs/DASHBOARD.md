# Dashboard

Each signed-in user can customise their dashboard. Layout is stored per user on the server (`user_ui_preferences` / `api/me/preferences/dashboard`).

## Widgets

| Id | Title | Source |
| --- | --- | --- |
| `myTickets` | My open tickets | Repairs assigned to the current user |
| `summaryCards` | Summary cards | `GET api/dashboard` cards (click → filtered repairs) |
| `calendar` | Calendar | Upcoming bookings (14 days) + link to full Calendar page |
| `pipeline` | Repair pipeline | Pipeline stages, urgent/overdue, unassigned |
| `workload` | Technician workload | Open jobs per technician |
| `lowStock` | Low stock | Inventory below reorder |
| `activity` | Recent activity | Audit / domain activity |

## Customise

1. Open **Dashboard**
2. Tap **Customise**
3. Toggle **Show** and use **Up** / **Down** to reorder
4. **Save layout** (persists for this user on this server) or **Reset defaults**

## Calendar on the dashboard

The calendar widget lists upcoming bookings for the next 14 days. Use **Open full calendar** for the dedicated Calendar page (create/edit bookings).
