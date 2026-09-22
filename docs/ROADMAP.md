# Development Roadmap

| Phase | Focus | Exit criteria |
| --- | --- | --- |
| **1** | Solution, API, auth, permissions, WinUI shell, Docker, health, setup | Build + tests green; login works; shell navigates |
| **2** | Customers, devices, repairs, intake, timeline | Create/open repair end-to-end |
| **3** | Full operational dashboard | Cards/pipeline/workload use live queries + tests |
| **4** | Quotes + pricing engine | Quote → approve |
| **5** | Inventory, barcodes, POs, reservations | Reserve/consume stock on repair |
| **6** | Invoices, payments, GST PDFs | Tax invoice PDF |
| **7** | Notifications centre + SMS/email providers | Real send or explicit “provider not configured” |
| **8** | PC diagnostics, PC builds, used tech | Margin tracking |
| **9** | Calendar, knowledge, QA | Booking + QA gate |
| **10** | Reporting CSV/PDF | Filtered reports |
| **11** | Optional AI (Ollama) | Confirm-before-apply |
| **12** | Offline cache, SignalR events, backups UI, installer, hardening | Multi-PC live updates + MSI |

## Phase completion rules

After each phase: build solution, run tests, fix failures, update migrations, update CHANGELOG and docs, commit. Do not start the next phase with failing fundamentals.
