using System.Net.Http.Json;
using FluentAssertions;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Workshop;

namespace WorkshopOS.Api.Tests;

[Collection("api")]
public sealed class CatalogueFlowTests
{
    private readonly WorkshopApiFactory _factory;
    private readonly HttpClient _client;

    public CatalogueFlowTests(WorkshopApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Seed_Search_Repair_Quote_AdminUpsert()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        // Ensure seed (startup may already have imported; force for reset DB)
        var import = await _client.PostAsync("/api/catalogue/import?force=true", null);
        import.EnsureSuccessStatusCode();
        var importDto = await import.Content.ReadFromJsonAsync<CatalogueImportResultDto>();
        importDto!.ServicesUpserted.Should().BeGreaterThan(50);

        var cats = await _client.GetFromJsonAsync<CatalogueCategoryDto[]>("/api/catalogue/categories");
        cats.Should().Contain(c => c.Key == "phone_repair");
        cats.Should().Contain(c => c.Key == "pc_upgrades");

        var all = await _client.GetFromJsonAsync<CatalogueServiceDto[]>("/api/catalogue/services?activeOnly=true");
        all.Should().Contain(s => s.Code == "PHONE-SCREEN-REPLACE");
        all.Should().Contain(s => s.Code == "PC-SSD-UPGRADE");

        var screen = await _client.GetFromJsonAsync<CatalogueServiceDto[]>("/api/catalogue/services?q=screen");
        screen!.Should().Contain(s => s.Name.Contains("Screen", StringComparison.OrdinalIgnoreCase));
        screen.Should().Contain(s => s.Code == "PHONE-SCREEN-REPLACE");

        var ssd = await _client.GetFromJsonAsync<CatalogueServiceDto[]>("/api/catalogue/services?q=SSD");
        ssd!.Should().Contain(s => s.Code == "PC-SSD-UPGRADE" || s.Name.Contains("SSD", StringComparison.OrdinalIgnoreCase));

        var phoneScreen = all!.First(s => s.Code == "PHONE-SCREEN-REPLACE");
        var pcSsd = all.First(s => s.Code == "PC-SSD-UPGRADE");

        var customer = await PostJson<CustomerDetailDto>("/api/customers", new UpsertCustomerRequest(
            null, CustomerType.Individual, "Cat", "Alogue", null, "0400 111 222", "cat@example.com",
            null, "Melbourne", "VIC", "3000", null, PreferredContact.Sms, false));

        var repair = await PostJson<RepairDetailDto>("/api/repairs", new CreateRepairRequest(
            customer.Id, null, null, null, null,
            "Screen + storage", null, null, null, null, true,
            true, false, false, false, null, null, null, null,
            DeviceCategory.Phone, "Apple", "iPhone 13", null, null,
            new[] { phoneScreen.Id, pcSsd.Id }, null));
        repair.ServiceLines.Should().NotBeNull();
        repair.ServiceLines!.Should().HaveCount(2);
        repair.ServiceLines.Should().Contain(l => l.Code == "PHONE-SCREEN-REPLACE");
        repair.EstimatedPrice.Should().Be(phoneScreen.DefaultLabourFee + pcSsd.DefaultLabourFee);

        var line = repair.ServiceLines.First();
        var completed = await PostJson<RepairDetailDto>(
            $"/api/repairs/{repair.Id}/service-lines/{line.Id}/complete",
            new CompleteRepairServiceLineRequest(true));
        completed.ServiceLines!.First(l => l.Id == line.Id).IsCompleted.Should().BeTrue();

        var quote = await PostJson<QuoteDetailDto>("/api/quotes", new CreateQuoteRequest(
            customer.Id, repair.Id, "Multi service quote", null, null, "Apple", "iPhone 13", null, "Phone", 14,
            [
                new QuoteLineInputDto("SERVICE", phoneScreen.Name, phoneScreen.Name, null, null, phoneScreen.Code,
                    null, phoneScreen.Id, null, 1m, 0m, 0m, 0m, null, null, null, null, 0m, 0m, null),
                new QuoteLineInputDto("SERVICE", pcSsd.Name, pcSsd.Name, null, null, pcSsd.Code,
                    null, pcSsd.Id, null, 1m, 0m, 0m, 0m, null, null, null, null, 0m, 0m, null)
            ],
            LabourFee: phoneScreen.DefaultLabourFee + pcSsd.DefaultLabourFee,
            MarkupPercent: 20m));
        quote.Lines.Should().HaveCount(2);
        quote.LabourFee.Should().Be(phoneScreen.DefaultLabourFee + pcSsd.DefaultLabourFee);

        var upserted = await PostJson<CatalogueServiceDto>("/api/catalogue/services", new UpsertCatalogueServiceRequest(
            null, "Admin Custom Polish", "Phone Repair", "Custom polish", 55m, null, true, 999,
            "PHONE-CUSTOM-POLISH", "Other Phone Repairs", "Phone", null, null, 0m, 25,
            null, null, false, false, 30, "Use microfibre", "Device polish service.", null, "phone_repair"));
        upserted.Code.Should().Be("PHONE-CUSTOM-POLISH");
        upserted.DefaultLabourFee.Should().Be(55m);

        var fav = await _client.PostAsync($"/api/catalogue/favourites/{phoneScreen.Id}", null);
        fav.EnsureSuccessStatusCode();
        var favourites = await _client.GetFromJsonAsync<CatalogueServiceDto[]>("/api/catalogue/favourites");
        favourites.Should().Contain(s => s.Id == phoneScreen.Id);

        var brands = await _client.GetFromJsonAsync<DeviceBrandDto[]>("/api/catalogue/brands?deviceType=Phone");
        brands.Should().Contain(b => b.Name == "Apple");
        var models = await _client.GetFromJsonAsync<DeviceModelDto[]>("/api/catalogue/models?brand=Apple&deviceType=Phone");
        models.Should().Contain(m => m.Name.Contains("iPhone 13"));
    }

    private async Task EnsureSetupAndLoginAsync()
    {
        var setup = new SetupRequest(
            "Catalogue Shop", null, null, null, null, null, "VIC", null, null, true, 0.1m, "AUD", 110m,
            "Owner", "catalogue@example.com", "Workshop!2026", "#0F766E");
        (await _client.PostAsJsonAsync("/api/setup", setup)).EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("catalogue@example.com", "Workshop!2026"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    private async Task<T> PostJson<T>(string url, object body)
    {
        var res = await _client.PostAsJsonAsync(url, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }
}
