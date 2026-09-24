using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using WorkshopOS.Contracts.Common;

namespace WorkshopOS.Api.Tests;

[Collection("api")]
public sealed class DiscoveryFlowTests
{
    private readonly WorkshopApiFactory _factory;
    private readonly HttpClient _client;

    public DiscoveryFlowTests(WorkshopApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Discovery_Returns_Pairing_Code_And_Suggested_Urls()
    {
        await _factory.ResetDatabaseAsync();

        var response = await _client.GetAsync("/api/discovery");
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<DiscoveryDto>();
        dto.Should().NotBeNull();
        dto!.Product.Should().Be("WorkshopOS");
        dto.PairingCode.Should().MatchRegex(@"^WOS-[A-Z2-9]{4}$");
        dto.SetupComplete.Should().BeFalse();
        dto.SuggestedUrls.Should().NotBeEmpty();
        dto.ApiVersion.Should().NotBeNullOrWhiteSpace();

        // Stable across calls until regenerated
        var again = await _client.GetFromJsonAsync<DiscoveryDto>("/api/discovery");
        again!.PairingCode.Should().Be(dto.PairingCode);
    }

    [Fact]
    public async Task Connect_Portal_Returns_Html_With_Pairing_Code()
    {
        await _factory.ResetDatabaseAsync();

        var discovery = await _client.GetFromJsonAsync<DiscoveryDto>("/api/discovery");
        var response = await _client.GetAsync("/connect");
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain(discovery!.PairingCode);
        html.Should().Contain("WorkshopOS");
        html.Should().Contain("/api/discovery");
    }

    [Fact]
    public async Task Discovery_Json_Uses_CamelCase_Property_Names()
    {
        var response = await _client.GetAsync("/api/discovery");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.TryGetProperty("pairingCode", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("setupComplete", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("suggestedUrls", out _).Should().BeTrue();
    }
}
