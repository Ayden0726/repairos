using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Workshop;

namespace WorkshopOS.Api.Tests;

[Collection("api")]
public sealed class OperationsFlowTests
{
    private readonly WorkshopApiFactory _factory;
    private readonly HttpClient _client;

    public OperationsFlowTests(WorkshopApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Dashboard_Inventory_Quote_Report_Ai_Backup()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var dash = await _client.GetFromJsonAsync<DashboardDto>("/api/dashboard");
        dash.Should().NotBeNull();
        dash!.Cards.OpenJobs.Should().BeGreaterThanOrEqualTo(0);
        dash.Pipeline.Should().NotBeEmpty();

        var item = await PostJson<InventoryListItemDto>("/api/inventory", new UpsertInventoryRequest(
            null, "SKU-OLED-13", null, "iPhone 13 OLED", "Parts", 80m, 220m, 3, 2, 5, "A1", null));
        item.Sku.Should().Be("SKU-OLED-13");
        item.Available.Should().Be(3);

        var customer = await PostJson<CustomerDetailDto>("/api/customers", new UpsertCustomerRequest(
            null, CustomerType.Individual, "Ops", "Tester", null, "0400 111 222", "ops@example.com",
            null, "Melbourne", "VIC", "3000", null, PreferredContact.Email, false));

        var quote = await PostJson<QuoteDetailDto>("/api/quotes", new CreateQuoteRequest(
            customer.Id, null, "Screen replacement quote", null, null, null, null, null, null, null,
            null, SimpleLines: [new LineInputDto("PART", "OLED assembly", 1, 220m)]));
        quote.Number.Should().StartWith("QTE-");
        quote.Total.Should().BeGreaterThan(0);

        var modules = await _client.GetFromJsonAsync<JsonElement>("/api/settings/modules");
        modules.ValueKind.Should().Be(JsonValueKind.Array);

        var report = await _client.GetFromJsonAsync<ReportSummaryDto>("/api/reports/summary");
        report.Should().NotBeNull();

        var ai = await PostJson<AiAssistResponse>("/api/ai/assist", new AiAssistRequest("Suggest diagnosis for no power", null));
        ai.Enabled.Should().BeFalse();
        ai.Output.Should().Contain("disabled");

        var backupResponse = await _client.PostAsync("/api/backups", null);
        backupResponse.EnsureSuccessStatusCode();
        var backup = await backupResponse.Content.ReadFromJsonAsync<BackupDto>();
        backup.Should().NotBeNull();
        backup!.Status.Should().BeOneOf("Completed", "Running", "Failed");

        var backups = await _client.GetFromJsonAsync<BackupDto[]>("/api/backups");
        backups.Should().Contain(b => b.Id == backup.Id);
    }

    private async Task EnsureSetupAndLoginAsync()
    {
        var setup = new SetupRequest(
            "Ops Shop", null, null, null, null, null, "VIC", null, null, true, 0.1m, "AUD", 110m,
            "Owner", "ops-owner@example.com", "Workshop!2026", "#0F766E");
        (await _client.PostAsJsonAsync("/api/setup", setup)).EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("ops-owner@example.com", "Workshop!2026"));
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
