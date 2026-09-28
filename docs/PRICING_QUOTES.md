# Pricing & Quotes (WorkshopOS)

Server is the source of truth for quote totals via `PricingCalculator` and `POST /api/pricing/preview`.

## Job-level pricing model

1. **Parts** — sum all landed part costs (cost + shipping + other × qty) → `PartsCostTotal`
2. **Markup once** — apply job markup % (or fixed/hybrid/tiered on the sum) → `PartsSellTotal`
3. **Labour once** — add a single job labour/service fee → `LabourFee`
4. Add additional charges, subtract discounts, apply rounding, then tax

Lines store part cost/qty/description only (no per-line labour or markup %). Summary panel shows: Parts cost → Markup → Parts sell → Labour → …

## Settings

| Area | Where | Storage |
| --- | --- | --- |
| Labour / parts markup / profitability / rounding / discounts / quote defaults | Settings → Pricing | `business_settings` key `pricing.settings` |
| Markup tiers (Option B) | Settings → Pricing | table `markup_tiers` |
| Service catalogue | Settings → Services + ServicePicker | `service_pricing` + `service_categories` / bundles / favourites |
| Device brands/models | Catalogue picker filters | `device_brands` / `device_models` |
| Tax (enabled, rate, inclusive) | Settings → Tax | business profile GST fields |

Seed defaults (not permanent hardcodes): labour fee **$50**, flat markup **20%**, rounding **End9**, validity **14 days**. These seed the job labour NumberBox and job markup % on new quotes.

## Quote lifecycle

`Draft` → `Sent` → `Viewed` / `Accepted` / `Declined` / `Expired` / `Converted` / `Cancelled`

Accepted quotes are **frozen** (financial snapshot preserved). Settings changes do not rewrite historical quotes. Revisions stored in `quote_revisions`; overrides in `quote_audit_log`.

## Customer print

`GET /api/quotes/{id}/print` — A4 HTML (`@page { size: A4; margin: 12mm }`, ~190mm sheet) opened by WinUI Print. Money is **hardcoded** as literal `$` + 2 decimals in the template (`<!-- workshopos-currency:$ -->`); never culture `¤`. Lists part descriptions with customer sell amounts (job markup allocated; **no** per-line labour), one **Labour / service** line, then Parts / Labour / Tax / Total. **No** cost, markup %, or profit fields.

## Permissions

`quotes.view` / `quotes.manage`, `pricing.view` / `pricing.edit` / `pricing.edit_settings`, plus `pricing.view_cost`, `pricing.view_profit`, `pricing.change_markup`, `pricing.override_labour`, `pricing.apply_discount`, `pricing.override_price`, `pricing.approve_low_margin`.
