using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Infrastructure.Persistence;

namespace WorkshopOS.Api.Tests;

[Collection("api")]
public sealed class AuthFlowTests
{
    private readonly WorkshopApiFactory _factory;
    private readonly HttpClient _client;

    public AuthFlowTests(WorkshopApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_Returns_Database_Ready()
    {
        var response = await _client.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("database").GetBoolean().Should().BeTrue();
        json.GetProperty("status").GetString().Should().Be("Healthy");
    }

    [Fact]
    public async Task Setup_Then_Login_And_Me()
    {
        await _factory.ResetDatabaseAsync();

        var status = await _client.GetFromJsonAsync<SetupStatusDto>("/api/setup/status");
        status!.IsComplete.Should().BeFalse();

        var setup = new SetupRequest(
            "Riverside Tech",
            "(03) 9000 0000",
            "hello@example.com",
            "12 345 678 901",
            "1 Test St",
            "Melbourne",
            "VIC",
            "3000",
            "https://example.com",
            true,
            0.10m,
            "AUD",
            110m,
            "Maya Owner",
            "owner@example.com",
            "Workshop!2026",
            "#0F766E");

        (await _client.PostAsJsonAsync("/api/setup", setup)).EnsureSuccessStatusCode();
        (await _client.GetFromJsonAsync<SetupStatusDto>("/api/setup/status"))!.IsComplete.Should().BeTrue();

        var duplicate = await _client.PostAsJsonAsync("/api/setup", setup);
        duplicate.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);

        var badPassword = setup with { OwnerEmail = "other@example.com", OwnerPassword = "short" };
        // setup already complete — conflict, not validation
        (await _client.PostAsJsonAsync("/api/setup", badPassword)).StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);

        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("owner@example.com", "Workshop!2026"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.User.IsOwner.Should().BeTrue();
        auth.User.Business!.Name.Should().Be("Riverside Tech");
        auth.User.Permissions.Should().Contain("settings.manage");

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var meResponse = await _client.SendAsync(meRequest);
        meResponse.EnsureSuccessStatusCode();
        var me = await meResponse.Content.ReadFromJsonAsync<UserDto>();
        me!.Email.Should().Be("owner@example.com");

        using var modulesRequest = new HttpRequestMessage(HttpMethod.Get, "/api/settings/modules");
        modulesRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var modules = await (await _client.SendAsync(modulesRequest)).Content.ReadFromJsonAsync<JsonElement>();
        modules.ValueKind.Should().Be(JsonValueKind.Array);
    }
}

public sealed class PasswordRulesTests
{
    [Theory]
    [InlineData("short", false)]
    [InlineData("Workshop!2026", true)]
    [InlineData("alllowercase1", false)]
    public void Password_Strength(string password, bool ok)
    {
        var result = WorkshopOS.Application.Common.PasswordRules.Validate(password);
        (result is null).Should().Be(ok);
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<WorkshopApiFactory> { }

public sealed class WorkshopApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string Database = "workshopos_api_tests";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default",
            $"Host=127.0.0.1;Port=5432;Database={Database};Username=workshopos;Password=workshopos_dev");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-workshopos-32chars-min!!");
        builder.UseSetting("Seed:Demo", "false");
        builder.UseEnvironment("Development");
    }

    public async Task InitializeAsync()
    {
        await EnsureDatabaseExistsAsync();
        await ResetDatabaseAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkshopDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await DbSeed.EnsureFoundationAsync(db);
    }

    private static async Task EnsureDatabaseExistsAsync()
    {
        await using var conn = new Npgsql.NpgsqlConnection(
            "Host=127.0.0.1;Port=5432;Database=postgres;Username=workshopos;Password=workshopos_dev");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT 1 FROM pg_database WHERE datname = '{Database}'";
        var exists = await cmd.ExecuteScalarAsync();
        if (exists is null)
        {
            await using var create = conn.CreateCommand();
            create.CommandText = $"CREATE DATABASE {Database} OWNER workshopos";
            await create.ExecuteNonQueryAsync();
        }
    }

    async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();
}
