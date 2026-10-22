using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Application.Common;
using WorkshopOS.Contracts.Finance;
using WorkshopOS.Domain.Entities;
using WorkshopOS.Infrastructure.Persistence;
using WorkshopOS.Infrastructure.Security;

namespace WorkshopOS.Infrastructure.Services;

public sealed class IntegrationCredentialService : IIntegrationCredentialService
{
    public static readonly IntegrationProviderInfoDto[] Catalogue =
    [
        new("woocommerce", "WooCommerce", "WordPress / WooCommerce REST API", ["consumerKey", "consumerSecret"]),
        new("pos", "POS / payments", "Square, Tyro, Stripe Terminal, or other POS provider", ["apiKey"]),
        new("openai", "OpenAI", "Official OpenAI API for listing and AI assist", ["apiKey"]),
        new("twilio", "Twilio SMS", "SMS notifications", ["accountSid", "authToken"]),
        new("smtp", "Email / SMTP", "Outbound email", ["username", "password"]),
        new("imei", "IMEI lookup", "Authorised IMEI / TAC provider", ["apiKey"]),
        new("shipping", "Shipping", "Courier / label provider", ["apiKey"]),
        new("webhook", "Website webhooks", "Outbound webhook signing secret", ["secret"])
    ];

    private readonly WorkshopDbContext _db;
    private readonly SecretProtector _protector;
    private readonly IAuditService _audit;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<IntegrationCredentialService> _log;

    public IntegrationCredentialService(
        WorkshopDbContext db, SecretProtector protector, IAuditService audit,
        IHttpClientFactory http, ILogger<IntegrationCredentialService> log)
    {
        _db = db;
        _protector = protector;
        _audit = audit;
        _http = http;
        _log = log;
    }

    public IReadOnlyList<IntegrationProviderInfoDto> ListProviders() => Catalogue;

    public async Task EnsureMigratedFromLegacySettingsAsync(CancellationToken ct = default)
    {
        await MigrateWooAsync(ct);
        await MigratePosAsync(ct);
        await MigrateWebhookAsync(ct);
    }

