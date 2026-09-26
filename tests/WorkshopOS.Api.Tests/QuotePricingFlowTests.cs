using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Workshop;

namespace WorkshopOS.Api.Tests;

[Collection("api")]
public sealed class QuotePricingFlowTests
{
    private readonly WorkshopApiFactory _factory;
    private readonly HttpClient _client;

    public QuotePricingFlowTests(WorkshopApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Pricing_Settings_Preview_Quote_Accept_Print()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var settings = await _client.GetFromJsonAsync<PricingSettingsDto>("/api/pricing/settings");
        settings.Should().NotBeNull();
        settings!.Parts.DefaultMarkupPercent.Should().Be(20m);
        settings.Labour.DefaultLabourFee.Should().Be(50m);

        var preview = await PostJson<PricingPreviewResponse>("/api/pricing/preview", new PricingPreviewRequest(
        [
            new QuoteLineCalcInput("PART", "Screen", null, "Screen", null, null, null, null, null,
                1m, 156m, 0m, 0m, null, null, null, 50m, 0m, 0m, null)
        ], null, null, null, null));
        preview.Total.Should().Be(239m);
        preview.ProfitTotal.Should().Be(83m);
        preview.MarginPercent.Should().Be(34.73m);

        var service = await PostJson<ServicePricingDto>("/api/pricing/services", new UpsertServicePricingRequest(
            null, "Screen replacement", "Phone", "Labour + part", 50m, 20m, true, 1));
        service.Name.Should().Be("Screen replacement");

        var tier = await PostJson<MarkupTierDto>("/api/pricing/tiers", new UpsertMarkupTierRequest(null, 0m, 100m, 30m, 1));
        tier.MarkupPercent.Should().Be(30m);

        var customer = await PostJson<CustomerDetailDto>("/api/customers", new UpsertCustomerRequest(
            null, CustomerType.Individual, "Quote", "Customer", null, "0400 999 888", "quote@example.com",
            null, "Melbourne", "VIC", "3000", null, PreferredContact.Email, false));

        var quote = await PostJson<QuoteDetailDto>("/api/quotes", new CreateQuoteRequest(
            customer.Id, null, "Cracked OLED", null, null, "Apple", "iPhone 13", null, "Phone", 14,
            [
                new QuoteLineInputDto("PART", "OLED assembly", "Screen replacement", "OLED", "Foxconn", "SKU-1",
                    null, service.Id, null, 1m, 156m, 0m, 0m, null, null, null, 50m, 0m, 0m, null)
            ]));
        quote.Number.Should().StartWith("QTE-");
        quote.Total.Should().Be(239m);
        quote.Status.Should().Be("Draft");
        quote.IncludeInternalFinancials.Should().BeTrue();
        quote.ProfitTotal.Should().Be(83m);

        var sent = await _client.PostAsync($"/api/quotes/{quote.Id}/send", null);
        sent.EnsureSuccessStatusCode();
        var sentDto = await sent.Content.ReadFromJsonAsync<QuoteDetailDto>();
        sentDto!.Status.Should().Be("Sent");

        var accepted = await _client.PostAsync($"/api/quotes/{quote.Id}/accept", null);
        accepted.EnsureSuccessStatusCode();
        var acceptedDto = await accepted.Content.ReadFromJsonAsync<QuoteDetailDto>();
        acceptedDto!.Status.Should().Be("Accepted");
        acceptedDto.IsFrozen.Should().BeTrue();

        var print = await _client.GetAsync($"/api/quotes/{quote.Id}/print");
        print.EnsureSuccessStatusCode();
        var html = await print.Content.ReadAsStringAsync();
        html.Should().Contain(quote.Number);
        html.Should().Contain("OLED assembly");
        html.Should().NotContain("Landed");
        html.Should().NotContain("Markup");
        html.Should().NotContain("Profit");

        var dash = await _client.GetFromJsonAsync<DashboardDto>("/api/dashboard");
        dash!.QuoteAnalytics.Should().NotBeNull();
        dash.QuoteAnalytics!.QuotesAccepted30Days.Should().BeGreaterThanOrEqualTo(1);
    }

    private async Task EnsureSetupAndLoginAsync()
    {
        var setup = new SetupRequest(
            "Pricing Shop", null, null, null, null, null, "VIC", null, null, true, 0.1m, "AUD", 110m,
            "Owner", "pricing-owner@example.com", "Workshop!2026", "#0F766E");
        (await _client.PostAsJsonAsync("/api/setup", setup)).EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("pricing-owner@example.com", "Workshop!2026"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    private async Task<T> PostJson<T>(string url, object body)
    {
        var response = await _client.PostAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
