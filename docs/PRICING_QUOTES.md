# Pricing & Quotes (WorkshopOS)

Server is the source of truth for quote totals via `PricingCalculator` and `POST /api/pricing/preview`.

## Settings

| Area | Where | Storage |
| --- | --- | --- |
| Labour / parts markup / profitability / rounding / discounts / quote defaults | Settings → Pricing | `business_settings` key `pricing.settings` |
| Markup tiers (Option B) | Settings → Pricing | table `markup_tiers` |
| Service catalogue | Settings → Services | table `service_pricing` |
| Tax (enabled, rate, inclusive) | Settings → Tax | business profile GST fields |

Seed defaults (not permanent hardcodes): labour fee **$50**, flat markup **20%**, rounding **End9**, validity **14 days**.

## Quote lifecycle

`Draft` → `Sent` → `Viewed` / `Accepted` / `Declined` / `Expired` / `Converted` / `Cancelled`

Accepted quotes are **frozen** (financial snapshot preserved). Settings changes do not rewrite historical quotes. Revisions stored in `quote_revisions`; overrides in `quote_audit_log`.

## Customer print

`GET /api/quotes/{id}/print` — HTML like repair job sheets. **No** cost, markup, or profit fields.

## Permissions

`quotes.view` / `quotes.manage`, `pricing.view` / `pricing.edit` / `pricing.edit_settings`, plus `pricing.view_cost`, `pricing.view_profit`, `pricing.change_markup`, `pricing.override_labour`, `pricing.apply_discount`, `pricing.override_price`, `pricing.approve_low_margin`.
