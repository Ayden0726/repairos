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
        var created = await create.Content.ReadFromJsonAsync<CreateStaffUserResponse>();
        created!.User.Email.Should().Be("tech@staff.test");
        created.User.RoleKey.Should().Be("technician");
        created.User.MustResetPassword.Should().BeTrue();
        created.TemporaryPassword.Should().Be("Workshop!2026");

        var list = await _client.GetFromJsonAsync<List<StaffUserDto>>("/api/users");
        list!.Should().HaveCount(2);
        list.Should().Contain(u => u.Email == "tech@staff.test" && u.MustResetPassword);

        var front = roles!.First(r => r.Key == "front_desk");
        var update = await _client.PutAsJsonAsync($"/api/users/{created.User.Id}",
            new UpdateStaffUserRequest(null, front.Key, "Suspended", null));
        update.EnsureSuccessStatusCode();
        var updated = await update.Content.ReadFromJsonAsync<StaffUserDto>();
        updated!.RoleKey.Should().Be("front_desk");
        updated.Status.Should().Be("Suspended");
    }

    [Fact]
    public async Task New_Staff_Gets_Temp_Password_And_Must_Change_On_First_Login()
    {
        await _factory.ResetDatabaseAsync();

        var setup = new SetupRequest(
            "Temp Shop",
            null, null, null, null, null, "VIC", null, null,
            true, 0.10m, "AUD", 110m,
            "Owner", "owner@temp.test", "Workshop!2026", null);
        (await _client.PostAsJsonAsync("/api/setup", setup)).EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("owner@temp.test", "Workshop!2026"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var roles = await _client.GetFromJsonAsync<List<RoleDto>>("/api/roles");
        var tech = roles!.First(r => r.Key == "technician");

        var create = await _client.PostAsJsonAsync("/api/users", new CreateStaffUserRequest(
            "tech@temp.test", "Temp Tech", null, tech.Key, null));
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<CreateStaffUserResponse>();
        created!.TemporaryPassword.Should().NotBeNullOrWhiteSpace();
        created.User.MustResetPassword.Should().BeTrue();

        var techLogin = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("tech@temp.test", created.TemporaryPassword));
        techLogin.EnsureSuccessStatusCode();
        var techAuth = await techLogin.Content.ReadFromJsonAsync<AuthResponse>();
        techAuth!.User.MustResetPassword.Should().BeTrue();

        using var techClient = _factory.CreateClient();
        techClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", techAuth.AccessToken);

        var change = await techClient.PutAsJsonAsync("/api/auth/password",
            new ChangePasswordRequest(created.TemporaryPassword, "Workshop!2028"));
        change.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);

        var after = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("tech@temp.test", "Workshop!2028"));
        after.EnsureSuccessStatusCode();
        var afterAuth = await after.Content.ReadFromJsonAsync<AuthResponse>();
        afterAuth!.User.MustResetPassword.Should().BeFalse();
    }

    [Fact]
    public async Task Owner_Can_Reset_Staff_Password_And_User_Can_Change_Own()
    {
        await _factory.ResetDatabaseAsync();

        var setup = new SetupRequest(
            "Pwd Shop",
            null, null, null, null, null, "VIC", null, null,
            true, 0.10m, "AUD", 110m,
            "Owner", "owner@pwd.test", "Workshop!2026", null);
        (await _client.PostAsJsonAsync("/api/setup", setup)).EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("owner@pwd.test", "Workshop!2026"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var roles = await _client.GetFromJsonAsync<List<RoleDto>>("/api/roles");
        var tech = roles!.First(r => r.Key == "technician");
        var create = await _client.PostAsJsonAsync("/api/users", new CreateStaffUserRequest(
            "tech@pwd.test", "Tech", "Workshop!2026", tech.Key, null));
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<CreateStaffUserResponse>();

        var reset = await _client.PutAsJsonAsync($"/api/users/{created!.User.Id}/password",
            new ResetStaffPasswordRequest("Workshop!2027"));
        reset.EnsureSuccessStatusCode();
        var resetBody = await reset.Content.ReadFromJsonAsync<CreateStaffUserResponse>();
        resetBody!.TemporaryPassword.Should().Be("Workshop!2027");
        resetBody.User.MustResetPassword.Should().BeTrue();

        var techLogin = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("tech@pwd.test", "Workshop!2027"));
        techLogin.EnsureSuccessStatusCode();
        var techAuth = await techLogin.Content.ReadFromJsonAsync<AuthResponse>();
        techAuth!.User.MustResetPassword.Should().BeTrue();

        using var techClient = _factory.CreateClient();
        techClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", techAuth.AccessToken);

        var change = await techClient.PutAsJsonAsync("/api/auth/password",
            new ChangePasswordRequest("Workshop!2027", "Workshop!2028"));
        change.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);

        (await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("tech@pwd.test", "Workshop!2028"))).EnsureSuccessStatusCode();

        var badCurrent = await techClient.PutAsJsonAsync("/api/auth/password",
            new ChangePasswordRequest("wrong-password", "Workshop!2029"));
        badCurrent.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }
}
