using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Workshop;

namespace WorkshopOS.Api.Tests;

[Collection("api")]
public sealed class V2OpsFlowTests
{
    private readonly WorkshopApiFactory _factory;
    private readonly HttpClient _client;

    public V2OpsFlowTests(WorkshopApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Donor_Harvest_Creates_Inventory_With_Trace()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var donor = await PostJson<DonorDeviceDetailDto>("/api/donors", new CreateDonorDeviceRequest(
            "Apple", "iPhone 11", "356938035643809", null, "Board short"));
        donor.Number.Should().StartWith("DNR-");
        donor.Status.Should().Be("Open");

        var harvested = await PostJson<DonorDeviceDetailDto>($"/api/donors/{donor.Id}/harvest",
            new HarvestComponentRequest("Screen assembly", "B", 1, "HV-SCREEN-11", 40m, 120m, "H1"));
        harvested.Components.Should().HaveCount(1);
        harvested.Components[0].InventorySku.Should().Be("HV-SCREEN-11");
        harvested.Status.Should().Be("Harvesting");

        var stock = await _client.GetFromJsonAsync<InventoryListItemDto[]>("/api/inventory");
        stock!.Should().Contain(i => i.Sku == "HV-SCREEN-11" && i.OnHand == 1);
    }

    [Fact]
    public async Task Loan_Checkout_And_Return()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var customer = await PostJson<CustomerDetailDto>("/api/customers", new UpsertCustomerRequest(
            null, CustomerType.Individual, "Loan", "Customer", null, "0400 000 001", null,
            null, "Brisbane", "QLD", "4000", null, PreferredContact.Sms, false));
        var device = await PostJson<LoanDeviceDto>("/api/loans/devices",
            new UpsertLoanDeviceRequest(null, "LOAN-01", "Samsung", "A14", null, null, null));
        device.Status.Should().Be("Available");

        var agreement = await PostJson<LoanAgreementDto>("/api/loans/checkout",
            new CheckoutLoanRequest(device.Id, customer.Id, null, null, "While repair"));
        agreement.Status.Should().Be("Active");

        var devices = await _client.GetFromJsonAsync<LoanDeviceDto[]>("/api/loans/devices");
        devices!.First(d => d.Id == device.Id).Status.Should().Be("OnLoan");

