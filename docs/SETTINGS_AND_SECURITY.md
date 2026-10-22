# Settings persistence & secure credentials

## Root cause of settings / API keys “resetting”

Investigation of Workshop OS (API + WinUI) found these primary causes — not a single frontend-only bug:

### 1. Secrets stored in plaintext inside `business_settings` JSON

POS API keys (`pos.settings.apiKey`) and WooCommerce consumer key/secret (`website.woocommerce`) were saved as ordinary JSON in PostgreSQL. They were never encrypted at rest. Any backup, log dump, or DB export exposed them.

### 2. Incomplete encryption helper + JWT coupling

`SecretProtector` only implemented **Encrypt** (AES-GCM) and had **no Decrypt**. It derived its key from `Encryption:Key` **or fell back to `Jwt:SigningKey`**. Docker Compose did not set a dedicated encryption key. If the JWT signing key changed between deploys (common when `.env` was recreated with a new `JWT_SIGNING_KEY`), anything encrypted with the old key became undecryptable — which looks like credentials “disappearing”.

### 3. Pricing save could rewrite the business profile from defaults

`PricingSettingsService.UpdateAsync` loaded `business.profile` with a **hardcoded fallback** (`Name = "Workshop"`, GST 10%, etc.) when the row was missing or failed to deserialise as non-null. It then wrote that blob back when syncing tax fields — which could overwrite a real business name / ABN with seed defaults.

### 4. No concurrency protection on settings writes

`SetSettingAsync` always overwrote the entire JSON document for a key with last-writer-wins. Concurrent Settings tabs (or POS visibility sync racing with POS save) could discard the other write.

### 5. What was *not* the cause

- Startup seeds (`EnsureFoundationAsync`, catalogue, website defaults) only **insert missing** rows; they do not wipe `business_settings`.
- PostgreSQL and file volumes are defined in Docker Compose (`postgres_data`, `files_data`, `backup_data`).
- WinUI pricing/POS saves already re-fetch before PUT and verify with GET after save.

## Fixes in this release

| Fix | Detail |
| --- | --- |
| `SecretProtector` | Encrypt + Decrypt, key versions (`v1`), dedicated `Encryption:Key`, fail loudly on decrypt failure (never silent wipe) |
| `integration_credentials` | Encrypted credential store; API never returns secret values |
| Migration of Woo/POS secrets | Moved into encrypted credentials; settings JSON retains non-secret config + “configured” flags only |
| Settings concurrency | Optional `expectedUpdatedAt` / conflict response; refuse blind overwrite |
| Pricing → business sync | Only updates GST fields when a real business profile row exists |
| Docker | `Encryption__Key` required in compose / `.env.example` |

## Configuring the master encryption key

Generate a long random secret (32+ characters) and set it **once** for the life of the database:

```bash
# .env next to docker-compose (never commit)
Encryption__Key="$(openssl rand -base64 48)"
JWT_SIGNING_KEY="$(openssl rand -base64 48)"
```

Compose:

```yaml
Encryption__Key: ${Encryption__Key}
```

**Do not** change `Encryption__Key` after credentials have been saved unless you run a planned key-rotation procedure. Changing it without re-encrypting makes existing ciphertexts unreadable.

### Recovery if the key is lost

1. Restore `Encryption__Key` from your secrets backup / password manager.
2. If the key is permanently lost, encrypted credentials cannot be recovered — re-enter each integration key via **Settings → Integrations → API Credentials**. Existing ciphertexts are left untouched (never auto-deleted).

## Using API Credentials

1. Sign in as Owner/Administrator (`integrations.manage` or `settings.manage`).
2. Open **Settings → Integrations**.
3. Select a provider (WooCommerce, POS/Square, OpenAI, Twilio, SMTP, IMEI, Shipping).
4. Enter new secrets (existing values are never shown — only “configured” + last-4 hint).
5. **Test connection** where supported, then **Save**.
6. Use **Disable** to stop using an integration without deleting ciphertext; **Rotate** replaces the secret.

## Using Expenses

1. Open **Expenses** from the main navigation (Finance area).
2. Create expenses with GST treatment (inclusive / exclusive / GST-free / input-taxed / no credit).
3. Link suppliers, repair tickets, or inventory items when relevant.
4. Recurring templates auto-create **draft** expenses on due dates (idempotent).
5. Export CSV for your accountant from the Expenses toolbar.

Reports distinguish operating expenses from inventory/COGS links so stock purchases already tracked in Purchasing are not double-counted when marked as inventory-linked.
