using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Operations;

namespace WorkshopOS.Api.Tests;

[Collection("api")]
public sealed class DashboardPreferencesTests
{
    private readonly WorkshopApiFactory _factory;
    private readonly HttpClient _client;

    public DashboardPreferencesTests(WorkshopApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Dashboard_Layout_Defaults_Then_Saves_Per_User()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var layout = await _client.GetFromJsonAsync<DashboardLayoutDto>("api/me/preferences/dashboard");
        layout.Should().NotBeNull();
        layout!.Widgets.Should().Contain(w => w.Id == DashboardWidgetIds.Calendar && w.Visible);
        layout.Widgets.Should().Contain(w => w.Id == DashboardWidgetIds.MyTickets);

        var reordered = layout.Widgets
            .OrderByDescending(w => w.Id == DashboardWidgetIds.Calendar)
            .ThenBy(w => w.Order)
            .Select((w, i) => w with
            {
                Visible = w.Id != DashboardWidgetIds.LowStock,
                Order = i
            })
            .ToList();

        var put = await _client.PutAsJsonAsync("api/me/preferences/dashboard",
            new UpdateDashboardLayoutRequest(reordered));
        put.EnsureSuccessStatusCode();
        var saved = await put.Content.ReadFromJsonAsync<DashboardLayoutDto>();
        saved!.Widgets.First().Id.Should().Be(DashboardWidgetIds.Calendar);
        saved.Widgets.Single(w => w.Id == DashboardWidgetIds.LowStock).Visible.Should().BeFalse();

        var again = await _client.GetFromJsonAsync<DashboardLayoutDto>("api/me/preferences/dashboard");
        again!.Widgets.First().Id.Should().Be(DashboardWidgetIds.Calendar);
        again.Widgets.Single(w => w.Id == DashboardWidgetIds.LowStock).Visible.Should().BeFalse();
    }

    private async Task EnsureSetupAndLoginAsync()
    {
        var setup = new SetupRequest(
            "Prefs Shop", null, null, null, null, null, "QLD", null, null, true, 0.1m, "AUD", 110m,
            "Owner", "prefs-owner@example.com", "Workshop!2026", "#0F766E");
        (await _client.PostAsJsonAsync("/api/setup", setup)).EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("prefs-owner@example.com", "Workshop!2026"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }
}
