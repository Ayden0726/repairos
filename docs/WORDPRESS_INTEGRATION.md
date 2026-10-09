# WordPress / 10Web Integration

WorkshopOS (TechBench NQ) is the **single source of truth** for repairs, quotes, bookings, Sell Your Tech, refurbished stock, invoices, warranties, and messaging.

WordPress / 10Web / WooCommerce **must not** keep a separate repair database. The site calls WorkshopOS over HTTPS using dedicated API credentials.

```
  WordPress / 10Web / WooCommerce          WorkshopOS API (source of truth)
  --------------------------------         --------------------------------
  Forms, tracking page, portal UI  ------>  /api/public/*   (X-WorkshopOS-Key)
  Customer login / account pages   ------>  /api/customer/* (JWT portal token)
  WooCommerce product sync         <----->  /api/website/*  (staff JWT)
```

The same public + customer APIs are designed so a future mobile app can reuse them without a second backend.

---

## Security rules

| Do | Do not |
| --- | --- |
| Create a WorkshopOS website API key (`wos_…`) | Use WordPress admin / application passwords as API auth |
| Store the key in WP server-side config / env | Put the key in JavaScript, themes, or page HTML |
| Call the API over HTTPS | Expose IMEI, serial, address, or internal notes on public responses |
| Rate-limit forms on the WP side too | Mirror repairs into a custom WP DB table |
| Validate uploads (type + size) before POST | Trust client-supplied quote totals or status changes |

Public endpoints are rate-limited (`public` policy: 60 req/min/IP). Staff endpoints never appear under `/api/public`.

---

## Environment variables

### WorkshopOS API server

| Variable | Purpose | Example |
| --- | --- | --- |
| `ConnectionStrings__Default` | PostgreSQL | `Host=…;Database=workshopos;Username=…;Password=…` |
| `Jwt__SigningKey` | JWT signing (≥32 chars) | long random secret |
| `Jwt__Issuer` | Token issuer | `WorkshopOS` |
| `Jwt__Audience` | Staff audience | `WorkshopOS` |
| `Jwt__CustomerAudience` | Portal audience | `WorkshopOS.CustomerPortal` |
| `Website__PublicBaseUrl` | Links in QR / emails | `https://techbenchnq.com.au` |
| `ASPNETCORE_URLS` | Listen URL | `http://0.0.0.0:5088` |

WooCommerce consumer key/secret are stored in WorkshopOS settings (`website.woocommerce`) via the staff API — not in WordPress as the repair DB.

### WordPress (server-side only)

| Variable / option | Purpose |
| --- | --- |
| `WORKSHOPOS_API_BASE` | e.g. `https://api.techbenchnq.com.au` |
| `WORKSHOPOS_API_KEY` | `wos_…` key from WorkshopOS |
| `WORKSHOPOS_PORTAL_COOKIE` / session | Optional: store customer JWT after `/api/customer/login` |

---

## Setup checklist

1. Deploy / update WorkshopOS API (Docker or host). Migrations apply on startup.
2. Sign in as owner → create a website API key:
   - `POST /api/website/api-keys` with staff JWT and permission `website.manage`
   - Copy the raw key once (`wos_…`). Only the prefix is stored for display later.
3. On WordPress/10Web, install a thin integration plugin (strategy below) or custom PHP that:
   - Reads `WORKSHOPOS_API_BASE` + `WORKSHOPOS_API_KEY`
   - Proxies form POSTs to WorkshopOS
   - Never writes repair rows to `wp_posts` / custom tables
4. (Optional) Configure WooCommerce credentials in WorkshopOS:
   - `PUT /api/website/woocommerce`
5. Test: submit a repair quote from the site → confirm ticket appears in WorkshopOS → track with code + PIN.

---

## Authentication

### Public website API

Header on every `/api/public/*` call:

```http
X-WorkshopOS-Key: wos_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
Content-Type: application/json
```

Missing/invalid key → `401` `{ "code": "unauthorized", ... }`.

### Customer portal

1. `POST /api/customer/login` or `/api/customer/register` (no API key required; rate-limited).
2. Use returned JWT:

```http
Authorization: Bearer <customer_access_token>
```

Token claims include `portal=customer` and `customer_id`. Staff tokens cannot call portal routes.

### Staff / integration admin

Standard WorkshopOS staff JWT (`Authorization: Bearer …`) with permissions such as `website.manage`, `selltech.*`, `messages.*`.

---

## Public endpoints

Base: `{API}/api/public`

### `POST /repair-quote`

Creates a customer (or matches by email/phone), device, quote request, and repair ticket with tracking code + PIN.

**Request** (JSON property names must match exactly)

