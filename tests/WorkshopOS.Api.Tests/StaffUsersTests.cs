using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Common;

namespace WorkshopOS.Api.Tests;

[Collection("api")]
public sealed class StaffUsersTests
{
    private readonly WorkshopApiFactory _factory;
    private readonly HttpClient _client;

    public StaffUsersTests(WorkshopApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Owner_Can_List_Create_And_Update_Staff()
    {
        await _factory.ResetDatabaseAsync();

        var setup = new SetupRequest(
            "Staff Shop",
            null, null, null, null, null, "VIC", null, null,
            true, 0.10m, "AUD", 110m,
            "Owner", "owner@staff.test", "Workshop!2026", null);
        (await _client.PostAsJsonAsync("/api/setup", setup)).EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("owner@staff.test", "Workshop!2026"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var roles = await _client.GetFromJsonAsync<List<RoleDto>>("/api/roles");
        roles.Should().NotBeEmpty();
        var tech = roles!.First(r => r.Key == "technician");

        var before = await _client.GetFromJsonAsync<List<StaffUserDto>>("/api/users");
        before!.Should().ContainSingle(u => u.IsOwner);

        var create = await _client.PostAsJsonAsync("/api/users", new CreateStaffUserRequest(
            "tech@staff.test", "Alex Tech", "Workshop!2026", tech.Key, null));
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<StaffUserDto>();
        created!.Email.Should().Be("tech@staff.test");
        created.RoleKey.Should().Be("technician");

        var list = await _client.GetFromJsonAsync<List<StaffUserDto>>("/api/users");
        list!.Should().HaveCount(2);

        var front = roles.First(r => r.Key == "front_desk");
        var update = await _client.PutAsJsonAsync($"/api/users/{created.Id}",
            new UpdateStaffUserRequest(null, front.Key, "Suspended", null));
        update.EnsureSuccessStatusCode();
        var updated = await update.Content.ReadFromJsonAsync<StaffUserDto>();
        updated!.RoleKey.Should().Be("front_desk");
        updated.Status.Should().Be("Suspended");
    }
}
