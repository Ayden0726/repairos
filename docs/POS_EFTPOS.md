# POS & EFTPOS (Australia)

WorkshopOS includes a **point-of-sale register** for counter sales (accessories, parts, quick labour).  
**POS is off by default.** An administrator must enable it under **Settings → POS** (or Settings → Modules).

Version **1.2.29+**.

---

## Enable POS (admins)

1. Sign in as Owner / Administrator (permission `settings.manage`).
2. Open **Settings → POS**.
3. Turn **Enable POS** on and Save.
4. Optionally set a payment provider (below) and Save again.
5. Sign out/in or reload the shell — **POS** appears in the sidebar between Invoices and Inventory.

You can also toggle the `pos` module under **Settings → Modules**. Enabling either path keeps module visibility and POS settings in sync.

Required permissions for staff:

| Permission | Purpose |
| --- | --- |
| `pos.view` | See sales history & provider info |
| `pos.sell` | Complete a sale |
| `pos.manage` | Void sales & (with settings) configure POS |

Front Desk and Sales roles include `pos.view` + `pos.sell` by default. Owner / Admin / Manager include `pos.manage`.

---

## What a sale does

1. Adds lines from inventory (SKU) and/or custom priced lines.
2. Creates an **Invoice** (`INV-…`) marked **Paid**.
3. Creates a **POS sale** (`POS-…`) linked to that invoice.
4. Records a **Payment** (Cash / Eftpos / Card / …).
5. Decrements on-hand stock for inventory lines (transaction type `Sale`).
6. Walk-in customers use a shared **Walk-in Customer** CRM record when no customer is selected.

Voiding a sale (permission `pos.manage`) cancels the invoice and restocks inventory.

---

## EFTPOS / card reader brands you can use

WorkshopOS does **not** store card PAN/CVV. Payment data stays on the terminal or the provider’s cloud.

| Provider key | Brand | Best for | How it connects |
| --- | --- | --- | --- |
| `None` | Cash / no terminal | Cash drawers | No hardware |
| `ManualEftpos` | **Any** bank / independent EFTPOS | Existing standalone machine | Staff types approval / RRN into POS |
| `Tyro` | **Tyro** (Yoximo, T2800, …) | AU integrated till | Tyro Cloud / Terminal API + Merchant & Terminal IDs |
| `Square` | **Square Terminal / Reader** | Retail accessories | Square Terminal API + Location ID + access token |
| `StripeTerminal` | **Stripe Terminal** (WisePOS E, BBPOS) | Stripe-based shops | Stripe Terminal API + reader/location + API key |
| `Linkly` | **Linkly** (PC-EFTPOS) + Ingenico/Verifone | Bank-supplied pin-pads | Linkly Client on the till PC + Merchant/Terminal IDs |

### Recommendation for a typical AU repair shop

1. **Already have a bank EFTPOS?** → Use **Manual EFTPOS** today (zero integration cost).
2. **Want tap-to-pay from the till screen?** → **Tyro** is the most common AU integrated choice for workshops.
3. **Already on Square for online/retail?** → **Square Terminal**.
4. **Already on Stripe?** → **Stripe Terminal**.
5. **Bank supplied PC-EFTPOS / Linkly?** → **Linkly**.

---

## How to connect each provider

### Manual EFTPOS (any brand)

1. Keep using your current terminal as today.
2. Settings → POS → Provider = **ManualEftpos** → Save.
3. When ringing up: Payment method **Eftpos**, enter the **approval code / RRN** from the slip into **Provider reference**.

### Tyro

1. Apply at [tyro.com](https://www.tyro.com/) and order a terminal.
2. Request **integrated** credentials (Cloud API / Terminal API) from Tyro.
3. Pair the terminal on shop Wi‑Fi or Ethernet.
4. Settings → POS → Provider = **Tyro**, enter **Merchant ID**, **Terminal ID**, paste **API key** → Save.
5. On sale: choose **Eftpos**; optional reference overrides the auto-generated Tyro-style id until live push-to-terminal is active with validated keys.

Docs: [Tyro Developers](https://www.tyro.com/developers/)

### Square

1. Square AU account → order **Terminal** or **Reader**.
2. Developer Dashboard → Application → **Production access token**.
3. Create a **Location**; pair the device under Devices.
4. Settings → POS → Provider = **Square**, Location ID + API key → Save.

Docs: [Square Terminal API](https://developer.squareup.com/docs/terminal-api/overview)

### Stripe Terminal

1. Stripe Dashboard (AU) → enable Terminal → register a reader (WisePOS E recommended).
2. Create an API key with Terminal permissions.
3. Settings → POS → Provider = **StripeTerminal**, location/reader ids + API key → Save.

Docs: [Stripe Terminal](https://docs.stripe.com/terminal)

### Linkly (PC-EFTPOS)

1. Merchant agreement via your bank or [Linkly](https://www.linkly.com.au/).
2. Install **Linkly Client** on the Windows till PC and pair the pin-pad.
3. Settings → POS → Provider = **Linkly**, Merchant ID + Terminal ID → Save.
4. Sales store the Linkly receipt / RRN; the Windows client can bridge to Linkly Client when installed.

---

## API (staff JWT)

| Method | Path | Permission | Notes |
| --- | --- | --- | --- |
| GET/PUT | `/api/settings/pos` | settings.view / settings.manage | Enable + provider credentials |
| PUT | `/api/settings/modules` | settings.manage | `{ "visibility": { "pos": true } }` |
| GET | `/api/pos/providers` | pos.view | Brand catalogue + setup steps |
| GET | `/api/pos/sales` | pos.view | Optional `from` / `to` |
| GET | `/api/pos/sales/{id}` | pos.view | Detail |
| POST | `/api/pos/sales` | pos.sell | Complete sale (invoice + stock) |
| POST | `/api/pos/sales/{id}/void` | pos.manage | Void + restock |

Example create body:

```json
{
  "customerId": null,
  "paymentMethod": "Eftpos",
  "providerReference": "123456",
  "notes": null,
  "lines": [
    { "inventoryItemId": "…", "quantity": 1, "unitPrice": null },
    { "inventoryItemId": null, "description": "Screen protector fit", "quantity": 1, "unitPrice": 15.00 }
  ]
}
```

API keys are stored server-side in `pos.settings` and never returned in full — only `apiKeyConfigured: true|false`.

---

## Security notes

- Never type full card numbers into WorkshopOS.
- Prefer provider APIs / terminals that are PCI-scoped; WorkshopOS only stores amount, method, and provider reference.
- Rotate API keys in the provider dashboard if a till PC is compromised; clear the key via Settings → POS → Clear API key.