```json
{
  "contactName": "Sam Nguyen",
  "phone": "0400111222",
  "email": "sam@example.com",
  "suburb": "Townsville",
  "deviceCategory": "Phone",
  "brand": "Apple",
  "model": "iPhone 13",
  "faultDescription": "Cracked screen",
  "preferredContact": "Sms",
  "notes": "Drop damage"
}
```

**Response `200`**

```json
{
  "quoteId": "…",
  "quoteNumber": "QTE-2026-00001",
  "trackingCode": "TB-2026-001284",
  "accessPin": "483921",
  "status": "Booking Received",
  "message": "Quote request received. Use your tracking code and PIN to check status."
}
```

Show the PIN once to the customer (email/SMS). It is stored hashed only.

### `POST /repair-booking`

```json
{
  "contactName": "Sam Nguyen",
  "phone": "0400111222",
  "email": "sam@example.com",
  "deviceCategory": "Laptop",
  "brand": "Dell",
  "model": "XPS 13",
  "faultOrService": "Won't charge",
  "preferredStart": "2026-10-08T09:00:00+10:00",
  "preferredEnd": "2026-10-08T09:30:00+10:00",
  "notes": "After school"
}
```

**Response:** booking id, tracking code, PIN, confirmation message.

### `GET /booking-availability?day=2026-10-08T00:00:00Z`

Returns open slots from shop hours − existing bookings − capacity.

### `GET /same-day-hint?serviceCode=PHONE-SCREEN-REPLACE&category=Phone`

```json
{ "sameDayPossible": true, "message": "Same-day repair may be available.", "stockAvailable": 5 }
```

Rules: stock `0` → part order required; stock `≤ 2` → low-stock warning.

### `POST /sell-device`

Full Sell Your Tech payload (category, brand, model, storage, RAM, CPU/GPU, colour, serial/IMEI, battery, carrier, condition flags, Find My / account locks, photos URLs, contact, suburb, preferred contact).

**Response:** `{ "requestId", "reference": "SYT-2026-00042", "status": "New", "message": "…" }`

### `POST /custom-pc` — **removed (HTTP 410)**

Custom PC Builds is no longer a WorkshopOS module. Use ordinary repair intake for desktop computers, or Inventory/POS for component sales. Historical rows may remain in the database.

### `GET /repair-status/{trackingCode}?pin=483921`

Customer-facing status (no IMEI/serial/address/internal notes):

```json
{
  "trackingCode": "TB-2026-001284",
  "deviceLabel": "Apple iPhone 13",
  "statusKey": "awaiting_approval",
  "statusName": "Awaiting Customer Approval",
  "statusColour": "#ca8a04",
  "estimatedCompletion": "2026-10-10T17:00:00+10:00",
  "customerNotes": "Screen assembly ordered",
  "quoteTotal": 289.00,
  "depositPaid": 50.00,
  "remainingBalance": 239.00,
  "warrantyStatus": null,
  "canApproveQuote": true,
  "canDeclineQuote": true,
  "quoteId": "…",
  "quoteNumber": "Q-2026-000123",
  "quoteRevisionNumber": 2,
  "timeline": [
    { "at": "…", "summary": "Website repair quote request received", "eventType": "website.quote_request" }
  ],
  "invoiceNumber": null,
  "messagingAvailable": true
}
```

### `GET /repair-status/link/{accessToken}`

Same payload using the secure access token (no PIN). Prefer this for emailed magic links.

### `POST /repair/{quoteId}/approve` · `POST /repair/{quoteId}/decline`

```json
{
  "accessPin": "483921",
  "comment": "Please go ahead",
  "expectedRevisionNumber": 2
}
```

Audit stores customer, repair, quote revision, action, time, IP, user-agent, comment.

If the quote is edited after approval, WorkshopOS resets it to `Draft` and increments `revisionNumber`. Approving an old revision with `expectedRevisionNumber` fails — the site must refresh status and collect a new decision.

### `POST /repair-status/{trackingCode}/message`

Message TechBench from the tracking page (PIN required; no portal login).

```json
{ "accessPin": "483921", "body": "Can I collect after 4pm?", "subject": null }
```

### `POST /refurbished/sync-status`

WordPress/WooCommerce pushes sold/reserved status **back** into WorkshopOS (source of truth).

```json
{ "stockIdOrSku": "TB-RFB-00382", "status": "Sold", "actualSalePrice": 649.00 }
```

Allowed statuses: `Reserved`, `Sold`, `Listed`, `Ready for Sale`.

---

## Customer portal endpoints

Base: `{API}/api/customer`

