using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Operations;

namespace WorkshopOS.Api.Tests;

[Collection("api")]
public sealed class V2PlatformFlowTests
{
    private readonly WorkshopApiFactory _factory;
    private readonly HttpClient _client;

    public V2PlatformFlowTests(WorkshopApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Fitment_Upsert_List_And_Delete()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var created = await PostJson<PartFitmentDto>("/api/fitment", new UpsertPartFitmentRequest(
            null, null, "SCR-IP12", "OLED screen", "Apple", "iPhone 12", "Phone", "OEM grade"));
        created.PartSku.Should().Be("SCR-IP12");
        created.Brand.Should().Be("Apple");
        created.Model.Should().Be("iPhone 12");
        created.IsVerified.Should().BeTrue();

        var list = await _client.GetFromJsonAsync<PartFitmentDto[]>("/api/fitment?brand=Apple&model=iPhone%2012");
        list!.Should().Contain(f => f.Id == created.Id && f.PartName == "OLED screen");

        var del = await _client.DeleteAsync($"/api/fitment/{created.Id}");
        del.EnsureSuccessStatusCode();
        list = await _client.GetFromJsonAsync<PartFitmentDto[]>("/api/fitment?brand=Apple");
        list!.Should().NotContain(f => f.Id == created.Id);
    }

    [Fact]
    public async Task Sync_Push_Acks_And_Deduplicates()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var mutationId = Guid.NewGuid();
        var push = await PostJson<SyncPushResponse>("/api/sync/push", new SyncPushRequest(
        [
            new SyncMutationDto(mutationId, "POST", "/api/customers", "{\"firstName\":\"Offline\"}")
        ]));
        push.Results.Should().ContainSingle();
        push.Results[0].Status.Should().Be("Applied");
        push.Results[0].ServerEntryId.Should().NotBeNull();

        var again = await PostJson<SyncPushResponse>("/api/sync/push", new SyncPushRequest(
        [
            new SyncMutationDto(mutationId, "POST", "/api/customers", "{\"firstName\":\"Offline\"}")
        ]));
        again.Results[0].Status.Should().Be("Duplicate");

        var rejected = await PostJson<SyncPushResponse>("/api/sync/push", new SyncPushRequest(
        [
            new SyncMutationDto(Guid.NewGuid(), "POST", "/api/admin/danger", null)
        ]));
        rejected.Results[0].Status.Should().Be("Rejected");

        var outbox = await _client.GetFromJsonAsync<SyncOutboxListItemDto[]>("/api/sync/outbox");
        outbox!.Should().Contain(e => e.ClientMutationId == mutationId && e.Status == "Applied");
    }

    [Fact]
    public async Task Locations_And_Inventory_Bins()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        // Setup seeds one default location — add additional multi-site rows on top.
        var before = await _client.GetFromJsonAsync<WorkshopLocationDto[]>("/api/locations");
        before!.Should().NotBeEmpty();

        var site = await PostJson<WorkshopLocationDto>("/api/locations", new UpsertWorkshopLocationRequest(
            null, "Townsville bench", null, null, "12 Flinders St", "Townsville", "QLD", "4810", true));
        site.Name.Should().Be("Townsville bench");
        site.IsDefault.Should().BeTrue();
        site.Suburb.Should().Be("Townsville");

        var second = await PostJson<WorkshopLocationDto>("/api/locations", new UpsertWorkshopLocationRequest(
            null, "Mobile van", null, null, null, "Cairns", "QLD", null, true));
        second.IsDefault.Should().BeTrue();

        var sites = await _client.GetFromJsonAsync<WorkshopLocationDto[]>("/api/locations");
        sites!.Should().HaveCount(before.Length + 2);
        sites.Single(s => s.Id == site.Id).IsDefault.Should().BeFalse();
        sites.Single(s => s.Id == second.Id).IsDefault.Should().BeTrue();
        sites.Count(s => s.IsDefault).Should().Be(1);

        var bin = await PostJson<InventoryBinDto>("/api/inventory-bins", new UpsertInventoryBinRequest(
            null, second.Id, null, "A-01", "Front drawer", "Fast movers"));
        bin.Code.Should().Be("A-01");
        bin.LocationId.Should().Be(second.Id);
        bin.LocationName.Should().Be("Mobile van");

        var bins = await _client.GetFromJsonAsync<InventoryBinDto[]>($"/api/inventory-bins?locationId={second.Id}");
        bins!.Should().ContainSingle(b => b.Id == bin.Id);
    }

    private async Task EnsureSetupAndLoginAsync()
    {
        var setup = new SetupRequest(
            "Platform Shop", null, null, null, null, null, "QLD", null, null, true, 0.1m, "AUD", 110m,
            "Owner", "platform-owner@example.com", "Workshop!2026", "#0F766E");
        (await _client.PostAsJsonAsync("/api/setup", setup)).EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("platform-owner@example.com", "Workshop!2026"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
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
