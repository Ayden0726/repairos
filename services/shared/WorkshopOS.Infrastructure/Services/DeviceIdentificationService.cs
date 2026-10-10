using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Application.Common;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Domain.Entities;
using WorkshopOS.Infrastructure.Persistence;

namespace WorkshopOS.Infrastructure.Services;

public sealed class DeviceIdentificationService : IDeviceIdentificationService
{
    public const string ProviderImeiInfo = "imei.info";
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;
    private readonly SecretProtector _secrets;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<DeviceIdentificationService> _log;

    public DeviceIdentificationService(
        WorkshopDbContext db,
        IAuditService audit,
        SecretProtector secrets,
        IHttpClientFactory httpFactory,
        ILogger<DeviceIdentificationService> log)
    {
        _db = db;
        _audit = audit;
        _secrets = secrets;
        _httpFactory = httpFactory;
        _log = log;
    }

    public async Task<DeviceIdentificationSettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var stored = await LoadStoredAsync(ct);
        var lookupsToday = 0;
        try
        {
            var today = DateTimeOffset.UtcNow.Date;
            lookupsToday = await _db.ImeiLookupLogs.CountAsync(l => l.CreatedAt >= today, ct);
        }
        catch (Exception ex)
        {
            // Settings must still load if lookup-log table is mid-migration.
            _log.LogWarning(ex, "Could not count IMEI lookups today");
        }
        return ToDto(stored, lookupsToday);
    }

    public async Task<DeviceIdentificationSettingsDto> UpdateSettingsAsync(
        UpdateDeviceIdentificationSettingsRequest request, Guid actorId, CancellationToken ct = default)
    {
        var existing = await LoadStoredAsync(ct);
        var apiKeyEnc = existing.ApiKeyEncrypted;
        if (request.ClearApiKey)
            apiKeyEnc = null;
        else if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            try
            {
                apiKeyEnc = _secrets.Encrypt(request.ApiKey.Trim());
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to encrypt IMEI API key");
                throw new ValidationAppException("Could not encrypt the API key on the server. Check Jwt:SigningKey is set (32+ chars).");
            }
        }

        var provider = string.IsNullOrWhiteSpace(request.Provider)
            ? ProviderImeiInfo
            : request.Provider.Trim();
        if (!string.Equals(provider, ProviderImeiInfo, StringComparison.OrdinalIgnoreCase))
            throw new ValidationAppException("Only the imei.info provider is supported currently.");

        var stored = new StoredSettings
        {
            Enabled = request.Enabled,
            Provider = ProviderImeiInfo,
            ApiKeyEncrypted = apiKeyEnc,
            BasicLookupEnabled = request.BasicLookupEnabled,
            AdvancedLookupEnabled = request.AdvancedLookupEnabled,
            BasicServiceId = request.BasicServiceId < 0 ? 0 : request.BasicServiceId,
            AdvancedServiceId = request.AdvancedServiceId is > 0 ? request.AdvancedServiceId : null,
            CacheEnabled = request.CacheEnabled,
            CacheTtlDays = Math.Clamp(request.CacheTtlDays <= 0 ? 90 : request.CacheTtlDays, 1, 3650),
            DailyLookupLimit = Math.Clamp(request.DailyLookupLimit, 0, 100_000),
            EstimatedBasicCost = request.EstimatedBasicCost,
            EstimatedAdvancedCost = request.EstimatedAdvancedCost,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        await DbSeed.SetSettingAsync(_db, SettingKeys.DeviceIdentification, stored, actorId, ct);

        // Re-read from DB so we never report "saved" unless jsonb round-trips.
        var verified = await LoadStoredAsync(ct);
        if (!string.IsNullOrWhiteSpace(apiKeyEnc) &&
            string.IsNullOrWhiteSpace(verified.ApiKeyEncrypted))
            throw new AppException("save_failed", "API key did not persist. Re-save Device ID settings.", 500);

        await _audit.WriteAsync(actorId, "settings.device_identification.update", "Setting",
            SettingKeys.DeviceIdentification,
            newValue: new
            {
                stored.Enabled,
                stored.Provider,
                ApiKeyConfigured = !string.IsNullOrWhiteSpace(verified.ApiKeyEncrypted),
                ApiKeyUpdated = !string.IsNullOrWhiteSpace(request.ApiKey) && !request.ClearApiKey,
                stored.BasicLookupEnabled,
                stored.AdvancedLookupEnabled,
                stored.BasicServiceId,
                stored.AdvancedServiceId,
                stored.CacheEnabled,
                stored.CacheTtlDays,
                stored.DailyLookupLimit
            },
            ct: ct);

        return await GetSettingsAsync(ct);
    }

    public async Task<DeviceIdentificationTestResultDto> TestConnectionAsync(CancellationToken ct = default)
    {
        var stored = await LoadStoredAsync(ct);
        var configured = !string.IsNullOrWhiteSpace(stored.ApiKeyEncrypted);
        if (!configured)
            return new DeviceIdentificationTestResultDto(false, "No API key stored. Paste your IMEI.info key and Save first.", false, false, null);

        var apiKey = TryDecryptKey(stored.ApiKeyEncrypted);
        if (string.IsNullOrWhiteSpace(apiKey))
            return new DeviceIdentificationTestResultDto(false,
                "API key is stored but could not be decrypted. Re-paste the key and Save again (server encryption key may have changed).",
                true, false, null);

        try
        {
            var client = _httpFactory.CreateClient("imei.info");
            var url = $"https://dash.imei.info/api/account/account/?API_KEY={Uri.EscapeDataString(apiKey)}";
            using var response = await client.GetAsync(url, ct);
            var code = (int)response.StatusCode;
            if (response.IsSuccessStatusCode)
                return new DeviceIdentificationTestResultDto(true,
                    "Connected to IMEI.info — API key works.", true, true, code);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new DeviceIdentificationTestResultDto(false,
                    "IMEI.info rejected the API key (401/403). Check the key in your IMEI.info dashboard.", true, true, code);
            return new DeviceIdentificationTestResultDto(false,
                $"IMEI.info returned HTTP {code}. Key is stored; provider may be unavailable.", true, true, code);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "IMEI.info account test failed");
            return new DeviceIdentificationTestResultDto(false,
                "Could not reach IMEI.info from the server (network/DNS/firewall). Key is stored; outbound HTTPS to dash.imei.info must be allowed.",
                true, true, null);
        }
    }

    public async Task<ImeiValidateResultDto> ValidateAsync(ImeiValidateRequest request, CancellationToken ct = default)
    {
        var normalized = NormalizeImei(request.Imei);
        if (normalized is null)
            return new ImeiValidateResultDto(false, null, null, "IMEI must be 15 digits.", null);
        if (!IsValidLuhn(normalized))
            return new ImeiValidateResultDto(false, normalized, normalized[..8], "IMEI failed Luhn check.", null);

        var duplicates = await FindDuplicatesAsync(normalized, ct);
        return new ImeiValidateResultDto(true, normalized, normalized[..8], "IMEI is valid.", duplicates);
    }

    public async Task<ImeiLookupResultDto> LookupAsync(ImeiLookupRequest request, Guid actorId, CancellationToken ct = default)
    {
        var settings = await LoadStoredAsync(ct);
        var normalized = NormalizeImei(request.Imei);
        if (normalized is null)
            return Fail("invalid_imei", "IMEI must be 15 digits.", settings, usedManual: true);
        if (!IsValidLuhn(normalized))
            return Fail("luhn_failed", "IMEI failed Luhn check. Correct the number or enter the device manually.", settings, usedManual: true, imei: normalized);

        var tac = normalized[..8];
        var duplicates = await FindDuplicatesAsync(normalized, ct);
        var masked = MaskImei(normalized);

        if (!settings.Enabled)
        {
            var local = await TryLocalCatalogueMatchAsync(null, null, tac, ct);
            await LogLookupAsync(normalized, tac, settings.Provider, null, request.Advanced, false, true, null, "Manual", null, actorId, ct);
            return ManualFallback(settings, masked, tac, duplicates, local,
                "Device identification is disabled. Enter brand/model manually or enable it in Settings → Device ID.");
        }

        if (request.Advanced && !settings.AdvancedLookupEnabled)
            return Fail("advanced_disabled", "Advanced lookup is disabled in Device Identification settings.", settings, imei: normalized, tac: tac, duplicates: duplicates);

        if (!request.Advanced && !settings.BasicLookupEnabled)
            return Fail("basic_disabled", "Basic lookup is disabled in Device Identification settings.", settings, imei: normalized, tac: tac, duplicates: duplicates);

        if (settings.DailyLookupLimit > 0)
        {
            var today = DateTimeOffset.UtcNow.Date;
            var count = await _db.ImeiLookupLogs.CountAsync(l => l.CreatedAt >= today && !l.FromCache, ct);
            if (count >= settings.DailyLookupLimit)
            {
                var cachedUnderLimit = settings.CacheEnabled
                    ? await GetCacheAsync(tac, settings, ct)
                    : null;
                if (cachedUnderLimit is not null)
                    return await BuildFromCacheAsync(cachedUnderLimit, settings, masked, tac, duplicates, actorId, normalized, ct);

                return Fail("rate_limit", $"Daily lookup limit ({settings.DailyLookupLimit}) reached. Use manual entry or try again tomorrow.",
                    settings, imei: normalized, tac: tac, duplicates: duplicates, usedManual: true);
            }
        }

        if (settings.CacheEnabled && !request.ForceRefresh && !request.Advanced)
        {
            var cached = await GetCacheAsync(tac, settings, ct);
            if (cached is not null)
                return await BuildFromCacheAsync(cached, settings, masked, tac, duplicates, actorId, normalized, ct);
        }

        var apiKey = TryDecryptKey(settings.ApiKeyEncrypted);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var local = await TryLocalCatalogueMatchAsync(null, null, tac, ct);
            var noKey = string.IsNullOrWhiteSpace(settings.ApiKeyEncrypted);
            await LogLookupAsync(normalized, tac, settings.Provider, null, request.Advanced, false, true,
                noKey ? "api_not_configured" : "api_key_decrypt_failed", "Manual", null, actorId, ct);
            return ManualFallback(settings, masked, tac, duplicates, local,
                noKey
                    ? "IMEI.info API key is not configured. Device intake continues with manual brand/model entry. Add the key in Settings → Device ID."
                    : "IMEI API key is stored but could not be decrypted. Re-paste the key in Settings → Device ID and Save.");
        }

        var serviceId = request.Advanced
            ? (settings.AdvancedServiceId ?? settings.BasicServiceId)
            : settings.BasicServiceId;

        try
        {
            var apiResult = await CallImeiInfoAsync(apiKey, serviceId, normalized, ct);
            if (!apiResult.Success)
            {
                var local = await TryLocalCatalogueMatchAsync(null, null, tac, ct);
                await LogLookupAsync(normalized, tac, settings.Provider, serviceId, request.Advanced, false, false, apiResult.ErrorCode, "Unverified", null, actorId, ct);
                return ManualFallback(settings, masked, tac, duplicates, local,
                    apiResult.Message + " You can still enter the device manually.",
                    errorCode: apiResult.ErrorCode);
            }

            var mapped = MapApiFields(apiResult.Fields ?? new Dictionary<string, string>());
            var verification = request.Advanced && HasDeviceSpecific(mapped)
                ? "DeviceVerified"
                : "ModelLevel";

            if (settings.CacheEnabled && !request.Advanced)
                await UpsertCacheAsync(tac, settings, mapped, verification, apiResult.Fields, ct);

            var catalogue = await TryLocalCatalogueMatchAsync(mapped.Brand, mapped.Model, tac, ct);
            var cost = apiResult.Cost ?? (request.Advanced ? settings.EstimatedAdvancedCost : settings.EstimatedBasicCost);

            await LogLookupAsync(normalized, tac, settings.Provider, serviceId, request.Advanced, false, true, null, verification, cost, actorId, ct);

            return new ImeiLookupResultDto(
                true, null,
                request.Advanced
                    ? "Advanced lookup completed. Review device-specific fields before saving."
                    : "Lookup completed. Specs below are model-level unless marked device-specific — review before saving.",
                masked, tac,
                mapped.Brand, mapped.Manufacturer, mapped.Model, mapped.ModelNumber, mapped.DeviceCategory,
                mapped.Display, mapped.Processor, mapped.Chipset, mapped.BatteryCapacity, mapped.Network,
                mapped.Storage, mapped.Ram, mapped.Colour, mapped.Warranty,
                mapped.StorageIsDeviceSpecific, mapped.ColourIsDeviceSpecific, mapped.WarrantyIsDeviceSpecific,
                verification, settings.Provider, DateTimeOffset.UtcNow, false, true, false, cost,
                catalogue.BrandId, catalogue.ModelId, catalogue.ModelName, catalogue.Matches, duplicates,
                apiResult.Fields);
        }
        catch (TaskCanceledException)
        {
            _log.LogWarning("IMEI lookup timed out for TAC {Tac}", tac);
            await LogLookupAsync(normalized, tac, settings.Provider, serviceId, request.Advanced, false, false, "timeout", "Unverified", null, actorId, ct);
            return ManualFallback(settings, masked, tac, duplicates, await TryLocalCatalogueMatchAsync(null, null, tac, ct),
                "Lookup timed out. Enter the device manually — intake is not blocked.",
                errorCode: "timeout");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "IMEI lookup failed for TAC {Tac}", tac);
            await LogLookupAsync(normalized, tac, settings.Provider, serviceId, request.Advanced, false, false, "api_error", "Unverified", null, actorId, ct);
            return ManualFallback(settings, masked, tac, duplicates, await TryLocalCatalogueMatchAsync(null, null, tac, ct),
                "Lookup provider unavailable. Enter the device manually — intake is not blocked.",
                errorCode: "api_error");
        }
    }

    private async Task<ImeiLookupResultDto> BuildFromCacheAsync(
        ImeiTacCache cached, StoredSettings settings, string masked, string tac,
        IReadOnlyList<ImeiDuplicateHitDto> duplicates, Guid actorId, string imei, CancellationToken ct)
    {
        cached.HitCount += 1;
        await _db.SaveChangesAsync(ct);
        var catalogue = await TryLocalCatalogueMatchAsync(cached.Brand, cached.Model, tac, ct);
        await LogLookupAsync(imei, tac, settings.Provider, null, false, true, true, null, cached.VerificationStatus, 0m, actorId, ct);

        IReadOnlyDictionary<string, string>? raw = null;
        if (!string.IsNullOrWhiteSpace(cached.SpecsJson))
        {
            try
            {
                raw = JsonSerializer.Deserialize<Dictionary<string, string>>(cached.SpecsJson);
            }
            catch { /* ignore */ }
        }

        return new ImeiLookupResultDto(
            true, null, "Loaded from TAC cache (model-level). Review before saving — storage/colour/lock are not assumed.",
            masked, tac,
            cached.Brand, cached.Manufacturer, cached.Model, cached.ModelNumber, cached.DeviceCategory,
            cached.Display, cached.Processor, cached.Chipset, cached.BatteryCapacity, cached.Network,
            cached.StorageHint, cached.RamHint, null, null,
            false, false, false,
            cached.VerificationStatus, settings.Provider, cached.LookedUpAt, true, true, false, 0m,
            catalogue.BrandId, catalogue.ModelId, catalogue.ModelName, catalogue.Matches, duplicates, raw);
    }

    private async Task<ImeiTacCache?> GetCacheAsync(string tac, StoredSettings settings, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return await _db.ImeiTacCaches.FirstOrDefaultAsync(
            c => c.Tac == tac && c.Provider == settings.Provider && c.ExpiresAt > now, ct);
    }

    private async Task UpsertCacheAsync(
        string tac, StoredSettings settings, MappedFields mapped, string verification,
        IReadOnlyDictionary<string, string>? raw, CancellationToken ct)
    {
        var row = await _db.ImeiTacCaches.FirstOrDefaultAsync(c => c.Tac == tac && c.Provider == settings.Provider, ct);
        var expires = DateTimeOffset.UtcNow.AddDays(settings.CacheTtlDays);
        if (row is null)
        {
            row = new ImeiTacCache { Tac = tac, Provider = settings.Provider };
            _db.ImeiTacCaches.Add(row);
        }

        row.Brand = mapped.Brand;
        row.Manufacturer = mapped.Manufacturer;
        row.Model = mapped.Model;
        row.ModelNumber = mapped.ModelNumber;
        row.DeviceCategory = mapped.DeviceCategory;
        row.Display = mapped.Display;
        row.Processor = mapped.Processor;
        row.Chipset = mapped.Chipset;
        row.BatteryCapacity = mapped.BatteryCapacity;
        row.Network = mapped.Network;
        // Never cache device-specific exact storage/colour as model truth.
        row.StorageHint = mapped.StorageIsDeviceSpecific ? null : mapped.Storage;
        row.RamHint = mapped.Ram;
        row.SpecsJson = raw is null ? null : JsonSerializer.Serialize(raw);
        row.VerificationStatus = verification;
        row.LookedUpAt = DateTimeOffset.UtcNow;
        row.ExpiresAt = expires;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<ApiCallResult> CallImeiInfoAsync(string apiKey, int serviceId, string imei, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("imei.info");
        var url = $"https://dash.imei.info/api/check/{serviceId}/?API_KEY={Uri.EscapeDataString(apiKey)}&imei={Uri.EscapeDataString(imei)}";
        using var response = await client.GetAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return new ApiCallResult(false, "rate_limit", "IMEI.info rate limit reached.", null, null);

        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            return new ApiCallResult(false, "auth_failed", "IMEI.info rejected the API key.", null, null);

        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            // Async check — poll search_history briefly without blocking intake for long.
            var pending = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts, ct);
            if (TryGetProp(pending, "id", out var idEl) && idEl.ValueKind is JsonValueKind.Number or JsonValueKind.String)
            {
                var id = idEl.ToString();
                for (var i = 0; i < 3; i++)
                {
                    await Task.Delay(800, ct);
                    var histUrl = $"https://dash.imei.info/api/search_history/{Uri.EscapeDataString(id)}/?API_KEY={Uri.EscapeDataString(apiKey)}";
                    using var hist = await client.GetAsync(histUrl, ct);
                    if (!hist.IsSuccessStatusCode) continue;
                    var histBody = await hist.Content.ReadFromJsonAsync<JsonElement>(JsonOpts, ct);
                    var fields = FlattenResult(histBody);
                    if (fields.Count > 0)
                        return new ApiCallResult(true, null, "ok", fields, ExtractCost(histBody));
                }
            }
            return new ApiCallResult(false, "pending", "Advanced check is still processing. Retry shortly or enter manually.", null, null);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _log.LogWarning("IMEI.info HTTP {Status} for service {Service} (body length {Len})",
                (int)response.StatusCode, serviceId, body.Length);
            return new ApiCallResult(false, "api_error", $"IMEI.info returned {(int)response.StatusCode}.", null, null);
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts, ct);
        var mappedFields = FlattenResult(json);
        if (mappedFields.Count == 0)
            return new ApiCallResult(false, "empty", "IMEI.info returned no device fields.", null, ExtractCost(json));

        return new ApiCallResult(true, null, "ok", mappedFields, ExtractCost(json));
    }

    private static decimal? ExtractCost(JsonElement json)
    {
        foreach (var key in new[] { "token_request_price", "price", "cost" })
        {
            if (TryGetProp(json, key, out var el))
            {
                if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var d)) return d;
                if (el.ValueKind == JsonValueKind.String &&
                    decimal.TryParse(el.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var p))
                    return p;
            }
        }
        return null;
    }

    private static Dictionary<string, string> FlattenResult(JsonElement json)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Walk(JsonElement el, string prefix)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in el.EnumerateObject())
                    {
                        var name = string.IsNullOrEmpty(prefix) ? prop.Name : prefix + "." + prop.Name;
                        if (prop.Name.Equals("result", StringComparison.OrdinalIgnoreCase) ||
                            prop.Name.Equals("response", StringComparison.OrdinalIgnoreCase) ||
                            prop.Name.Equals("data", StringComparison.OrdinalIgnoreCase) ||
                            prop.Name.Equals("device", StringComparison.OrdinalIgnoreCase))
                            Walk(prop.Value, prefix);
                        else
                            Walk(prop.Value, name);
                    }
                    break;
                case JsonValueKind.Array:
                    break;
                case JsonValueKind.String:
                    var s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s) && !string.IsNullOrEmpty(prefix))
                        dict[prefix] = s.Trim();
                    break;
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (!string.IsNullOrEmpty(prefix))
                        dict[prefix] = el.ToString();
                    break;
            }
        }
        Walk(json, "");
        // Prefer short keys without path noise for known fields.
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in dict)
        {
            var shortKey = k.Contains('.') ? k[(k.LastIndexOf('.') + 1)..] : k;
            if (!aliases.ContainsKey(shortKey))
                aliases[shortKey] = v;
            aliases[k] = v;
        }
        return aliases;
    }

    private static MappedFields MapApiFields(IReadOnlyDictionary<string, string> fields)
    {
        string? Pick(params string[] keys)
        {
            foreach (var key in keys)
            {
                if (fields.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
                    return v.Trim();
            }
            return null;
        }

        var brand = Pick("Brand", "brand", "Manufacturer", "Make");
        var manufacturer = Pick("Manufacturer", "manufacturer", "Brand");
        var model = Pick("Model", "model", "Model Name", "Device Name", "Name");
        var modelNumber = Pick("Model Number", "ModelNumber", "model_number", "Code Name TAC", "Marketing Name");
        var category = Pick("Type", "Device Type", "Category", "device_type") ?? "Phone";
        var display = Pick("Display", "Screen", "Display Size", "Resolution");
        var processor = Pick("CPU", "Processor", "Chipset CPU");
        var chipset = Pick("Chipset", "SoC");
        var battery = Pick("Battery", "Battery Capacity", "Battery (mAh)");
        var network = Pick("Network", "Bands", "Connectivity", "2G bands", "3G bands", "4G bands", "5G bands");
        var storage = Pick("Storage", "Internal Memory", "Memory", "Capacity");
        var ram = Pick("RAM", "Ram");
        var colour = Pick("Color", "Colour", "color");
        var warranty = Pick("Warranty", "Warranty Status", "Apple Care", "Coverage");

        // Exact colour/storage/warranty from basic TAC checks are usually model-range hints, not this unit.
        var colourDeviceSpecific = fields.Keys.Any(k =>
            k.Contains("color", StringComparison.OrdinalIgnoreCase) &&
            (k.Contains("fsmi", StringComparison.OrdinalIgnoreCase) ||
             k.Contains("gsx", StringComparison.OrdinalIgnoreCase) ||
             k.Contains("serial", StringComparison.OrdinalIgnoreCase) ||
             k.Contains("device", StringComparison.OrdinalIgnoreCase)));
        var storageDeviceSpecific = fields.Keys.Any(k =>
            k.Contains("storage", StringComparison.OrdinalIgnoreCase) &&
            (k.Contains("fsmi", StringComparison.OrdinalIgnoreCase) ||
             k.Contains("gsx", StringComparison.OrdinalIgnoreCase) ||
             k.Contains("serial", StringComparison.OrdinalIgnoreCase)));
        var warrantyDeviceSpecific = !string.IsNullOrWhiteSpace(warranty);

        return new MappedFields(
            brand, manufacturer, model, modelNumber, category, display, processor, chipset, battery, network,
            storage, ram, colourDeviceSpecific ? colour : null, warrantyDeviceSpecific ? warranty : null,
            storageDeviceSpecific, colourDeviceSpecific, warrantyDeviceSpecific);
    }

    private static bool HasDeviceSpecific(MappedFields m) =>
        m.StorageIsDeviceSpecific || m.ColourIsDeviceSpecific || m.WarrantyIsDeviceSpecific;

    private async Task<(Guid? BrandId, Guid? ModelId, string? ModelName, IReadOnlyList<DeviceModelDto>? Matches)> TryLocalCatalogueMatchAsync(
        string? brand, string? model, string tac, CancellationToken ct)
    {
        var q = string.Join(' ', new[] { brand, model }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        if (string.IsNullOrWhiteSpace(q))
        {
            // TAC-only: see if cache already linked a catalogue model via prior brand/model text — nothing else.
            return (null, null, null, null);
        }

        var brandTerm = brand?.Trim().ToLowerInvariant();
        var modelTerm = model?.Trim().ToLowerInvariant();
        var qLower = q.ToLowerInvariant();

        var matches = await _db.DeviceModels.AsNoTracking()
            .Include(m => m.Brand)
            .Where(m => m.ArchivedAt == null && m.Brand.ArchivedAt == null)
            .Where(m =>
                m.Name.ToLower().Contains(qLower) ||
                (brandTerm != null && m.Brand.Name.ToLower().Contains(brandTerm) &&
                 (modelTerm == null ||
                  m.Name.ToLower().Contains(modelTerm) ||
                  (m.Aliases != null && m.Aliases.ToLower().Contains(modelTerm)) ||
                  (m.Series != null && m.Series.ToLower().Contains(modelTerm)))))
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Name)
            .Take(12)
            .Select(m => new DeviceModelDto(
                m.Id, m.BrandId, m.Brand.Name, m.Name, m.DeviceType, m.SortOrder,
                m.Series,
                string.IsNullOrWhiteSpace(m.Aliases)
                    ? null
                    : m.Aliases.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                m.ReleaseYear, m.IsVerified))
            .ToListAsync(ct);

        if (matches.Count == 0 && !string.IsNullOrWhiteSpace(model))
        {
            matches = await _db.DeviceModels.AsNoTracking()
                .Include(m => m.Brand)
                .Where(m => m.ArchivedAt == null)
                .Where(m => m.Name.ToLower().Contains(model.ToLower()) ||
                            (m.Aliases != null && m.Aliases.ToLower().Contains(model.ToLower())))
                .OrderBy(m => m.SortOrder)
                .Take(12)
                .Select(m => new DeviceModelDto(
                    m.Id, m.BrandId, m.Brand.Name, m.Name, m.DeviceType, m.SortOrder,
                    m.Series,
                    string.IsNullOrWhiteSpace(m.Aliases)
                        ? null
                        : m.Aliases.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                    m.ReleaseYear, m.IsVerified))
                .ToListAsync(ct);
        }

        var best = matches.FirstOrDefault();
        return (best?.BrandId, best?.Id, best?.Name, matches.Count == 0 ? null : matches);
    }

    private async Task<IReadOnlyList<ImeiDuplicateHitDto>> FindDuplicatesAsync(string imei, CancellationToken ct)
    {
        var hits = new List<ImeiDuplicateHitDto>();
        var devices = await _db.Devices.AsNoTracking()
            .Include(d => d.Customer)
            .Where(d => d.ArchivedAt == null && d.Imei == imei)
            .OrderByDescending(d => d.CreatedAt)
            .Take(10)
            .ToListAsync(ct);
        foreach (var d in devices)
        {
            hits.Add(new ImeiDuplicateHitDto(
                "device", d.Id,
                $"{d.Brand} {d.Model} · {d.Customer.DisplayName}",
                null, d.CreatedAt));
        }

        var tickets = await _db.RepairTickets.AsNoTracking()
            .Include(r => r.Device)
            .Include(r => r.Customer)
            .Where(r => r.ArchivedAt == null && r.Device != null && r.Device.Imei == imei)
            .OrderByDescending(r => r.CreatedAt)
            .Take(10)
            .ToListAsync(ct);
        foreach (var r in tickets)
        {
            hits.Add(new ImeiDuplicateHitDto(
                "repair", r.Id,
                $"{r.Customer.DisplayName} · {(r.Device == null ? "device" : r.Device.Brand + " " + r.Device.Model)}",
                r.TicketNumber, r.CreatedAt));
        }

        var used = await _db.UsedDevices.AsNoTracking()
            .Where(u => u.ArchivedAt == null && u.Imei == imei)
            .OrderByDescending(u => u.CreatedAt)
            .Take(10)
            .ToListAsync(ct);
        foreach (var u in used)
        {
            hits.Add(new ImeiDuplicateHitDto(
                "inventory", u.Id,
                $"{u.Brand} {u.Model} (refurbished/used)",
                null, u.CreatedAt));
        }

        return hits;
    }

    private async Task LogLookupAsync(
        string imei, string? tac, string provider, int? serviceId, bool advanced, bool fromCache,
        bool success, string? errorCode, string verification, decimal? cost, Guid actorId, CancellationToken ct)
    {
        _db.ImeiLookupLogs.Add(new ImeiLookupLog
        {
            ImeiHash = HashImei(imei),
            Tac = tac,
            Provider = provider,
            ServiceId = serviceId,
            Advanced = advanced,
            FromCache = fromCache,
            Success = success,
            ErrorCode = errorCode,
            VerificationStatus = verification,
            Cost = cost,
            ActorUserId = actorId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    private ImeiLookupResultDto ManualFallback(
        StoredSettings settings, string? masked, string? tac,
        IReadOnlyList<ImeiDuplicateHitDto>? duplicates,
        (Guid? BrandId, Guid? ModelId, string? ModelName, IReadOnlyList<DeviceModelDto>? Matches) local,
        string message, string? errorCode = null) =>
        new(true, errorCode, message, masked, tac,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            false, false, false, "Manual", settings.Provider, DateTimeOffset.UtcNow, false,
            !string.IsNullOrWhiteSpace(settings.ApiKeyEncrypted), true, null,
            local.BrandId, local.ModelId, local.ModelName, local.Matches, duplicates, null);

    private ImeiLookupResultDto Fail(
        string code, string message, StoredSettings settings,
        bool usedManual = false, string? imei = null, string? tac = null,
        IReadOnlyList<ImeiDuplicateHitDto>? duplicates = null) =>
        new(false, code, message,
            imei is null ? null : MaskImei(imei), tac,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            false, false, false, "Unverified", settings.Provider, DateTimeOffset.UtcNow, false,
            !string.IsNullOrWhiteSpace(settings.ApiKeyEncrypted), usedManual, null,
            null, null, null, null, duplicates, null);

    private string? TryDecryptKey(string? enc)
    {
        if (string.IsNullOrWhiteSpace(enc)) return null;
        try { return _secrets.Decrypt(enc); }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not decrypt IMEI API key");
            return null;
        }
    }

    private async Task<StoredSettings> LoadStoredAsync(CancellationToken ct) =>
        await DbSeed.GetSettingAsync(_db, SettingKeys.DeviceIdentification, new StoredSettings(), ct);

    private static DeviceIdentificationSettingsDto ToDto(StoredSettings s, int lookupsToday) =>
        new(s.Enabled, s.Provider, !string.IsNullOrWhiteSpace(s.ApiKeyEncrypted),
            s.BasicLookupEnabled, s.AdvancedLookupEnabled, s.BasicServiceId, s.AdvancedServiceId,
            s.CacheEnabled, s.CacheTtlDays, s.DailyLookupLimit, lookupsToday,
            s.EstimatedBasicCost, s.EstimatedAdvancedCost, s.Notes);

    public static string? NormalizeImei(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return digits.Length == 15 ? digits : null;
    }

    public static bool IsValidLuhn(string digits)
    {
        if (digits.Length != 15 || !digits.All(char.IsDigit)) return false;
        var sum = 0;
        // IMEI Luhn: from right, double every second digit.
        for (var i = 0; i < 15; i++)
        {
            var n = digits[14 - i] - '0';
            if (i % 2 == 1)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
        }
        return sum % 10 == 0;
    }

    public static string MaskImei(string imei) =>
        imei.Length == 15 ? imei[..4] + "*******" + imei[^4..] : "***************";

    public static string HashImei(string imei)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(imei));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class StoredSettings
    {
        public bool Enabled { get; set; } = true;
        public string Provider { get; set; } = ProviderImeiInfo;
        public string? ApiKeyEncrypted { get; set; }
        public bool BasicLookupEnabled { get; set; } = true;
        public bool AdvancedLookupEnabled { get; set; }
        public int BasicServiceId { get; set; }
        public int? AdvancedServiceId { get; set; }
        public bool CacheEnabled { get; set; } = true;
        public int CacheTtlDays { get; set; } = 90;
        public int DailyLookupLimit { get; set; } = 200;
        public decimal? EstimatedBasicCost { get; set; } = 0.02m;
        public decimal? EstimatedAdvancedCost { get; set; }
        public string? Notes { get; set; }
    }

    private sealed record MappedFields(
        string? Brand, string? Manufacturer, string? Model, string? ModelNumber, string? DeviceCategory,
        string? Display, string? Processor, string? Chipset, string? BatteryCapacity, string? Network,
        string? Storage, string? Ram, string? Colour, string? Warranty,
        bool StorageIsDeviceSpecific, bool ColourIsDeviceSpecific, bool WarrantyIsDeviceSpecific);

    private sealed record ApiCallResult(
        bool Success, string? ErrorCode, string Message,
        IReadOnlyDictionary<string, string>? Fields, decimal? Cost);

    private static bool TryGetProp(JsonElement el, string name, out JsonElement value)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out value))
            return true;
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in el.EnumerateObject())
            {
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = p.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }
}
