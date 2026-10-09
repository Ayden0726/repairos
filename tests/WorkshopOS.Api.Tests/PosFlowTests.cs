using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Common;
using WorkshopOS.Contracts.Operations;

namespace WorkshopOS.Api.Tests;

[Collection("api")]
public sealed class PosFlowTests
{
    private readonly WorkshopApiFactory _factory;
    private readonly HttpClient _client;

    public PosFlowTests(WorkshopApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Pos_Disabled_By_Default_Then_Enable_And_Sell()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var modules = await _client.GetFromJsonAsync<ModuleDto[]>("/api/settings/modules");
        modules.Should().NotBeNull();
        modules!.First(m => m.Key == "pos").Visible.Should().BeFalse();
        modules.First(m => m.Key == "pos").Implemented.Should().BeTrue();

        var disabled = await _client.PostAsJsonAsync("/api/pos/sales", new CreatePosSaleRequest(
            null, "Cash", null, null,
            [new PosSaleLineInputDto(null, "Cable", 1, 12m)]));
        disabled.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var posSettings = await _client.PutAsJsonAsync("/api/settings/pos", new UpdatePosSettingsRequest(
            true, "ManualEftpos", null, null, null, null, false, "Counter pad"));
        posSettings.EnsureSuccessStatusCode();
        var saved = await posSettings.Content.ReadFromJsonAsync<PosSettingsDto>();
        saved!.Enabled.Should().BeTrue();
        saved.Provider.Should().Be("ManualEftpos");

        modules = await _client.GetFromJsonAsync<ModuleDto[]>("/api/settings/modules");
        modules!.First(m => m.Key == "pos").Visible.Should().BeTrue();

        var item = await PostJson<InventoryListItemDto>("/api/inventory", new UpsertInventoryRequest(
            null, "SKU-CASE-01", null, "Phone case", "Accessories", 5m, 29.95m, 10, 2, 5, "B2", null));

        var sale = await PostJson<PosSaleDetailDto>("/api/pos/sales", new CreatePosSaleRequest(
            null, "Eftpos", "AUTH9911", "Counter sale",
            [
                new PosSaleLineInputDto(item.Id, null, 2, null),
                new PosSaleLineInputDto(null, "Fitment", 1, 10m)
            ]));

        sale.Number.Should().StartWith("POS-");
        sale.InvoiceNumber.Should().StartWith("INV-");
        sale.CustomerName.Should().Be("Walk-in Customer");
        sale.PaymentMethod.Should().Be("Eftpos");
        sale.PaymentProvider.Should().Be("ManualEftpos");
        sale.ProviderReference.Should().Be("AUTH9911");
        sale.Status.Should().Be("Completed");
        sale.Lines.Should().HaveCount(2);
        sale.Total.Should().Be(69.90m); // 2*29.95 + 10

        var stock = await _client.GetFromJsonAsync<InventoryListItemDto[]>("/api/inventory");
        stock!.First(i => i.Id == item.Id).OnHand.Should().Be(8);

        var listed = await _client.GetFromJsonAsync<PosSaleListItemDto[]>("/api/pos/sales");
        listed.Should().Contain(s => s.Id == sale.Id);

        var providers = await _client.GetFromJsonAsync<PosProviderInfoDto[]>("/api/pos/providers");
        providers.Should().Contain(p => p.Key == "Tyro");
        providers.Should().Contain(p => p.Key == "Square");

        var voidResponse = await _client.PostAsync($"/api/pos/sales/{sale.Id}/void", null);
        voidResponse.EnsureSuccessStatusCode();
        var voided = await voidResponse.Content.ReadFromJsonAsync<PosSaleDetailDto>();
        voided!.Status.Should().Be("Voided");
        stock = await _client.GetFromJsonAsync<InventoryListItemDto[]>("/api/inventory");
        stock!.First(i => i.Id == item.Id).OnHand.Should().Be(10);
    }

    private async Task EnsureSetupAndLoginAsync()
    {
        var setup = new SetupRequest(
            "POS Shop", null, null, null, null, null, "QLD", null, null, true, 0.1m, "AUD", 110m,
            "Owner", "pos-owner@example.com", "Workshop!2026", "#0F766E");
        (await _client.PostAsJsonAsync("/api/setup", setup)).EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("pos-owner@example.com", "Workshop!2026"));
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
