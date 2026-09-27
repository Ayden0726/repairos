using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Common;

namespace WorkshopOS.Api.Tests;

[Collection("api")]
public sealed class RolesAndSystemTests
{
    private readonly WorkshopApiFactory _factory;
    private readonly HttpClient _client;

    public RolesAndSystemTests(WorkshopApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Default_Roles_Seeded_And_Custom_Role_Crud()
    {
        await _factory.ResetDatabaseAsync();

        var setup = new SetupRequest(
            "Roles Shop",
            null, null, null, null, null, "VIC", null, null,
            true, 0.10m, "AUD", 110m,
            "Owner", "owner@roles.test", "Workshop!2026", null);
        (await _client.PostAsJsonAsync("/api/setup", setup)).EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("owner@roles.test", "Workshop!2026"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var roles = await _client.GetFromJsonAsync<List<RoleDto>>("/api/roles");
        roles.Should().NotBeNull();
        roles!.Select(r => r.Key).Should().Contain(["owner", "administrator", "manager", "technician", "front_desk", "sales", "read_only"]);

        var perms = await _client.GetFromJsonAsync<List<PermissionDto>>("/api/permissions");
        perms.Should().NotBeEmpty();

        var create = await _client.PostAsJsonAsync("/api/roles", new CreateRoleRequest(
            "custom_tech_lead", "Tech Lead", "Custom lead role",
            ["tickets.view", "tickets.edit", "pricing.view"]));
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<RoleDto>();
        created!.Key.Should().Be("custom_tech_lead");
        created.Permissions.Should().Contain("tickets.view");
        created.IsSystem.Should().BeFalse();

        var update = await _client.PutAsJsonAsync($"/api/roles/{created.Id}",
            new UpdateRoleRequest("Tech Lead+", "Updated", ["tickets.view", "customers.view"]));
        update.EnsureSuccessStatusCode();
        var updated = await update.Content.ReadFromJsonAsync<RoleDto>();
        updated!.Name.Should().Be("Tech Lead+");
        updated.Permissions.Should().BeEquivalentTo(["customers.view", "tickets.view"]);

        var health = await _client.GetFromJsonAsync<HealthDto>("/api/health");
        health!.ApiVersion.Should().Be(ProductVersions.Api);

        var info = await _client.GetFromJsonAsync<SystemInfoDto>("/api/system/info");
        info!.ApiVersion.Should().Be(ProductVersions.Api);
        info.UpdateCommands.Should().NotBeEmpty();

        var restart = await _client.PostAsync("/api/system/restart", null);
        restart.EnsureSuccessStatusCode();
        var restartDto = await restart.Content.ReadFromJsonAsync<RestartResultDto>();
        restartDto!.Restarted.Should().BeFalse();
        restartDto.Commands.Should().NotBeEmpty();
    }
}
