using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Workshop;

namespace WorkshopOS.Api.Tests;

[Collection("api")]
public sealed class RepairFlowTests
{
    private readonly WorkshopApiFactory _factory;
    private readonly HttpClient _client;

    public RepairFlowTests(WorkshopApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Customer_Device_Repair_Status_Timeline()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var customer = await PostJson<CustomerDetailDto>("/api/customers", new UpsertCustomerRequest(
            null, CustomerType.Individual, "Nora", "Patterson", null, "0412 000 111", "nora@example.com",
            null, "Melbourne", "VIC", "3000", null, PreferredContact.Sms, false));
        customer.DisplayName.Should().Be("Nora Patterson");

        var device = await PostJson<DeviceListItemDto>("/api/devices", new UpsertDeviceRequest(
            null, customer.Id, DeviceCategory.Phone, "Apple", "iPhone 13 Pro Max", null, "Graphite", null, "356938035643809", "256GB", null));
        device.Label.Should().Contain("iPhone 13");

        var lookups = await GetJson<RepairLookupsDto>("/api/repairs/lookups");
        lookups.Statuses.Should().NotBeEmpty();
        var diagnosing = lookups.Statuses.First(s => s.Key == "diagnosing");

        var repair = await PostJson<RepairDetailDto>("/api/repairs", new CreateRepairRequest(
            customer.Id, device.Id, lookups.Types.First(t => t.Key == "repair").Id,
            lookups.Priorities.First(p => p.Key == "urgent").Id, null,
            "Cracked OLED, Face ID still works", "Front glass smashed", true, true, false, true,
            true, false, true, true, "2580", 429m, 100m, DateTimeOffset.UtcNow.AddHours(-1),
            null, null, null, null, null));

        repair.TicketNumber.Should().StartWith("REP-");
        repair.Timeline.Should().Contain(e => e.EventType == "repair.created");
        repair.HasPasscode.Should().BeTrue();

        var listed = await GetJson<PagedResult<RepairListItemDto>>("/api/repairs?overdue=true");
        listed.Items.Should().Contain(i => i.Id == repair.Id && i.IsOverdue);

        var updated = await PostJson<RepairDetailDto>($"/api/repairs/{repair.Id}/status", new ChangeStatusRequest(diagnosing.Id));
        updated.StatusKey.Should().Be("diagnosing");
        updated.Timeline.Should().Contain(e => e.EventType == "repair.status");

        await PostJson<RepairDetailDto>($"/api/repairs/{repair.Id}/diagnosis", new UpdateDiagnosisRequest("Display assembly failed", "Replace OLED"));
        var note = await PostJson<RepairNoteDto>($"/api/repairs/{repair.Id}/notes", new AddNoteRequest("Customer approved screen replacement", false));
        note.Body.Should().Contain("approved");

        var search = await GetJson<JsonElement>("/api/search?q=Nora");
        search.GetProperty("groups").EnumerateArray().SelectMany(g => g.GetProperty("hits").EnumerateArray()).Should().NotBeEmpty();
    }

    private async Task EnsureSetupAndLoginAsync()
    {
        var setup = new SetupRequest(
            "Phase2 Shop", null, null, null, null, null, "VIC", null, null, true, 0.1m, "AUD", 110m,
            "Owner", "phase2@example.com", "Workshop!2026", "#0F766E");
        (await _client.PostAsJsonAsync("/api/setup", setup)).EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("phase2@example.com", "Workshop!2026"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    private async Task<T> GetJson<T>(string url)
    {
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<T> PostJson<T>(string url, object body)
    {
        var response = await _client.PostAsJsonAsync(url, body);
        var bodyText = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(bodyText);
        return JsonSerializer.Deserialize<T>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
}