    public async Task<IReadOnlyList<IntegrationCredentialDto>> ListAsync(CancellationToken ct = default)
    {
        await EnsureMigratedFromLegacySettingsAsync(ct);
        var rows = await _db.IntegrationCredentials.AsNoTracking()
            .Where(c => c.ArchivedAt == null)
            .OrderBy(c => c.DisplayName)
            .ToListAsync(ct);

        var known = Catalogue.Select(c => c.ProviderKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var list = rows.Select(Map).ToList();
        foreach (var p in Catalogue)
        {
            if (list.All(x => !x.ProviderKey.Equals(p.ProviderKey, StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(new IntegrationCredentialDto(
                    Guid.Empty, p.ProviderKey, p.DisplayName, true, false, null, "NotConfigured",
                    null, null, null, null, DateTimeOffset.MinValue));
            }
        }

        return list.OrderBy(x => x.DisplayName).ToList();
    }

    public async Task<IntegrationCredentialDto> UpsertAsync(
        UpsertIntegrationCredentialRequest request, Guid actorId, CancellationToken ct = default)
    {
        var key = NormalizeKey(request.ProviderKey);
        var display = string.IsNullOrWhiteSpace(request.DisplayName)
            ? Catalogue.FirstOrDefault(c => c.ProviderKey == key)?.DisplayName ?? key
            : request.DisplayName.Trim();

        var row = await _db.IntegrationCredentials
            .FirstOrDefaultAsync(c => c.ProviderKey == key && c.ArchivedAt == null, ct);
        if (row is null)
        {
            row = new IntegrationCredential { ProviderKey = key, DisplayName = display };
            _db.IntegrationCredentials.Add(row);
        }

        row.DisplayName = display;
        row.IsEnabled = request.IsEnabled;
        if (request.PublicConfigJson is not null)
            row.PublicConfigJson = string.IsNullOrWhiteSpace(request.PublicConfigJson) ? null : request.PublicConfigJson.Trim();

        if (request.ClearSecrets)
        {
            row.SecretCipher = null;
            row.SecretHint = null;
            row.ConnectionStatus = row.IsEnabled ? "NotConfigured" : "Disabled";
        }
        else if (!string.IsNullOrWhiteSpace(request.SecretsJson))
        {
            ValidateSecretsJson(request.SecretsJson);
            row.SecretCipher = _protector.Encrypt(request.SecretsJson.Trim());
            row.KeyVersion = _protector.KeyVersion;
            row.SecretHint = BuildHint(request.SecretsJson);
            row.ConnectionStatus = row.IsEnabled ? "Configured" : "Disabled";
            row.LastError = null;
        }

        if (!row.IsEnabled)
            row.ConnectionStatus = "Disabled";
        else if (!row.HasSecret && row.ConnectionStatus != "Configured")
            row.ConnectionStatus = "NotConfigured";

        row.UpdatedById = actorId;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "integrations.credential.upsert", "IntegrationCredential", row.Id.ToString(),
            newValue: new { row.ProviderKey, row.IsEnabled, row.HasSecret, row.ConnectionStatus }, ct: ct);
        return Map(row);
    }

    public async Task<TestIntegrationCredentialResponse> TestAsync(
        string providerKey, Guid actorId, CancellationToken ct = default)
    {
        var key = NormalizeKey(providerKey);
        var row = await _db.IntegrationCredentials
            .FirstOrDefaultAsync(c => c.ProviderKey == key && c.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Integration credential not found. Save a key first.", 404);

        row.LastTestedAt = DateTimeOffset.UtcNow;
        if (!row.HasSecret)
        {
            row.LastError = "No secret configured.";
            row.ConnectionStatus = "Error";
            await _db.SaveChangesAsync(ct);
            return new TestIntegrationCredentialResponse(false, row.LastError, row.LastTestedAt.Value);
        }

        if (!_protector.TryDecrypt(row.SecretCipher, out var secretsJson, out var decryptError))
        {
            row.LastError = decryptError;
            row.ConnectionStatus = "Error";
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync(actorId, "integrations.credential.test_fail", "IntegrationCredential", row.Id.ToString(),
                newValue: new { row.ProviderKey, reason = "decrypt_failed" }, ct: ct);
            return new TestIntegrationCredentialResponse(false, decryptError!, row.LastTestedAt.Value);
        }

        var (ok, message) = await RunTestAsync(key, row.PublicConfigJson, secretsJson!, ct);
        if (ok)
        {
            row.LastSuccessAt = DateTimeOffset.UtcNow;
            row.LastError = null;
            row.ConnectionStatus = "Connected";
        }
        else
        {
            row.LastError = message;
            row.ConnectionStatus = "Error";
        }

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, ok ? "integrations.credential.test_ok" : "integrations.credential.test_fail",
            "IntegrationCredential", row.Id.ToString(), newValue: new { row.ProviderKey, ok }, ct: ct);
        return new TestIntegrationCredentialResponse(ok, message, row.LastTestedAt.Value);
    }

    public async Task<Dictionary<string, string>?> GetSecretsMapAsync(string providerKey, CancellationToken ct = default)
    {
        var key = NormalizeKey(providerKey);
        var row = await _db.IntegrationCredentials.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ProviderKey == key && c.ArchivedAt == null && c.IsEnabled, ct);
        if (row is null || !row.HasSecret) return null;
        if (!_protector.TryDecrypt(row.SecretCipher, out var json, out _))
            return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json!)
                   ?? new Dictionary<string, string>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetPublicConfigAsync(string providerKey, CancellationToken ct = default)
    {
        var key = NormalizeKey(providerKey);
        return await _db.IntegrationCredentials.AsNoTracking()
            .Where(c => c.ProviderKey == key && c.ArchivedAt == null)
            .Select(c => c.PublicConfigJson)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<(bool Ok, string Message)> RunTestAsync(
        string key, string? publicConfig, string secretsJson, CancellationToken ct)
    {
        try
        {
            var secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(secretsJson)
                          ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return key switch
            {
                "woocommerce" => await TestWooAsync(publicConfig, secrets, ct),
                "openai" => await TestOpenAiAsync(secrets, ct),
                "pos" => (secrets.ContainsKey("apiKey") && !string.IsNullOrWhiteSpace(secrets["apiKey"]),
                    secrets.ContainsKey("apiKey") ? "API key present (provider-specific live test not run)." : "apiKey missing."),
                _ => (secrets.Count > 0, secrets.Count > 0
                    ? "Secret stored and decryptable. Provider-specific live test not configured."
                    : "No secret fields found.")
            };
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Integration test failed for {Provider}", key);
            return (false, "Connection test failed. Check configuration (details omitted from logs for security).");
        }
    }

    private async Task<(bool, string)> TestWooAsync(string? publicConfig, Dictionary<string, string> secrets, CancellationToken ct)
    {
        string? storeUrl = null;
        if (!string.IsNullOrWhiteSpace(publicConfig))
        {
            using var doc = JsonDocument.Parse(publicConfig);
            if (doc.RootElement.TryGetProperty("storeUrl", out var u))
                storeUrl = u.GetString();
        }

        if (string.IsNullOrWhiteSpace(storeUrl))
            return (false, "storeUrl is required in public configuration.");
        if (!secrets.TryGetValue("consumerKey", out var ck) || string.IsNullOrWhiteSpace(ck) ||
            !secrets.TryGetValue("consumerSecret", out var cs) || string.IsNullOrWhiteSpace(cs))
            return (false, "consumerKey and consumerSecret are required.");

        var client = _http.CreateClient("woocommerce");
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ck}:{cs}"));
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{storeUrl.TrimEnd('/')}/wp-json/wc/v3/system_status");
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", creds);
        using var res = await client.SendAsync(req, ct);
        if (res.IsSuccessStatusCode)
            return (true, "WooCommerce REST API reachable.");
        // Some stores disable system_status — try products
        using var req2 = new HttpRequestMessage(HttpMethod.Get, $"{storeUrl.TrimEnd('/')}/wp-json/wc/v3/products?per_page=1");
        req2.Headers.Authorization = new AuthenticationHeaderValue("Basic", creds);
        using var res2 = await client.SendAsync(req2, ct);
        return res2.IsSuccessStatusCode
            ? (true, "WooCommerce REST API reachable.")
            : (false, $"WooCommerce returned HTTP {(int)res2.StatusCode}.");
    }

    private async Task<(bool, string)> TestOpenAiAsync(Dictionary<string, string> secrets, CancellationToken ct)
    {
        if (!secrets.TryGetValue("apiKey", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
            return (false, "apiKey is required.");
        var client = _http.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var res = await client.SendAsync(req, ct);
        return res.IsSuccessStatusCode
            ? (true, "OpenAI API key accepted.")
            : (false, $"OpenAI returned HTTP {(int)res.StatusCode}.");
    }

    private async Task MigrateWooAsync(CancellationToken ct)
    {
        if (await _db.IntegrationCredentials.AnyAsync(c => c.ProviderKey == "woocommerce" && c.ArchivedAt == null, ct))
            return;
        var stored = await DbSeed.GetSettingAsync(_db, "website.woocommerce", new WooLegacy(), ct);
        if (string.IsNullOrWhiteSpace(stored.ConsumerKey) && string.IsNullOrWhiteSpace(stored.ConsumerSecret))
            return;

        var secrets = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(stored.ConsumerKey)) secrets["consumerKey"] = stored.ConsumerKey!;
        if (!string.IsNullOrWhiteSpace(stored.ConsumerSecret)) secrets["consumerSecret"] = stored.ConsumerSecret!;
        var publicCfg = JsonSerializer.Serialize(new
        {
            storeUrl = stored.StoreUrl,
            enabled = stored.Enabled,
            defaultWarrantyText = stored.DefaultWarrantyText
        });
        var row = new IntegrationCredential
        {
            ProviderKey = "woocommerce",
            DisplayName = "WooCommerce",
            IsEnabled = stored.Enabled,
            PublicConfigJson = publicCfg,
            SecretCipher = _protector.Encrypt(JsonSerializer.Serialize(secrets)),
            KeyVersion = _protector.KeyVersion,
            SecretHint = BuildHint(JsonSerializer.Serialize(secrets)),
            ConnectionStatus = "Configured"
        };
        _db.IntegrationCredentials.Add(row);

        // Strip secrets from legacy settings blob (keep non-secret flags).
        await DbSeed.SetSettingAsync(_db, "website.woocommerce", new
        {
            stored.Enabled,
            stored.StoreUrl,
            ConsumerKey = (string?)null,
            ConsumerSecret = (string?)null,
            stored.DefaultWarrantyText,
            MigratedToCredentials = true
        }, null, ct);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Migrated WooCommerce secrets into integration_credentials.");
    }

    private async Task MigratePosAsync(CancellationToken ct)
    {
        if (await _db.IntegrationCredentials.AnyAsync(c => c.ProviderKey == "pos" && c.ArchivedAt == null, ct))
            return;
        var row = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == SettingKeys.PosSettings, ct);
        if (row is null || string.IsNullOrWhiteSpace(row.JsonValue)) return;
        using var doc = JsonDocument.Parse(row.JsonValue);
        var root = doc.RootElement;
        var apiKey = root.TryGetProperty("apiKey", out var ak) ? ak.GetString()
            : root.TryGetProperty("ApiKey", out var ak2) ? ak2.GetString() : null;
        if (string.IsNullOrWhiteSpace(apiKey)) return;

        string? Prop(string a, string b) =>
            root.TryGetProperty(a, out var p) ? p.ToString()
            : root.TryGetProperty(b, out var p2) ? p2.ToString() : null;

        var enabled = root.TryGetProperty("enabled", out var en) && en.ValueKind == JsonValueKind.True
                      || root.TryGetProperty("Enabled", out var en2) && en2.ValueKind == JsonValueKind.True;
        var secrets = JsonSerializer.Serialize(new Dictionary<string, string> { ["apiKey"] = apiKey });
        var publicCfg = JsonSerializer.Serialize(new
        {
            enabled,
            provider = Prop("provider", "Provider"),
            merchantId = Prop("merchantId", "MerchantId"),
            terminalId = Prop("terminalId", "TerminalId"),
            providerLocationId = Prop("providerLocationId", "ProviderLocationId")
        });
        _db.IntegrationCredentials.Add(new IntegrationCredential
        {
            ProviderKey = "pos",
            DisplayName = "POS / payments",
            IsEnabled = enabled,
            PublicConfigJson = publicCfg,
            SecretCipher = _protector.Encrypt(secrets),
            KeyVersion = _protector.KeyVersion,
            SecretHint = SecretProtector.MaskSecret(apiKey),
            ConnectionStatus = "Configured"
        });

        // Rewrite settings JSON without apiKey (preserve other fields).
        var map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.JsonValue)
                  ?? new Dictionary<string, JsonElement>();
        map.Remove("apiKey");
        map.Remove("ApiKey");
        map["migratedToCredentials"] = JsonSerializer.SerializeToElement(true);
        await DbSeed.SetSettingAsync(_db, SettingKeys.PosSettings, map, null, ct);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Migrated POS API key into integration_credentials.");
    }

