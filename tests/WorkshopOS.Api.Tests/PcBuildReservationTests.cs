using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Common;

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
    public async Task Custom_Pc_Builds_Module_Is_Removed()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var list = await _client.GetAsync("/api/builds");
        ((int)list.StatusCode).Should().Be(410);

        var modules = await _client.GetFromJsonAsync<ModuleDto[]>("/api/settings/modules");
        modules.Should().NotContain(m => m.Key == "builds");
        modules.Should().Contain(m => m.Key == "repairs");
        modules.Should().Contain(m => m.Key == "ai");
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
}