| Method | Path | Auth | Description |
| --- | --- | --- | --- |
| POST | `/login` | Anonymous | Email/password → JWT |
| POST | `/register` | Anonymous | Create portal account (+ CRM customer) |
| GET | `/repairs` | Portal JWT | Repair list + status |
| GET | `/quotes` | Portal JWT | Quotes |
| GET | `/devices` | Portal JWT | Devices on file |
| GET | `/invoices` | Portal JWT | Invoices / balances |
| GET | `/warranties` | Portal JWT | Warranty coverage |
| GET | `/messages` | Portal JWT | Conversation threads |
| GET | `/messages/{threadId}` | Portal JWT | Messages (customer-visible only) |
| POST | `/messages` | Portal JWT | Post a customer message |
| GET | `/sell-requests` | Portal JWT | Sell Your Tech submissions |

**Login request**

```json
{ "email": "sam@example.com", "password": "…" }
```

**Login response**

```json
{
  "accessToken": "…",
  "expiresAt": "…",
  "customerId": "…",
  "displayName": "Sam Nguyen",
  "email": "sam@example.com"
}
```

Internal technician notes are never returned on portal or public routes.

---

## Staff endpoints (WorkshopOS app / admin)

| Area | Base | Permissions |
| --- | --- | --- |
| API keys, Woo, publish, QR, dashboard | `/api/website/*` | `website.manage` (dashboard: `tickets.view`) |
| Notification templates | `/api/website/notification-templates` | `settings.manage` |
| Sell Your Tech | `/api/sell-tech/*` | `selltech.view` / `selltech.manage` |
| Messaging | `/api/messages/*` | `messages.view` / `messages.manage` |

Useful staff calls:

- `GET /api/website/dashboard` — cards for diagnosis / approval / in progress / testing / ready / Sell Tech / offers / refurb / low stock / today’s appointments / outstanding balances
- `POST /api/sell-tech/{id}/offer` — offer calculator fields + recommended offer
- `POST /api/sell-tech/{id}/create-refurb` — one-click refurb job (`TB-RFB-#####`)
- `POST /api/website/woocommerce/publish` — mark Listed + push Woo product when credentials configured
- `GET/PUT /api/website/webhooks` — outbound webhook URL/secret
- `GET/PUT /api/website/booking-availability-settings` — open/close hours, slot length, capacity
- `GET /api/website/qr/{kind}/{id}` — `kind`: `repair`, `invoice`, `refurb`, `part`
- `GET/POST /api/custom-pc` · `POST /api/custom-pc/{id}/status` · `POST /api/custom-pc/{id}/convert-to-build`
- `GET/POST /api/refurb-diagnostics/checklists` · `POST /api/refurb-diagnostics/results`

---

## Error codes

| HTTP | `code` (typical) | Meaning |
| --- | --- | --- |
| 400 | validation / `title` | Bad payload |
| 401 | `unauthorized` / `invalid_credentials` | Missing API key or bad portal login |
| 403 | `forbidden` | Wrong PIN / no permission |
| 404 | `not_found` | Unknown tracking / resource (PIN failures intentionally look like 404) |
| 429 | — | Rate limited |
| 500 | — | Unexpected server error (`detail` may include message) |

Shape:

```json
{ "title": "…", "code": "unauthorized", "status": 401 }
```

---

## Repair statuses (customer-facing)

WorkshopOS seeds/extends statuses including:

Booking Received → Awaiting Device → Checked In → Diagnosis In Progress → Diagnosis Complete → Awaiting Customer Approval → Approved → Parts Required → Parts Ordered → Parts Arrived → Repair In Progress → Testing → Quality Check → Ready for Collection → Completed / Cancelled

Every status change is timestamped on the repair event / audit history.

---

## Automated notifications

Templates are editable under Settings via:

- `GET /api/website/notification-templates`
- `PUT /api/website/notification-templates/{key}`

Default keys: `booking_confirmed`, `device_checked_in`, `diagnosis_complete`, `quote_ready`, `quote_approved`, `parts_ordered`, `repair_started`, `testing_begun`, `ready_for_collection`, `payment_received`, `repair_completed`, `warranty_expiring`, `sell_request_received`.

Messages are queued to a notification outbox (SMS/email channels). Configure delivery providers on the WorkshopOS server; WordPress does not send shop operational SMS.

---

## Webhooks (WorkshopOS → WordPress)

Configure via staff API:

- `GET/PUT /api/website/webhooks` → `{ enabled, url, secret }`

When enabled, WorkshopOS POSTs JSON envelopes:

```json
{
  "id": "…",
  "event": "repair.status_changed",
  "at": "2026-10-06T08:00:00Z",
  "data": { "trackingCode": "TB-2026-001284", "statusKey": "ready_pickup", "statusName": "Ready for Collection" }
}
```

Headers: `X-WorkshopOS-Event`, `X-WorkshopOS-Webhook-Secret` (when configured).

