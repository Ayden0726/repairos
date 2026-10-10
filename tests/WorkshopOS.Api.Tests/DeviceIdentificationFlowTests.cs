using System.Net.Http.Json;
using FluentAssertions;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Workshop;
using WorkshopOS.Infrastructure.Services;

namespace WorkshopOS.Api.Tests;

public sealed class ImeiLuhnTests
{
    [Theory]
    [InlineData("356166091795616", true)]
    [InlineData("490154203237518", true)]
    [InlineData("356166091795617", false)]
    [InlineData("123", false)]
    [InlineData("", false)]
    public void Luhn_And_Normalize(string input, bool expectedValid)
    {
        var normalized = DeviceIdentificationService.NormalizeImei(input);
        if (!expectedValid && input.Length != 15)
        {
            normalized.Should().BeNull();
            return;
        }

        normalized.Should().NotBeNull();
        DeviceIdentificationService.IsValidLuhn(normalized!).Should().Be(expectedValid);
        if (expectedValid)
            DeviceIdentificationService.MaskImei(normalized!).Should().NotContain(normalized![4..11]);
    }
}

[Collection("api")]
public sealed class DeviceIdentificationFlowTests
{
    private readonly WorkshopApiFactory _factory;
    private readonly HttpClient _client;

    public DeviceIdentificationFlowTests(WorkshopApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Settings_Validate_Lookup_ManualFallback_And_Duplicates()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var settings = await _client.GetFromJsonAsync<DeviceIdentificationSettingsDto>("/api/device-identification/settings");
        settings.Should().NotBeNull();
        settings!.Enabled.Should().BeTrue();
        settings.ApiKeyConfigured.Should().BeFalse();
        settings.Provider.Should().Be("imei.info");

        var saved = await _client.PutAsJsonAsync("/api/device-identification/settings",
            new UpdateDeviceIdentificationSettingsRequest(
                true, "imei.info", null, false, true, false, 0, null, true, 90, 200, 0.02m, null, "test notes"));
        saved.EnsureSuccessStatusCode();
        var savedDto = await saved.Content.ReadFromJsonAsync<DeviceIdentificationSettingsDto>();
        savedDto!.Notes.Should().Be("test notes");
        savedDto.ApiKeyConfigured.Should().BeFalse();

        var bad = await _client.PostAsJsonAsync("/api/device-identification/validate", new ImeiValidateRequest("12345"));
        bad.EnsureSuccessStatusCode();
        var badDto = await bad.Content.ReadFromJsonAsync<ImeiValidateResultDto>();
        badDto!.IsValid.Should().BeFalse();

        const string validImei = "490154203237518";
        var ok = await _client.PostAsJsonAsync("/api/device-identification/validate", new ImeiValidateRequest(validImei));
        ok.EnsureSuccessStatusCode();
        var okDto = await ok.Content.ReadFromJsonAsync<ImeiValidateResultDto>();
        okDto!.IsValid.Should().BeTrue();
        okDto.Tac.Should().Be("49015420");

        var lookup = await _client.PostAsJsonAsync("/api/device-identification/lookup",
            new ImeiLookupRequest(validImei));
        lookup.EnsureSuccessStatusCode();
        var lookupDto = await lookup.Content.ReadFromJsonAsync<ImeiLookupResultDto>();
        lookupDto!.Success.Should().BeTrue();
        lookupDto.UsedManualFallback.Should().BeTrue();
        lookupDto.ApiConfigured.Should().BeFalse();
        lookupDto.Tac.Should().Be("49015420");
        lookupDto.ImeiMasked.Should().NotBeNullOrWhiteSpace();
        lookupDto.VerificationStatus.Should().Be("Manual");
        // Never echo full IMEI in the response payload fields we surface.
        lookupDto.ImeiMasked.Should().NotBe(validImei);

        var customer = await PostJson<CustomerDetailDto>("/api/customers", new UpsertCustomerRequest(
            null, CustomerType.Individual, "Imei", "Dup", null, "0400 999 111", "imei-dup@example.com",
            null, "Townsville", "QLD", "4810", null, PreferredContact.Sms, false));

        var repair = await PostJson<RepairDetailDto>("/api/repairs", new CreateRepairRequest(
            customer.Id, null, null, null, null,
            "Screen", null, null, null, null, true,
            true, false, false, false, null, null, null, null,
            DeviceCategory.Phone, "Apple", "iPhone 13", null, validImei));
        repair.DeviceId.Should().NotBeNull();

        var dupLookup = await _client.PostAsJsonAsync("/api/device-identification/lookup",
            new ImeiLookupRequest(validImei));
        dupLookup.EnsureSuccessStatusCode();
        var dupDto = await dupLookup.Content.ReadFromJsonAsync<ImeiLookupResultDto>();
        dupDto!.Duplicates.Should().NotBeNull();
        dupDto.Duplicates!.Should().Contain(d => d.Source == "repair" || d.Source == "device");
    }