    private async Task MigrateWebhookAsync(CancellationToken ct)
    {
        if (await _db.IntegrationCredentials.AnyAsync(c => c.ProviderKey == "webhook" && c.ArchivedAt == null, ct))
            return;
        var stored = await DbSeed.GetSettingAsync(_db, "website.webhooks", new WebhookLegacy(), ct);
        if (string.IsNullOrWhiteSpace(stored.Secret))
            return;
        var secrets = JsonSerializer.Serialize(new Dictionary<string, string> { ["secret"] = stored.Secret! });
        _db.IntegrationCredentials.Add(new IntegrationCredential
        {
            ProviderKey = "webhook",
            DisplayName = "Website webhooks",
            IsEnabled = stored.Enabled,
            PublicConfigJson = JsonSerializer.Serialize(new { stored.Enabled, stored.Url }),
            SecretCipher = _protector.Encrypt(secrets),
            KeyVersion = _protector.KeyVersion,
            SecretHint = SecretProtector.MaskSecret(stored.Secret),
            ConnectionStatus = "Configured"
        });
        await DbSeed.SetSettingAsync(_db, "website.webhooks", new { stored.Enabled, stored.Url, Secret = (string?)null, MigratedToCredentials = true }, null, ct);
        await _db.SaveChangesAsync(ct);
    }

