using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Workshop;
using WorkshopOS.Infrastructure.Persistence;

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
            new QuoteLineCalcInput("PART", "Panel A", null, "Panel A", null, null, null, null, null,
                1m, 100m, 0m, 0m, null, null, null, null, 0m, 0m, null),
            new QuoteLineCalcInput("PART", "Panel B", null, "Panel B", null, null, null, null, null,
                1m, 56m, 0m, 0m, null, null, null, null, 0m, 0m, null)
        ], null, null, null, null));
        preview.PartsCostTotal.Should().Be(156m);
        preview.PartsSellTotal.Should().Be(187.20m);
        preview.LabourFee.Should().Be(50m);
        preview.Total.Should().Be(239m);
        preview.ProfitTotal.Should().Be(83m);
        preview.MarginPercent.Should().Be(34.73m);
        preview.Lines.Should().OnlyContain(l => l.LabourAmount == 0m);

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
                    null, service.Id, null, 1m, 100m, 0m, 0m, null, null, null, null, 0m, 0m, null),
                new QuoteLineInputDto("PART", "Adhesive kit", null, "Adhesive", null, "SKU-2",
                    null, null, null, 1m, 56m, 0m, 0m, null, null, null, null, 0m, 0m, null)
            ], LabourFee: 50m, MarkupPercent: 20m));
        quote.Number.Should().StartWith("QTE-");
        quote.PartsCostTotal.Should().Be(156m);
        quote.PartsSellTotal.Should().Be(187.20m);
        quote.LabourFee.Should().Be(50m);
        quote.Total.Should().Be(239m);
        quote.Status.Should().Be("Draft");
        quote.IncludeInternalFinancials.Should().BeTrue();
        quote.ProfitTotal.Should().Be(83m);
        quote.Lines.Should().OnlyContain(l => l.LabourAmount == 0m);

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
        html.Should().Contain("@page{size:A4;margin:12mm}");
        html.Should().Contain("max-width:190mm");
        html.Should().Contain("$");
        html.Should().NotContain("¤");
        html.Should().Contain("Labour / service");
        html.Should().Contain("$50.00");
        html.Should().Contain("<span>Parts</span>");
        html.Should().Contain("<span>Labour</span>");
        html.Should().Contain("Amount");
        html.Should().NotContain("Landed");
        html.Should().NotContain("Markup");
        html.Should().NotContain("Profit");
        // Single labour row — not duplicated across part lines
        (html.Split("Labour / service", StringSplitOptions.None).Length - 1).Should().Be(1);

        var dash = await _client.GetFromJsonAsync<DashboardDto>("/api/dashboard");
        dash!.QuoteAnalytics.Should().NotBeNull();
        dash.QuoteAnalytics!.QuotesAccepted30Days.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Preview_Uses_Saved_Pricing_Settings_Not_Seed_Defaults()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var settings = await _client.GetFromJsonAsync<PricingSettingsDto>("/api/pricing/settings");
        settings.Should().NotBeNull();

        var updated = settings! with
        {
            Parts = settings.Parts with { MarkupMethod = "FlatPercent", DefaultMarkupPercent = 40m },
            Labour = settings.Labour with { DefaultLabourFee = 75m, DifficultyPricingEnabled = false },
            Rounding = settings.Rounding with { Method = "None" }
        };
        var put = await _client.PutAsJsonAsync("/api/pricing/settings", updated);
        put.EnsureSuccessStatusCode();
        var saved = await put.Content.ReadFromJsonAsync<PricingSettingsDto>();
        saved!.Parts.DefaultMarkupPercent.Should().Be(40m);
        saved.Labour.DefaultLabourFee.Should().Be(75m);

        // Null job labour/markup → server must apply saved settings (not hardcoded 20/50).
        var preview = await PostJson<PricingPreviewResponse>("/api/pricing/preview", new PricingPreviewRequest(
        [
            new QuoteLineCalcInput("PART", "Battery", null, "Battery", null, null, null, null, null,
                1m, 100m, 0m, 0m, null, null, null, null, 0m, 0m, null)
        ], null, null, null, null));

        preview.MarkupPercent.Should().Be(40m);
        preview.PartsSellTotal.Should().Be(140m);
        preview.LabourFee.Should().Be(75m);
        preview.Lines[0].LabourAmount.Should().Be(0m);
        preview.PreRoundTotal.Should().Be(215m);
        preview.Total.Should().Be(215m);
    }

    [Fact]
    public async Task Pricing_Settings_Put_Then_Get_Returns_Same_Values()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var before = await _client.GetFromJsonAsync<PricingSettingsDto>("/api/pricing/settings");
        before.Should().NotBeNull();

        var updated = before! with
        {
            Labour = before.Labour with
            {
                DefaultLabourFee = 67.5m,
                MinimumLabourFee = 12.25m,
                DifficultyPricingEnabled = true
            },
            Parts = before.Parts with
            {
                MarkupMethod = "Hybrid",
                DefaultMarkupPercent = 27.5m,
                FixedMarkupAmount = 5.5m,
                MinimumPartProfit = 3.25m
            },
            Profitability = before.Profitability with
            {
                MinimumGrossMarginPercent = 18m,
                WarnBelowMarginPercent = 22m,
                ManagerApprovalRequired = false
            },
            Rounding = before.Rounding with { Method = "Nearest5" },
            Discounts = before.Discounts with { MaxTechDiscountPercent = 7.5m },
            Quote = before.Quote with { DefaultValidityDays = 21, AutoExpire = false },
            Tax = new TaxPricingDto(true, 0.15m, false)
        };

        var put = await _client.PutAsJsonAsync("/api/pricing/settings", updated);
        put.EnsureSuccessStatusCode();
        var putBody = await put.Content.ReadFromJsonAsync<PricingSettingsDto>();
        putBody.Should().NotBeNull();

        var got = await _client.GetFromJsonAsync<PricingSettingsDto>("/api/pricing/settings");
        got.Should().NotBeNull();
        got!.Labour.DefaultLabourFee.Should().Be(67.5m);
        got.Labour.MinimumLabourFee.Should().Be(12.25m);
        got.Labour.DifficultyPricingEnabled.Should().BeTrue();
        got.Parts.MarkupMethod.Should().Be("Hybrid");
        got.Parts.DefaultMarkupPercent.Should().Be(27.5m);
        got.Parts.FixedMarkupAmount.Should().Be(5.5m);
        got.Parts.MinimumPartProfit.Should().Be(3.25m);
        got.Profitability.MinimumGrossMarginPercent.Should().Be(18m);
        got.Profitability.WarnBelowMarginPercent.Should().Be(22m);
        got.Profitability.ManagerApprovalRequired.Should().BeFalse();
        got.Rounding.Method.Should().Be("Nearest5");
        got.Discounts.MaxTechDiscountPercent.Should().Be(7.5m);
        got.Quote.DefaultValidityDays.Should().Be(21);
        got.Quote.AutoExpire.Should().BeFalse();
        got.Tax.Should().NotBeNull();
        got.Tax!.Enabled.Should().BeTrue();
        got.Tax.Rate.Should().Be(0.15m);
        got.Tax.Inclusive.Should().BeFalse();

        // Business profile tax must mirror pricing Tax after pricing PUT.
        var business = await _client.GetFromJsonAsync<BusinessProfileDto>("/api/settings/business");
        business!.GstRegistered.Should().BeTrue();
        business.GstRate.Should().Be(0.15m);
        business.GstInclusive.Should().BeFalse();

        // Raw jsonb row must deserialize to the same markup/labour (proves BusinessSetting write).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkshopDbContext>();
            var row = await db.Settings.AsNoTracking()
                .FirstAsync(s => s.Key == SettingKeys.PricingSettings);
            row.JsonValue.Should().Contain("27.5");
            row.JsonValue.Should().Contain("67.5");
            var fromDb = System.Text.Json.JsonSerializer.Deserialize<PricingSettingsDto>(
                row.JsonValue, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            fromDb!.Parts.DefaultMarkupPercent.Should().Be(27.5m);
            fromDb.Labour.DefaultLabourFee.Should().Be(67.5m);
        }
    }

    [Fact]
    public async Task Business_Tax_Put_Then_Get_And_Pricing_Mirror()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var business = await _client.GetFromJsonAsync<BusinessProfileDto>("/api/settings/business");
        business.Should().NotBeNull();
        var updated = business! with
        {
            GstRegistered = true,
            GstRate = 0.125m,
            GstInclusive = false,
            Currency = "NZD"
        };
        var put = await _client.PutAsJsonAsync("/api/settings/business", updated);
        put.EnsureSuccessStatusCode();

        var got = await _client.GetFromJsonAsync<BusinessProfileDto>("/api/settings/business");
        got!.GstRate.Should().Be(0.125m);
        got.GstInclusive.Should().BeFalse();
        got.Currency.Should().Be("NZD");

        var pricing = await _client.GetFromJsonAsync<PricingSettingsDto>("/api/pricing/settings");
        pricing!.Tax.Should().NotBeNull();
        pricing.Tax!.Rate.Should().Be(0.125m);
        pricing.Tax.Inclusive.Should().BeFalse();
        pricing.Tax.Enabled.Should().BeTrue();
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
