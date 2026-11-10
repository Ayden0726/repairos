using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Common;

namespace WorkshopOS.Api.Tests;

[Collection("api")]
public sealed class HomeWorkshopFlowTests
{
    private readonly WorkshopApiFactory _factory;
    private readonly HttpClient _client;

    public HomeWorkshopFlowTests(WorkshopApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Setup_Home_Applies_Slim_Modules_And_Style()
    {
        await _factory.ResetDatabaseAsync();

        var setup = new SetupRequest(
            "Bench at Home", null, null, null, null, null, "QLD", null, null, true, 0.1m, "AUD", 90m,
            "Ayden", "home-owner@example.com", "Workshop!2026", "#0F766E",
            WorkshopStyles.Home, true);
        (await _client.PostAsJsonAsync("/api/setup", setup)).EnsureSuccessStatusCode();

        await LoginAsync("home-owner@example.com");

        var style = await _client.GetFromJsonAsync<WorkshopStyleSettingsDto>("/api/settings/workshop-style");
        style!.Style.Should().Be(WorkshopStyles.Home);
        style.ShowBeginnerTips.Should().BeTrue();

        var modules = await _client.GetFromJsonAsync<ModuleDto[]>("/api/settings/modules");
        modules.Should().NotBeNull();
        modules!.Single(m => m.Key == "dashboard").Title.Should().Be("Home");
        modules.Single(m => m.Key == "inventory").Title.Should().Be("Parts");
        modules.Single(m => m.Key == "reports").Title.Should().Be("Money");
        modules.Single(m => m.Key == "repairs").Visible.Should().BeTrue();
        modules.Single(m => m.Key == "pos").Visible.Should().BeFalse();
        modules.Single(m => m.Key == "calendar").Visible.Should().BeFalse();
        modules.Single(m => m.Key == "users").Visible.Should().BeFalse();
        modules.Single(m => m.Key == "fitment").Visible.Should().BeFalse();
    }

    [Fact]
    public async Task Toggle_FullShop_Restores_Modules_And_Money_Endpoint_Works()
    {
        await _factory.ResetDatabaseAsync();

        var setup = new SetupRequest(
            "Toggle Shop", null, null, null, null, null, "QLD", null, null, true, 0.1m, "AUD", 110m,
            "Owner", "toggle-owner@example.com", "Workshop!2026", "#0F766E",
            WorkshopStyles.Home, true);
        (await _client.PostAsJsonAsync("/api/setup", setup)).EnsureSuccessStatusCode();
        await LoginAsync("toggle-owner@example.com");

        var templates = await _client.GetFromJsonAsync<JobTemplateDto[]>("/api/settings/job-templates");
        templates!.Should().NotBeEmpty();
        templates.Should().Contain(t => t.Key == "phone_screen");

        var money = await _client.GetFromJsonAsync<HomeMoneySummaryDto>("/api/settings/home/money");
        money!.Currency.Should().Be("AUD");
        money.PeriodLabel.Should().NotBeNullOrWhiteSpace();
        money.OpenJobs.Should().BeGreaterThanOrEqualTo(0);

        var updated = await PostJson<WorkshopStyleSettingsDto>("/api/settings/workshop-style",
            new UpdateWorkshopStyleRequest(WorkshopStyles.FullShop, false));
        updated.Style.Should().Be(WorkshopStyles.FullShop);
        updated.ShowBeginnerTips.Should().BeFalse();

        var modules = await _client.GetFromJsonAsync<ModuleDto[]>("/api/settings/modules");
        modules!.Single(m => m.Key == "calendar").Visible.Should().BeTrue();
        modules.Single(m => m.Key == "inventory").Title.Should().Be("Inventory");
        modules.Single(m => m.Key == "reports").Title.Should().Be("Reports");
        modules.Single(m => m.Key == "pos").Visible.Should().BeFalse(); // still off until enabled
    }

    private async Task LoginAsync(string email)
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Workshop!2026"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    private async Task<T> PostJson<T>(string url, object body)
    {
        var res = await _client.PutAsJsonAsync(url, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }
}
