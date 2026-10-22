using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Workshop;

namespace WorkshopOS.Api.Tests;

[Collection("api")]
public sealed class RefurbishedPhase1Tests
{
    private readonly WorkshopApiFactory _factory;
    private readonly HttpClient _client;

    public RefurbishedPhase1Tests(WorkshopApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task BuyCalculator_Dashboard_Acquisition_Convert()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var calc = await PostJson<RefurbBuyCalcResult>("/api/used-tech/buy-calculator", new RefurbBuyCalcRequest(
            EstimatedResalePrice: 800m,
            ProposedPurchasePrice: 350m,
            ReplacementParts: 80m,
            TechnicianLabour: 40m));
        calc.GrossProfit.Should().BeGreaterThan(0);
        calc.MaximumRecommendedPurchasePrice.Should().BeGreaterThan(0);
        calc.Recommendation.Should().BeOneOf(
            "Excellent Buy", "Good Buy", "Acceptable", "Low Margin", "High Risk", "Avoid");

        var lead = await PostJson<AcquisitionLeadDetailDto>("/api/acquisitions", new CreateAcquisitionLeadRequest(
            Brand: "Apple",
            Model: "iPhone 13",
            ModelNumber: "A2633",
            Category: "Phone",
            StorageCapacity: "128GB",
            Colour: "Midnight",
            Imei: "356938035643809",
            Serial: null,
            Carrier: "Unlocked",
            SellerName: "Jordan Lee",
            SellerContact: "0400 555 111",
            SellerEmail: "jordan@example.com",
            SellerPhone: "0400 555 111",
            SellerType: "Individual",
            AcquisitionSource: "Facebook Marketplace",
            ListingUrl: "https://facebook.com/marketplace/item/example-13",
            AskingPrice: 420m,
            ProposedOffer: 350m,
            DeviceCondition: "Cracked screen",
            KnownFaults: "Front glass cracked",
            SuspectedFaults: "Battery degradation",
            ActivationLockStatus: "Unknown",
            PhotoUrls: ["https://example.com/photo1.jpg"],
            Notes: "Seller can meet in Townsville",
            AssignedStaffId: null,
            FollowUpAt: DateTimeOffset.UtcNow.AddDays(1),
            SellerCustomerId: null));

        lead.Reference.Should().StartWith("TB-ACQ-");
        lead.Status.Should().Be("New Lead");
        lead.Brand.Should().Be("Apple");
        lead.Events.Should().NotBeEmpty();

        var offer = await PostJson<AcquisitionOfferDto>($"/api/acquisitions/{lead.Id}/offers",
            new CreateAcquisitionOfferRequest(350m, "Hi, would you take $350 cash today?", null, 800m, 80m, 40m, true));
        offer.Amount.Should().Be(350m);
        offer.Status.Should().Be("Sent");
        offer.BuyRecommendation.Should().NotBeNullOrWhiteSpace();

        var afterOffer = await _client.GetFromJsonAsync<AcquisitionLeadDetailDto>($"/api/acquisitions/{lead.Id}");
        afterOffer!.Status.Should().Be("Offer Sent");
        afterOffer.ProposedOffer.Should().Be(350m);

        await PostJson<AcquisitionLeadDetailDto>($"/api/acquisitions/{lead.Id}/status",
            new UpdateAcquisitionStatusRequest("Accepted", "Seller agreed"));

        var device = await PostJson<UsedDeviceDetailDto>($"/api/acquisitions/{lead.Id}/convert",
            new ConvertAcquisitionRequest(350m, 800m, 80m, "Purchased", "A-12"));
        device.StockId.Should().StartWith("TB-RFB-");
        device.PurchasePrice.Should().Be(350m);
        device.PartsCost.Should().Be(80m);
        device.AskingPrice.Should().Be(800m);
        device.AcquisitionLeadId.Should().Be(lead.Id);
        device.LocationBin.Should().Be("A-12");
        device.Security.Should().NotBeNull();
        device.Costs.Should().NotBeNull();
        device.Costs!.TotalInvested.Should().Be(430m);

        var list = await _client.GetFromJsonAsync<UsedDeviceDto[]>("/api/used-tech?q=iPhone%2013");
        list.Should().Contain(d => d.Id == device.Id && d.StockId == device.StockId);

        var dashboard = await _client.GetFromJsonAsync<RefurbDashboardDto>("/api/used-tech/dashboard");
        dashboard.Should().NotBeNull();
        dashboard!.Pipeline.TotalAcquired.Should().BeGreaterThanOrEqualTo(1);
        dashboard.Pipeline.AwaitingInspection.Should().BeGreaterThanOrEqualTo(1);
        dashboard.Financials.CurrentInventoryInvestment.Should().BeGreaterThan(0);

        // Idempotent convert
        var again = await PostJson<UsedDeviceDetailDto>($"/api/acquisitions/{lead.Id}/convert",
            new ConvertAcquisitionRequest(null, null, null, null, null));
        again.Id.Should().Be(device.Id);
    }

    [Fact]
    public async Task UsedTech_Create_AssignsStockId()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var created = await PostJson<UsedDeviceDetailDto>("/api/used-tech", new CreateUsedDeviceRequest(
            null, "Samsung", "Galaxy S21", "Phone", null, "356938035643809", "Phantom Grey", "256GB",
            null, "Good", "Purchased", 200m, 450m, 50m, "No charge", null, "Walk-In", null, null, null));
        created.StockId.Should().StartWith("TB-RFB-");
        created.Sku.Should().Be(created.StockId);
    }

    private async Task EnsureSetupAndLoginAsync()
    {
        var setup = new SetupRequest(
            "Refurb Shop", null, null, null, null, null, "QLD", null, null, true, 0.1m, "AUD", 110m,
            "Owner", "refurb-owner@example.com", "Workshop!2026", "#0F766E");
        (await _client.PostAsJsonAsync("/api/setup", setup)).EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("refurb-owner@example.com", "Workshop!2026"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    private async Task<T> PostJson<T>(string url, object body)
    {
        var response = await _client.PostAsJsonAsync(url, body);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new Exception($"{(int)response.StatusCode} {url}: {err}");
        }
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