        var returned = await PostJson<LoanAgreementDto>($"/api/loans/agreements/{agreement.Id}/return",
            new ReturnLoanRequest("Back ok"));
        returned.Status.Should().Be("Returned");
        devices = await _client.GetFromJsonAsync<LoanDeviceDto[]>("/api/loans/devices");
        devices!.First(d => d.Id == device.Id).Status.Should().Be("Available");
    }

    [Fact]
    public async Task Pos_Sells_Used_Device_And_Void_Restores()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();
        await EnablePosAsync();

        var used = await PostJson<UsedDeviceDetailDto>("/api/used-tech", new CreateUsedDeviceRequest(
            null, "Google", "Pixel 7", "Phone", null, null, null, null,
            null, "Good", "Ready for Sale", 200m, 449m, 0m, null, null, "Walk-In", null, null, null));

        var sale = await PostJson<PosSaleDetailDto>("/api/pos/sales", new CreatePosSaleRequest(
            null, "Cash", null, "Refurb sale",
            [new PosSaleLineInputDto(null, null, 1, null, used.Id)]));
        sale.Lines.Should().ContainSingle(l => l.UsedDeviceId == used.Id);
        sale.Total.Should().Be(449m);

        var after = await _client.GetFromJsonAsync<UsedDeviceDetailDto>($"/api/used-tech/{used.Id}");
        after!.Status.Should().Be("Sold");
        after.ActualSalePrice.Should().Be(449m);

        var voidResponse = await _client.PostAsync($"/api/pos/sales/{sale.Id}/void", null);
        voidResponse.EnsureSuccessStatusCode();
        after = await _client.GetFromJsonAsync<UsedDeviceDetailDto>($"/api/used-tech/{used.Id}");
        after!.Status.Should().Be("Ready for Sale");
        after.ActualSalePrice.Should().BeNull();
    }

    [Fact]
    public async Task Refund_Approval_Flow()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();
        await EnablePosAsync();

        var sale = await PostJson<PosSaleDetailDto>("/api/pos/sales", new CreatePosSaleRequest(
            null, "Cash", null, null,
            [new PosSaleLineInputDto(null, "Cable", 1, 20m)]));

        var refund = await PostJson<RefundRequestDto>("/api/refunds",
            new CreateRefundRequestDto(sale.Id, sale.InvoiceId, 20m, "Customer changed mind"));
        refund.Status.Should().Be("Pending");
        refund.Number.Should().StartWith("RFD-");

        var decided = await PostJson<RefundRequestDto>($"/api/refunds/{refund.Id}/decide",
            new DecideRefundRequest(true, "Approved by owner"));
        decided.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task Repair_Media_Signature_And_Condition_Report()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var customer = await PostJson<CustomerDetailDto>("/api/customers", new UpsertCustomerRequest(
            null, CustomerType.Individual, "Media", "Test", null, "0400 000 002", null,
            null, "Brisbane", "QLD", "4000", null, PreferredContact.Sms, false));
        var repair = await PostJson<RepairDetailDto>("/api/repairs", new CreateRepairRequest(
            customer.Id, null, null, null, null,
            "Screen cracked", null, null, null, null, true,
            true, false, false, false, null, 200m, null, null,
            DeviceCategory.Phone, "Apple", "iPhone 12", null, null));

        const string png =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";
        var att = await PostJson<RepairAttachmentDto>($"/api/repairs/{repair.Id}/media/attachments",
            new UploadRepairAttachmentRequest("intake.png", "image/png", png, "Front", "Photo"));
        att.FileName.Should().Be("intake.png");

        var sig = await PostJson<RepairSignatureDto>($"/api/repairs/{repair.Id}/media/signatures",
            new CaptureRepairSignatureRequest("Alex Customer", "Intake", png));
        sig.SignerName.Should().Be("Alex Customer");

        var report = await _client.GetAsync($"/api/repairs/{repair.Id}/media/condition-report");
        report.EnsureSuccessStatusCode();
        var html = await report.Content.ReadAsStringAsync();
        html.Should().Contain("Condition report");
        html.Should().Contain(repair.TicketNumber);
    }

    [Fact]
    public async Task Inventory_Barcode_Transfer_Quarantine_And_Backup_Restore_Test()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var item = await PostJson<InventoryListItemDto>("/api/inventory", new UpsertInventoryRequest(
            null, "SKU-BC-01", "BC123456", "Barcode part", "Parts", 5m, 15m, 10, 1, 5, "A1", null));

        var found = await _client.GetFromJsonAsync<InventoryListItemDto>($"/api/inventory/barcode/{item.Barcode}");
        found!.Id.Should().Be(item.Id);

        var moved = await PostJson<InventoryListItemDto>($"/api/inventory/{item.Id}/transfer-bin",
            new TransferBinRequest("B9"));
        moved.LocationBin.Should().Be("B9");

        var q = await PostJson<InventoryListItemDto>($"/api/inventory/{item.Id}/quarantine",
            new QuarantineStockRequest(2, "Suspected DOA"));
        q.Quarantined.Should().Be(2);
        q.Available.Should().Be(8);

        var backupResp = await _client.PostAsync("/api/backups", null);
        backupResp.EnsureSuccessStatusCode();
        var backup = (await backupResp.Content.ReadFromJsonAsync<BackupDto>())!;
        backup.Id.Should().NotBeEmpty();
        var restoreResp = await _client.PostAsync($"/api/backups/{backup.Id}/restore-test", null);
        restoreResp.EnsureSuccessStatusCode();
        var restore = (await restoreResp.Content.ReadFromJsonAsync<BackupRestoreTestDto>())!;
        restore.FileExists.Should().BeTrue();
        restore.BackupId.Should().Be(backup.Id);
    }

    [Fact]
    public async Task Printer_Batch_Labels()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var printer = await PostJson<PrinterProfileDto>("/api/printers",
            new UpsertPrinterProfileRequest(null, "Counter PDF", "PdfFallback", null, "part", true));
        printer.IsDefault.Should().BeTrue();

        var item = await PostJson<InventoryListItemDto>("/api/inventory", new UpsertInventoryRequest(
            null, "SKU-LBL", null, "Label part", "Parts", 1m, 5m, 3, 1, 2, null, null));
        var batch = await _client.PostAsJsonAsync("/api/labels/batch",
            new BatchLabelRequest("part", [item.Id]));
        batch.EnsureSuccessStatusCode();
        var html = await batch.Content.ReadAsStringAsync();
        html.Should().Contain("SKU-LBL");

        var health = await _client.GetFromJsonAsync<IntegrationHealthEventDto[]>("/api/integration-health");
        health.Should().NotBeNull();
    }

    private async Task EnsureSetupAndLoginAsync()
    {
        var setup = new SetupRequest(
            "V2 Shop", null, null, null, null, null, "QLD", null, null, true, 0.1m, "AUD", 110m,
            "Owner", "v2-owner@example.com", "Workshop!2026", "#0F766E");
        (await _client.PostAsJsonAsync("/api/setup", setup)).EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("v2-owner@example.com", "Workshop!2026"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    private async Task EnablePosAsync()
    {
        var posSettings = await _client.PutAsJsonAsync("/api/settings/pos", new UpdatePosSettingsRequest(
            true, "None", null, null, null, null, false, null));
        posSettings.EnsureSuccessStatusCode();
    }

    private async Task<T> PostJson<T>(string path, object body)
    {
        var response = await _client.PostAsJsonAsync(path, body);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"{(int)response.StatusCode} {path}: {err}");
        }
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
