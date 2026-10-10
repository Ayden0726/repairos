using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Common;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Workshop;

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
        saved.DiscountPresets.Should().NotBeEmpty();
        saved.DiscountPresets.Should().Contain(p => p.Key == "staff" && p.Percent == 10m);
        saved.DiscountPresets.Should().Contain(p => p.Key == "family_friends" && p.Percent == 15m);

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
        sale.DiscountAmount.Should().Be(0m);

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

    [Fact]
    public async Task Pos_Retail_Discount_Preset_Reduces_Total()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();
        await EnablePosAsync();

        var sale = await PostJson<PosSaleDetailDto>("/api/pos/sales", new CreatePosSaleRequest(
            null, "Cash", null, null,
            [new PosSaleLineInputDto(null, "Screen protector", 1, 100m)],
            DiscountPresetKey: "staff"));

        sale.DiscountPercent.Should().Be(10m);
        sale.DiscountAmount.Should().Be(10m);
        sale.Total.Should().Be(90m);
        sale.AmountPaid.Should().Be(90m);
        sale.DiscountLabel.Should().Be("Staff");
        sale.IsDeposit.Should().BeFalse();
    }

    [Fact]
    public async Task Pos_Repair_Deposit_Then_Full_Payoff_With_Discount()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();
        await EnablePosAsync();

        var customer = await PostJson<CustomerDetailDto>("/api/customers", new UpsertCustomerRequest(
            null, CustomerType.Individual, "Pay", "Repair", null, "0400 222 333", "pay.repair@example.com",
            null, "Brisbane", "QLD", "4000", null, PreferredContact.Sms, false));

        var repair = await PostJson<RepairDetailDto>("/api/repairs", new CreateRepairRequest(
            customer.Id, null, null, null, null,
            "Battery swollen", null, null, null, null, true,
            true, false, false, false, null, 400m, null, null,
            DeviceCategory.Phone, "Samsung", "S22", null, null));

        var payable = await _client.GetFromJsonAsync<PosPayableRepairDto[]>(
            $"/api/pos/repairs-payable?q={Uri.EscapeDataString(repair.TicketNumber)}");
        payable.Should().NotBeNull();
        payable!.Should().Contain(p => p.RepairTicketId == repair.Id && p.Balance == 400m);

        var deposit = await PostJson<PosSaleDetailDto>("/api/pos/sales", new CreatePosSaleRequest(
            null, "Cash", null, "Repair deposit",
            [],
            RepairTicketId: repair.Id,
            AmountToPay: 100m,
            IsDeposit: true));

        deposit.RepairTicketId.Should().Be(repair.Id);
        deposit.RepairTicketNumber.Should().Be(repair.TicketNumber);
        deposit.IsDeposit.Should().BeTrue();
        deposit.Total.Should().Be(100m);
        deposit.AmountPaid.Should().Be(100m);
        deposit.InvoiceBalance.Should().Be(300m);

        // Remaining balance with family & friends 15% → discount 45, pay 255
        var payoff = await PostJson<PosSaleDetailDto>("/api/pos/sales", new CreatePosSaleRequest(
            null, "Eftpos", "AUTH-REPAIR-1", "Final payoff",
            [],
            RepairTicketId: repair.Id,
            IsDeposit: false,
            DiscountPresetKey: "family_friends"));

        payoff.IsDeposit.Should().BeFalse();
        payoff.DiscountPercent.Should().Be(15m);
        payoff.DiscountAmount.Should().Be(45m);
        payoff.Total.Should().Be(255m);
        payoff.InvoiceBalance.Should().Be(0m);

        var invoice = await _client.GetFromJsonAsync<InvoiceDetailDto>($"/api/invoices/{payoff.InvoiceId}");
        invoice!.Status.Should().Be("Paid");
        invoice.AmountPaid.Should().Be(invoice.Total);
        invoice.Balance.Should().Be(0m);

        var stillPayable = await _client.GetFromJsonAsync<PosPayableRepairDto[]>(
            $"/api/pos/repairs-payable?q={Uri.EscapeDataString(repair.TicketNumber)}");
        stillPayable!.Should().NotContain(p => p.RepairTicketId == repair.Id);
    }

    [Fact]
    public async Task Pos_Discount_Presets_Configurable_In_Settings()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var updated = await PostPutJson<PosSettingsDto>("/api/settings/pos", new UpdatePosSettingsRequest(
            true, "None", null, null, null, null, false, null,
            [
                new PosDiscountPresetDto("staff", "Staff", 20m, true),
                new PosDiscountPresetDto("family_friends", "Family & Friends", 25m, true),
                new PosDiscountPresetDto("loyalty", "Loyalty", 8m, true),
                new PosDiscountPresetDto("other", "Other", 0m, true)
            ]));

        updated.DiscountPresets.First(p => p.Key == "staff").Percent.Should().Be(20m);
        updated.DiscountPresets.First(p => p.Key == "family_friends").Percent.Should().Be(25m);

        var sale = await PostJson<PosSaleDetailDto>("/api/pos/sales", new CreatePosSaleRequest(
            null, "Cash", null, null,
            [new PosSaleLineInputDto(null, "Case", 1, 50m)],
            DiscountPresetKey: "staff"));
        sale.DiscountPercent.Should().Be(20m);
        sale.DiscountAmount.Should().Be(10m);
        sale.Total.Should().Be(40m);
    }

    private async Task EnablePosAsync()
    {
        var posSettings = await _client.PutAsJsonAsync("/api/settings/pos", new UpdatePosSettingsRequest(
            true, "ManualEftpos", null, null, null, null, false, "Counter"));
        posSettings.EnsureSuccessStatusCode();
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
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase} for {url}: {err}");
        }
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<T> PostPutJson<T>(string url, object body)
    {
        var response = await _client.PutAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
