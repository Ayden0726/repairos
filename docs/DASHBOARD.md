# Dashboard Data Requirements (Phase 3)

Phase 1 ships the Dashboard **route shell only**. Live metrics are Phase 3.

## Summary cards (click → filtered Repairs)

| Card | Source rule |
| --- | --- |
| Open Jobs | Status not completed/cancelled |
| Due Today | DueDate::date = today, open |
| Awaiting Approval | Status = Awaiting Quote Approval |
| Waiting for Parts | Status = Waiting for Parts |
| Ready for Pickup | Status = Ready for Pickup |
| Overdue | DueDate < now AND open |
| Revenue — 30 Days | Paid invoice totals (AUD) last 30 days |
| Gross Profit — 30 Days | Revenue − COGS for same window |

## Other panels

- Repair Pipeline — counts per status stage
- Urgent / Overdue — severity sort (overdue urgent → …)
- Technician Workload + Unassigned
- Waiting for Parts detail
- Low Stock
- Today's Work
- Recent Activity (from audit / domain events)

## Validation rules

- Average repair hours: only jobs with valid start AND completion; never negative
- No duplicate rows from incorrect joins (tests cover count queries)
- Production never uses seed/fake numbers