    [Fact]
    public async Task Advanced_Disabled_Returns_Error_Without_Blocking_Validate()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        await _client.PutAsJsonAsync("/api/device-identification/settings",
            new UpdateDeviceIdentificationSettingsRequest(
                true, "imei.info", null, false, true, false, 0, 12, true, 30, 50, 0.02m, 0.50m, null));

        var advanced = await _client.PostAsJsonAsync("/api/device-identification/lookup",
            new ImeiLookupRequest("490154203237518", Advanced: true));
        advanced.EnsureSuccessStatusCode();
        var dto = await advanced.Content.ReadFromJsonAsync<ImeiLookupResultDto>();
        dto!.Success.Should().BeFalse();
        dto.ErrorCode.Should().Be("advanced_disabled");
    }

    [Fact]
    public async Task ApiKey_Save_Keep_Clear_And_Test()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var withKey = await _client.PutAsJsonAsync("/api/device-identification/settings",
            new UpdateDeviceIdentificationSettingsRequest(
                true, "imei.info", "test-imei-api-key-value", false, true, false, 0, null, true, 90, 200, 0.02m, null, "with key"));
        withKey.EnsureSuccessStatusCode();
        var keyDto = await withKey.Content.ReadFromJsonAsync<DeviceIdentificationSettingsDto>();
        keyDto!.ApiKeyConfigured.Should().BeTrue();

        var keep = await _client.PutAsJsonAsync("/api/device-identification/settings",
            new UpdateDeviceIdentificationSettingsRequest(
                true, "imei.info", null, false, true, false, 0, null, true, 90, 200, 0.02m, null, "kept key"));
        keep.EnsureSuccessStatusCode();
        (await keep.Content.ReadFromJsonAsync<DeviceIdentificationSettingsDto>())!.ApiKeyConfigured.Should().BeTrue();

        var test = await _client.PostAsync("/api/device-identification/test", null);
        test.EnsureSuccessStatusCode();
        var testDto = await test.Content.ReadFromJsonAsync<DeviceIdentificationTestResultDto>();
        testDto!.ApiKeyConfigured.Should().BeTrue();
        testDto.ApiKeyDecryptable.Should().BeTrue();

        var clear = await _client.PutAsJsonAsync("/api/device-identification/settings",
            new UpdateDeviceIdentificationSettingsRequest(
                true, "imei.info", null, true, true, false, 0, null, true, 90, 200, 0.02m, null, null));
        clear.EnsureSuccessStatusCode();
        (await clear.Content.ReadFromJsonAsync<DeviceIdentificationSettingsDto>())!.ApiKeyConfigured.Should().BeFalse();
    }

    private async Task EnsureSetupAndLoginAsync()
    {
        var setup = new SetupRequest(
            "IMEI Shop", null, null, null, null, null, "QLD", null, null, true, 0.1m, "AUD", 110m,
            "Owner", "imei-owner@example.com", "Workshop!2026", "#0F766E");
        (await _client.PostAsJsonAsync("/api/setup", setup)).EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("imei-owner@example.com", "Workshop!2026"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    private async Task<T> PostJson<T>(string url, object body)
    {
        var response = await _client.PostAsJsonAsync(url, body);
        var bodyText = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(bodyText);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