| Event | When |
| --- | --- |
| `repair.status_changed` | Staff changes repair status |
| `refurb.listed` | Publish refurbished device (also attempts WooCommerce REST product create when credentials are set) |

**WordPress receiver requirements**

- HTTPS endpoint, e.g. `https://yoursite/wp-json/workshopos/v1/webhook`
- Shared secret header check
- Idempotent handling by event `id`
- Update Woo product stock/status only — never create WorkshopOS repairs from webhooks

Sold/reserved sync **into** WorkshopOS uses `POST /api/public/refurbished/sync-status` (API key), not webhooks.

---

## WordPress plugin strategy

Ship a small plugin (e.g. `workshopos-bridge`) — not a second ERP.

### Modules (shortcodes / blocks)

| Feature | WP surface | WorkshopOS call |
| --- | --- | --- |
| Repair quote form | Block / shortcode | `POST /api/public/repair-quote` |
| Repair booking | Calendar UI | availability + `POST /repair-booking` |
| Repair tracking | Code + PIN page | `GET /repair-status/{code}` |
| Quote approve/decline | Buttons on tracking page | `POST /repair/{quoteId}/approve\|decline` |
| Sell Your Tech | Multi-step form + photos | `POST /sell-device` (upload media to WP or S3, send URLs) |
| Custom PC | Form | `POST /custom-pc` |
| Customer portal | Account pages | `/api/customer/*` with JWT in HttpOnly cookie |
| Refurbished store | WooCommerce products | Publish from WorkshopOS; sync SKU/stockId/status |

### Plugin architecture

1. **Settings page** — API base URL + API key (capability `manage_options` only).
2. **Server proxy** — `admin-ajax.php` or REST routes under `workshopos/v1/*` that add `X-WorkshopOS-Key` server-side. Browser never sees the key.
3. **Forms** — validate locally, then proxy; show tracking + PIN from JSON response.
4. **Portal** — login proxy stores JWT in HttpOnly cookie; subsequent WP REST calls attach it to WorkshopOS.
5. **WooCommerce** — on `woocommerce_order_status_completed` / reservation, call WorkshopOS to set refurbished device `Sold` / `Reserved` by SKU or stock id.
6. **No custom tables** for tickets/quotes — optional WP transient cache for availability only (TTL minutes).

### Photo uploads

- Accept jpeg/png/webp only; max size e.g. 5 MB each.
- Store in WP media or object storage; send HTTPS URLs in `photoUrls` on Sell Your Tech.
- WorkshopOS stores URLs, not WordPress binary blobs.

### Example PHP proxy (sketch)

```php
function wos_public_post(string $path, array $body): array {
  $base = rtrim(getenv('WORKSHOPOS_API_BASE'), '/');
  $key  = getenv('WORKSHOPOS_API_KEY');
  $res = wp_remote_post($base . '/api/public/' . ltrim($path, '/'), [
    'headers' => [
      'Content-Type' => 'application/json',
      'X-WorkshopOS-Key' => $key,
    ],
    'body' => wp_json_encode($body),
    'timeout' => 30,
  ]);
  // map WP_Error / HTTP status to form errors
  return json_decode(wp_remote_retrieve_body($res), true);
}
```

---

## Sell Your Tech / offer calculator / refurb

Staff workflow in WorkshopOS:

1. Website creates `SellDeviceRequest` (`New`).
2. Staff review → offer calculator (resale, parts, labour, fees, shipping, risk, min profit/margin) → recommended offer range / max purchase.
3. Global defaults in Settings (`website.offer_defaults`); per-device overrides on the offer.
4. Send offer → customer accept/decline.
5. On purchase → **Create Refurbishment Job** → `TB-RFB-#####`, copies device details, photos, faults, serial/IMEI, purchase price.
6. Refurb statuses: Purchased → Inspection → Parts Required → Repair → Testing → Cleaning → Final QC → Ready for Sale → Listed → Reserved → Sold.
7. Category diagnostic checklists (phone/laptop defaults seeded).
8. After QC → **Publish to Website** → Listed in WorkshopOS for Woo sync.

---

## QR / barcode payloads

`GET /api/website/qr/{kind}/{id}` returns:

```json
{
  "kind": "repair",
  "entityId": "…",
  "label": "TB-2026-001284",
  "payload": "workshopos:repair:{guid}:{ticketNumber}",
  "publicUrl": "https://techbenchnq.com.au/repair-status?token=…"
}
```

Windows client can deep-link on `workshopos:repair:…` to open the job.

---

## Versioning & compatibility

Website integration ships with WorkshopOS **1.2.26+**. Bump the WordPress plugin’s required API version when breaking public DTO fields. Prefer additive JSON fields.

OpenAPI UI: `{API}/swagger` (when enabled).