    private static IntegrationCredentialDto Map(IntegrationCredential c) => new(
        c.Id, c.ProviderKey, c.DisplayName, c.IsEnabled, c.HasSecret, c.SecretHint, c.ConnectionStatus,
        c.PublicConfigJson, c.LastTestedAt, c.LastSuccessAt, c.LastError, c.UpdatedAt);

    private static string NormalizeKey(string key) =>
        string.IsNullOrWhiteSpace(key)
            ? throw new ValidationAppException("Provider key is required.")
            : key.Trim().ToLowerInvariant();

    private static void ValidateSecretsJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new ValidationAppException("Secrets must be a JSON object of field → value.");
        }
        catch (JsonException)
        {
            throw new ValidationAppException("Secrets JSON is invalid.");
        }
    }

    private static string BuildHint(string secretsJson)
    {
        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(secretsJson);
            var first = map?.Values.FirstOrDefault(v => !string.IsNullOrEmpty(v));
            return SecretProtector.MaskSecret(first);
        }
        catch
        {
            return "••••";
        }
    }

    private sealed class WooLegacy
    {
        public bool Enabled { get; set; }
        public string? StoreUrl { get; set; }
        public string? ConsumerKey { get; set; }
        public string? ConsumerSecret { get; set; }
        public string? DefaultWarrantyText { get; set; }
    }

    private sealed class WebhookLegacy
    {
        public bool Enabled { get; set; }
        public string? Url { get; set; }
        public string? Secret { get; set; }
    }
}
