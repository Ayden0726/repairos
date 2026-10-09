using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Workshop;

namespace WorkshopOS.Api.Tests;

[Collection("api")]
public sealed class BookingFlowTests
{
    private readonly WorkshopApiFactory _factory;
    private readonly HttpClient _client;

    public BookingFlowTests(WorkshopApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_List_Update_Cancel_Booking()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var customer = await PostJson<CustomerDetailDto>("/api/customers", new UpsertCustomerRequest(
            null, CustomerType.Individual, "Cal", "endar", null, "0400 555 010", "cal@example.com",
            null, "Townsville", "QLD", "4810", null, PreferredContact.Phone, false));

        var start = DateTimeOffset.Now.Date.AddDays(1).AddHours(10);
        var end = start.AddHours(1);
        var created = await PostJson<BookingDto>("/api/bookings", new CreateBookingRequest(
            customer.Id, null, "Intake", start, end, "Screen drop-off"));
        created.CustomerName.Should().Contain("Cal");
        created.Status.Should().Be("Confirmed");
        created.Type.Should().Be("Intake");

        var listed = await _client.GetFromJsonAsync<BookingDto[]>(
            $"/api/bookings?from={Uri.EscapeDataString(start.AddHours(-1).ToString("o"))}&to={Uri.EscapeDataString(end.AddHours(1).ToString("o"))}");
        listed.Should().Contain(b => b.Id == created.Id);

        var got = await _client.GetFromJsonAsync<BookingDto>($"/api/bookings/{created.Id}");
        got!.Notes.Should().Be("Screen drop-off");

        var movedStart = start.AddHours(2);
        var movedEnd = movedStart.AddHours(1);
        var updated = await PutJson<BookingDto>($"/api/bookings/{created.Id}", new UpdateBookingRequest(
            customer.Id, null, "Diagnostic", "Confirmed", movedStart, movedEnd, "Rescheduled"));
        updated.Type.Should().Be("Diagnostic");
        updated.StartsAt.Should().BeCloseTo(movedStart, TimeSpan.FromSeconds(1));

        var cancelResponse = await _client.PostAsync($"/api/bookings/{created.Id}/cancel", null);
        cancelResponse.EnsureSuccessStatusCode();
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<BookingDto>();
        cancelled!.Status.Should().Be("Cancelled");
    }

    private async Task EnsureSetupAndLoginAsync()
    {
        var setup = new SetupRequest(
            "Calendar Shop", null, null, null, null, null, "QLD", null, null, true, 0.1m, "AUD", 110m,
            "Owner", "cal-owner@example.com", "Workshop!2026", "#0F766E");
        (await _client.PostAsJsonAsync("/api/setup", setup)).EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("cal-owner@example.com", "Workshop!2026"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    private async Task<T> PostJson<T>(string url, object body)
    {
        var response = await _client.PostAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<T> PutJson<T>(string url, object body)
    {
        var response = await _client.PutAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
