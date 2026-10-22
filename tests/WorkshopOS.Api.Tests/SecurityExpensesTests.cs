using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Common;
using WorkshopOS.Contracts.Finance;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Infrastructure.Persistence;
using WorkshopOS.Infrastructure.Security;
using WorkshopOS.Infrastructure.Services;

namespace WorkshopOS.Api.Tests;

[Collection("api")]
public sealed class SecurityExpensesTests
{
    private readonly WorkshopApiFactory _factory;
    private readonly HttpClient _client;

    public SecurityExpensesTests(WorkshopApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public void SecretProtector_RoundTrip_And_Mask()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Key"] = "unit-test-encryption-key-at-least-32-chars!!"
            })
            .Build();
        var protector = new SecretProtector(config);
        var cipher = protector.Encrypt("""{"apiKey":"sk-live-abcdef123456"}""");
        cipher.Should().StartWith("v1:");
        cipher.Should().NotContain("sk-live");
        protector.Decrypt(cipher).Should().Contain("sk-live-abcdef123456");
        protector.TryDecrypt(cipher, out var plain, out var err).Should().BeTrue();
        plain.Should().Contain("abcdef");
        err.Should().BeNull();

        var bad = new SecretProtector(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Key"] = "totally-different-encryption-key-32chars++"
            }).Build());
        bad.TryDecrypt(cipher, out _, out var fail).Should().BeFalse();
        fail.Should().NotBeNullOrWhiteSpace();
        fail.Should().NotContain("sk-live");

        SecretProtector.MaskSecret("sk-live-abcdef123456").Should().EndWith("3456");
    }

    [Fact]
    public void Gst_Inclusive_Exclusive_And_Free()
    {
        var inclusive = ExpenseService.CalculateGst(110m, "Inclusive");
        inclusive.AmountExGst.Should().Be(100m);
        inclusive.GstAmount.Should().Be(10m);
        inclusive.TotalAmount.Should().Be(110m);

        var exclusive = ExpenseService.CalculateGst(100m, "Exclusive");
        exclusive.AmountExGst.Should().Be(100m);
        exclusive.GstAmount.Should().Be(10m);
        exclusive.TotalAmount.Should().Be(110m);

        var free = ExpenseService.CalculateGst(55m, "GstFree");
        free.GstAmount.Should().Be(0m);
        free.TotalAmount.Should().Be(55m);
    }

    [Fact]
    public void AustralianFinancialYear_JulyToJune()
    {
        var (start, end) = ExpenseService.AustralianFinancialYear(new DateOnly(2026, 3, 15));
        start.Should().Be(new DateOnly(2025, 7, 1));
        end.Should().Be(new DateOnly(2026, 6, 30));

        var (start2, end2) = ExpenseService.AustralianFinancialYear(new DateOnly(2026, 8, 1));
        start2.Should().Be(new DateOnly(2026, 7, 1));
        end2.Should().Be(new DateOnly(2027, 6, 30));
    }

    [Fact]
    public async Task Credentials_EncryptPersist_NeverReturnSecret_SurviveRestartSemantics()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var upsert = new UpsertIntegrationCredentialRequest(
            "openai",
            "OpenAI",
            true,
            """{"model":"gpt-4o-mini"}""",
            """{"apiKey":"sk-test-super-secret-key-9999"}""",
            false);

        var saved = await PostPutCredentialAsync(upsert);
        saved.HasSecret.Should().BeTrue();
        saved.SecretHint.Should().NotBeNullOrWhiteSpace();
        saved.SecretHint.Should().NotContain("sk-test-super-secret");
        JsonSerializer.Serialize(saved).Should().NotContain("sk-test-super-secret-key-9999");

        var list = await _client.GetFromJsonAsync<IntegrationCredentialDto[]>("/api/integrations/credentials");
        list.Should().NotBeNull();
        var row = list!.First(c => c.ProviderKey == "openai");
        row.HasSecret.Should().BeTrue();
        JsonSerializer.Serialize(row).Should().NotContain("sk-test-super-secret-key-9999");

        var rotated = await PostPutCredentialAsync(upsert with
        {
            SecretsJson = """{"apiKey":"sk-rotated-new-value-8888"}"""
        });
        rotated.HasSecret.Should().BeTrue();
        JsonSerializer.Serialize(rotated).Should().NotContain("sk-rotated-new-value-8888");
        JsonSerializer.Serialize(rotated).Should().NotContain("sk-test-super-secret");
    }

    [Fact]
    public async Task PosSettings_DoesNotStoreApiKeyInSettingsJson()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var put = await _client.PutAsJsonAsync("/api/settings/pos", new UpdatePosSettingsRequest(
            Enabled: true,
            Provider: "Square",
            MerchantId: "m_1",
            TerminalId: "t_1",
            ProviderLocationId: "loc_1",
            ApiKey: "square-secret-key-should-encrypt",
            ClearApiKey: false,
            ConnectionNotes: "test",
            DiscountPresets: null));
        put.EnsureSuccessStatusCode();
        var dto = await put.Content.ReadFromJsonAsync<PosSettingsDto>();
        dto!.ApiKeyConfigured.Should().BeTrue();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkshopDbContext>();
        var row = await db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "pos.settings");
        row.Should().NotBeNull();
        row!.JsonValue.Should().NotContain("square-secret-key-should-encrypt");

        var cred = await db.IntegrationCredentials.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ProviderKey == "pos");
        cred.Should().NotBeNull();
        cred!.SecretCipher.Should().NotBeNullOrWhiteSpace();
        cred.SecretCipher.Should().NotContain("square-secret-key-should-encrypt");
    }

    [Fact]
    public async Task Expenses_Create_Dashboard_Export_RecurringIdempotent_InventoryExcluded()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var cats = await _client.GetFromJsonAsync<ExpenseCategoryDto[]>("/api/expenses/categories");
        cats.Should().NotBeEmpty();
        var hosting = cats!.First(c => c.Name.Contains("Website hosting", StringComparison.OrdinalIgnoreCase));

        var operating = await PostJson<ExpenseDetailDto>("/api/expenses", new UpsertExpenseRequest(
            Title: "Site hosting March",
            Description: "Monthly hosting",
            CategoryId: hosting.Id,
            SupplierId: null,
            SupplierName: "Hosting Co",
            PurchaseDate: DateOnly.FromDateTime(DateTime.UtcNow),
            PaymentDate: DateOnly.FromDateTime(DateTime.UtcNow),
            InvoiceReference: "INV-HOST-1",
            Status: "Paid",
            TotalAmount: 110m,
            GstTreatment: "Inclusive",
            AmountExGst: null,
            GstAmount: null,
            PaymentMethod: "Card",
            AmountPaid: 110m,
            RefundAmount: null,
            BusinessUsePercent: 100m,
            Notes: null,
            RepairTicketId: null,
            InventoryItemId: null,
            PurchaseOrderId: null,
            IsInventoryOrCogsLinked: false));

        operating.GstAmount.Should().Be(10m);
        operating.AmountExGst.Should().Be(100m);

        var stockCat = cats.First(c => c.Name.Contains("Replacement screens", StringComparison.OrdinalIgnoreCase));
        await PostJson<ExpenseDetailDto>("/api/expenses", new UpsertExpenseRequest(
            Title: "iPhone screen stock",
            Description: null,
            CategoryId: stockCat.Id,
            SupplierId: null,
            SupplierName: "Parts AU",
            PurchaseDate: DateOnly.FromDateTime(DateTime.UtcNow),
            PaymentDate: null,
            InvoiceReference: "PO-99",
            Status: "Paid",
            TotalAmount: 550m,
            GstTreatment: "Inclusive",
            AmountExGst: null,
            GstAmount: null,
            PaymentMethod: "Account",
            AmountPaid: 550m,
            RefundAmount: null,
            BusinessUsePercent: 100m,
            Notes: null,
            RepairTicketId: null,
            InventoryItemId: Guid.NewGuid(),
            PurchaseOrderId: null,
            IsInventoryOrCogsLinked: true));

        var dash = await _client.GetFromJsonAsync<ExpenseDashboardDto>("/api/expenses/dashboard");
        dash.Should().NotBeNull();
        dash!.TotalThisMonth.Should().Be(110m);
        dash.AccountingBasisNote.Should().Contain("Inventory-linked");

        var csvRes = await _client.GetAsync("/api/expenses/export/csv");
        csvRes.EnsureSuccessStatusCode();
        var csv = await csvRes.Content.ReadAsStringAsync();
        csv.Should().Contain("Site hosting March");
        csv.Should().NotContain("iPhone screen stock");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await PostJson<RecurringExpenseDto>("/api/expenses/recurring", new UpsertRecurringExpenseRequest(
            Title: "Internet plan",
            CategoryId: hosting.Id,
            SupplierId: null,
            SupplierName: "ISP",
            TotalAmount: 89m,
            GstTreatment: "Inclusive",
            PaymentMethod: "Direct debit",
            Frequency: "Monthly",
            CustomIntervalDays: 0,
            StartDate: today,
            NextDueDate: today,
            EndDate: null,
            AutoCreateDraft: true,
            IsPaused: false,
            Notes: null));

        var process1 = await _client.PostAsync("/api/expenses/recurring/process-due", null);
        process1.EnsureSuccessStatusCode();
        var created1 = await process1.Content.ReadFromJsonAsync<JsonElement>();
        created1.GetProperty("created").GetInt32().Should().BeGreaterThanOrEqualTo(1);

        var process2 = await _client.PostAsync("/api/expenses/recurring/process-due", null);
        process2.EnsureSuccessStatusCode();
        var created2 = await process2.Content.ReadFromJsonAsync<JsonElement>();
        created2.GetProperty("created").GetInt32().Should().Be(0);

        var list = await _client.GetFromJsonAsync<ExpenseListItemDto[]>("/api/expenses?q=Internet");
        list!.Count(x => x.Title == "Internet plan").Should().Be(1);

        var del = await _client.DeleteAsync($"/api/expenses/{operating.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = await _client.GetAsync($"/api/expenses/{operating.Id}");
        after.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Credentials_RequireIntegrationsPermission()
    {
        await _factory.ResetDatabaseAsync();
        await EnsureSetupAndLoginAsync();

        var create = await _client.PostAsJsonAsync("/api/users", new CreateStaffUserRequest(
            "tech-sec@example.com", "Tech User", "TechPassw0rd!", "technician", null));
        create.EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("tech-sec@example.com", "TechPassw0rd!"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        using var techClient = _factory.CreateClient();
        techClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var forbidden = await techClient.GetAsync("/api/integrations/credentials");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task EnsureSetupAndLoginAsync()
    {
        var setup = new SetupRequest(
            "Expense Shop", null, null, null, null, null, "QLD", null, null, true, 0.1m, "AUD", 110m,
            "Owner", "expense-owner@example.com", "Workshop!2026", "#0F766E");
        (await _client.PostAsJsonAsync("/api/setup", setup)).EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("expense-owner@example.com", "Workshop!2026"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    private async Task<IntegrationCredentialDto> PostPutCredentialAsync(UpsertIntegrationCredentialRequest request)
    {
        var res = await _client.PutAsJsonAsync("/api/integrations/credentials", request);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IntegrationCredentialDto>())!;
    }

    private async Task<T> PostJson<T>(string path, object body)
    {
        var res = await _client.PostAsJsonAsync(path, body);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync();
            throw new Exception($"{(int)res.StatusCode} {path}: {err}");
        }
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }
}
