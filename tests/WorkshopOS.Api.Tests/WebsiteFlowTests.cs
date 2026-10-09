using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Common;
using WorkshopOS.Contracts.Website;

namespace WorkshopOS.Api.Tests;

[Collection("api")]
public sealed class WebsiteFlowTests
{
    private readonly WorkshopApiFactory _factory;
    private readonly HttpClient _client;

    public WebsiteFlowTests(WorkshopApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Public_Quote_Status_Portal_And_SellTech()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var keyResponse = await _client.PostAsJsonAsync("/api/website/api-keys",
            new CreateWebsiteApiKeyRequest("WordPress Test", "https://example.com"));
        keyResponse.EnsureSuccessStatusCode();
        var key = await keyResponse.Content.ReadFromJsonAsync<CreateWebsiteApiKeyResponse>();
        key!.ApiKey.Should().StartWith("wos_");

        using var publicClient = _factory.CreateClient();
        publicClient.DefaultRequestHeaders.Add("X-WorkshopOS-Key", key.ApiKey);

        var unauthorized = await _factory.CreateClient().PostAsJsonAsync("/api/public/repair-quote",
            new PublicRepairQuoteRequest("Sam Nguyen", "0400111222", "sam@example.com", "Townsville",
                "Phone", "Apple", "iPhone 13", "Cracked screen"));
        unauthorized.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var quoteRes = await publicClient.PostAsJsonAsync("/api/public/repair-quote",
            new PublicRepairQuoteRequest("Sam Nguyen", "0400111222", "sam@example.com", "Townsville",
                "Phone", "Apple", "iPhone 13", "Cracked screen", "Sms", "Drop damage"));
        quoteRes.EnsureSuccessStatusCode();
        var quote = await quoteRes.Content.ReadFromJsonAsync<PublicRepairQuoteResponse>();
        quote!.TrackingCode.Should().StartWith("TB-");
        quote.AccessPin.Should().HaveLength(6);

        var status = await publicClient.GetFromJsonAsync<PublicRepairStatusDto>(
            $"/api/public/repair-status/{Uri.EscapeDataString(quote.TrackingCode)}?pin={quote.AccessPin}");
        status!.DeviceLabel.Should().Contain("iPhone");
        status.TrackingCode.Should().Be(quote.TrackingCode);
        status.Timeline.Should().NotBeEmpty();

        var sellRes = await publicClient.PostAsJsonAsync("/api/public/sell-device",
            new PublicSellDeviceRequest("Phone", "Apple", "iPhone 12", "Sam Nguyen", "0400111222", "sam@example.com",
                "Townsville", "Sms", Storage: "128GB", BatteryHealth: "86%", PowersOn: true, Charges: true,
                ScreenCondition: "Good", FindMyEnabled: false, PhotoUrls: ["https://example.com/a.jpg"]));
        sellRes.EnsureSuccessStatusCode();
        var sell = await sellRes.Content.ReadFromJsonAsync<PublicSellDeviceResponse>();
        sell!.Reference.Should().StartWith("SYT-");
        sell.Status.Should().Be("New");

        var register = await publicClient.PostAsJsonAsync("/api/customer/register",
            new CustomerPortalRegisterRequest("sam@example.com", "Portal!2026", "Sam", "Nguyen", "0400111222"));
        register.EnsureSuccessStatusCode();
        var portalAuth = await register.Content.ReadFromJsonAsync<CustomerPortalAuthResponse>();
        portalAuth!.AccessToken.Should().NotBeNullOrWhiteSpace();

        using var portalClient = _factory.CreateClient();
        portalClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", portalAuth.AccessToken);
        var repairs = await portalClient.GetFromJsonAsync<CustomerRepairListItemDto[]>("/api/customer/repairs");
        repairs.Should().Contain(r => r.TrackingCode == quote.TrackingCode);

        var sellList = await portalClient.GetFromJsonAsync<CustomerSellRequestListItemDto[]>("/api/customer/sell-requests");
        sellList.Should().Contain(s => s.Reference == sell.Reference);

        var dash = await _client.GetFromJsonAsync<WebsiteDashboardCardsDto>("/api/website/dashboard");
        dash!.NewSellRequests.Should().BeGreaterThan(0);

        var msgRes = await publicClient.PostAsJsonAsync(
            $"/api/public/repair-status/{Uri.EscapeDataString(quote.TrackingCode)}/message",
            new PublicRepairMessageRequest(quote.AccessPin, "Can I collect after 4pm?"));
        msgRes.EnsureSuccessStatusCode();

        var pcGone = await publicClient.PostAsJsonAsync("/api/public/custom-pc",
            new PublicCustomPcRequest("Sam Nguyen", "0400111222", "sam@example.com", 2500m, "Gaming",
                null, null, null, null, null, null, null, null, null, null));
        ((int)pcGone.StatusCode).Should().Be(410);

        var staffPcGone = await _client.GetAsync("/api/custom-pc");
        ((int)staffPcGone.StatusCode).Should().Be(410);

        var checklists = await _client.GetFromJsonAsync<RefurbChecklistDto[]>("/api/refurb-diagnostics/checklists");
        checklists.Should().NotBeEmpty();

        await _client.PutAsJsonAsync("/api/website/booking-availability-settings",
            new BookingAvailabilitySettingsDto(8, 16, 30, 2));
        var slots = await publicClient.GetFromJsonAsync<PublicAvailabilitySlotDto[]>(
            $"/api/public/booking-availability?day={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("o"))}");
        slots.Should().NotBeEmpty();

        var health = await _client.GetFromJsonAsync<HealthDto>("/api/health");
        health!.ApiVersion.Should().Be(ProductVersions.Api);
    }

    private async Task<T> PostJson<T>(string url, object body)
    {
        var response = await _client.PostAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task EnsureSetupAndLoginAsync()
    {
        var setup = new SetupRequest(
            "TechBench NQ", null, null, null, null, null, "QLD", null, null, true, 0.1m, "AUD", 110m,
            "Owner", "web-owner@example.com", "Workshop!2026", "#0F766E");
        (await _client.PostAsJsonAsync("/api/setup", setup)).EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("web-owner@example.com", "Workshop!2026"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }
}
