using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Workshop;

namespace WorkshopOS.Api.Tests;

[Collection("api")]
public sealed class PcBuildReservationTests
{
    private readonly WorkshopApiFactory _factory;
    private readonly HttpClient _client;

    public PcBuildReservationTests(WorkshopApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Build_Reserves_Exclusively_And_Repair_Cannot_Take_Same_Stock()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var cpu = await PostJson<InventoryListItemDto>("/api/inventory", new UpsertInventoryRequest(
            null, "CPU-7950X", null, "Ryzen 9 7950X", "Parts", 500m, 750m, 1, 0, 1, null, null, "CPU"));
        cpu.Available.Should().Be(1);
        cpu.ComponentType.Should().Be("CPU");

        var customer = await PostJson<CustomerDetailDto>("/api/customers", new UpsertCustomerRequest(
            null, CustomerType.Individual, "Build", "Buyer", null, "0400 222 333", "build@example.com",
            null, "Melbourne", "VIC", "3000", null, PreferredContact.Email, false));

        var build = await PostJson<PcBuildDetailDto>("/api/builds", new CreatePcBuildRequest(
            customer.Id, "Gaming rig", "Gaming", 2500m, null,
            [new PcPartInputDto("CPU", "Ryzen 9 7950X", cpu.Id, 500m, 750m, 1)]));
        build.Status.Should().Be("Reserved");
        build.Parts.Should().ContainSingle(p => p.InventoryItemId == cpu.Id && p.ReservationId.HasValue);
        build.CostTotal.Should().Be(500m);
        build.SellTotal.Should().Be(750m);

        var afterReserve = (await _client.GetFromJsonAsync<InventoryListItemDto[]>("/api/inventory"))!
            .First(i => i.Id == cpu.Id);
        afterReserve.Available.Should().Be(0);
        afterReserve.Reserved.Should().Be(1);

        // Second build cannot reserve the same unit
        var conflict = await _client.PostAsJsonAsync("/api/builds", new CreatePcBuildRequest(
            customer.Id, "Second build", null, null, null,
            [new PcPartInputDto("CPU", "Ryzen 9 7950X", cpu.Id, 500m, 750m, 1)]));
        conflict.IsSuccessStatusCode.Should().BeFalse();
        var conflictBody = await conflict.Content.ReadAsStringAsync();
        conflictBody.Should().Contain("available");

        // Repair reservation also blocked
        var device = await PostJson<DeviceListItemDto>("/api/devices", new UpsertDeviceRequest(
            null, customer.Id, DeviceCategory.Desktop, "Custom", "PC", null, null, null, null, null, null));
        var lookups = await _client.GetFromJsonAsync<RepairLookupsDto>("/api/repairs/lookups");
        var repair = await PostJson<RepairDetailDto>("/api/repairs", new CreateRepairRequest(
            customer.Id, device.Id, lookups!.Types.First().Id, lookups.Priorities.First().Id, null,
            "No boot", null, false, false, false, false, false, false, false, false, null, null, null,
            null, null, null, null, null, null));

        var repairReserve = await _client.PostAsJsonAsync("/api/inventory/reserve",
            new ReserveStockRequest(cpu.Id, 1, repair.Id, null));
        repairReserve.IsSuccessStatusCode.Should().BeFalse();

        // Cancel build releases stock
        (await _client.DeleteAsync($"/api/builds/{build.Id}")).EnsureSuccessStatusCode();
        var afterRelease = (await _client.GetFromJsonAsync<InventoryListItemDto[]>("/api/inventory"))!
            .First(i => i.Id == cpu.Id);
        afterRelease.Available.Should().Be(1);
        afterRelease.Reserved.Should().Be(0);
    }

    [Fact]
    public async Task Build_Complete_Consumes_Stock()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var ram = await PostJson<InventoryListItemDto>("/api/inventory", new UpsertInventoryRequest(
            null, "RAM-32", null, "32GB DDR5", "Parts", 80m, 140m, 2, 0, 1, null, null, "RAM"));

        var build = await PostJson<PcBuildDetailDto>("/api/builds", new CreatePcBuildRequest(
            null, "RAM kit", null, null, null,
            [
                new PcPartInputDto("RAM", "Kit A", ram.Id, 80m, 140m, 1),
                new PcPartInputDto("RAM", "Kit B", ram.Id, 80m, 140m, 1)
            ]));
        build.Parts.Should().HaveCount(2);

        var mid = (await _client.GetFromJsonAsync<InventoryListItemDto[]>("/api/inventory"))!
            .First(i => i.Id == ram.Id);
        mid.Available.Should().Be(0);
        mid.Reserved.Should().Be(2);
        mid.OnHand.Should().Be(2);

        var completed = await PostJson<PcBuildDetailDto>($"/api/builds/{build.Id}/status",
            new UpdatePcBuildStatusRequest("Completed"));
        completed.Status.Should().Be("Completed");

        var done = (await _client.GetFromJsonAsync<InventoryListItemDto[]>("/api/inventory"))!
            .First(i => i.Id == ram.Id);
        done.OnHand.Should().Be(0);
        done.Reserved.Should().Be(0);
        done.Available.Should().Be(0);
    }

    [Fact]
    public async Task Inventory_Filter_By_ComponentType()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        await PostJson<InventoryListItemDto>("/api/inventory", new UpsertInventoryRequest(
            null, "GPU-1", null, "RTX 4070", "Parts", 500m, 800m, 1, 0, 1, null, null, "GPU"));
        await PostJson<InventoryListItemDto>("/api/inventory", new UpsertInventoryRequest(
            null, "CPU-1", null, "i5-14600K", "Parts", 250m, 400m, 1, 0, 1, null, null, "CPU"));

        var gpus = await _client.GetFromJsonAsync<InventoryListItemDto[]>("/api/inventory?componentType=GPU");
        gpus.Should().ContainSingle(i => i.Sku == "GPU-1");
        gpus.Should().NotContain(i => i.Sku == "CPU-1");
    }

    private async Task EnsureSetupAndLoginAsync()
    {
        var setup = new SetupRequest(
            "Build Shop", null, null, null, null, null, "VIC", null, null, true, 0.1m, "AUD", 110m,
            "Owner", "build-owner@example.com", "Workshop!2026", "#0F766E");
        (await _client.PostAsJsonAsync("/api/setup", setup)).EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("build-owner@example.com", "Workshop!2026"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    private async Task<T> PostJson<T>(string url, object body)
    {
        var response = await _client.PostAsJsonAsync(url, body);
        var text = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(text);
        return JsonSerializer.Deserialize<T>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
}
