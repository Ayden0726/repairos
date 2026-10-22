namespace WorkshopOS.Contracts.Finance;

// —— Integration credentials (secrets never returned) ——

public sealed record IntegrationCredentialDto(
    Guid Id,
    string ProviderKey,
    string DisplayName,
    bool IsEnabled,
    bool HasSecret,
    string? SecretHint,
    string ConnectionStatus,
    string? PublicConfigJson,
    DateTimeOffset? LastTestedAt,
    DateTimeOffset? LastSuccessAt,
    string? LastError,
    DateTimeOffset UpdatedAt);

public sealed record UpsertIntegrationCredentialRequest(
    string ProviderKey,
    string? DisplayName,
    bool IsEnabled,
    string? PublicConfigJson,
    /// <summary>Raw secret JSON map, e.g. {"apiKey":"...","consumerSecret":"..."}. Null = keep existing.</summary>
    string? SecretsJson,
    bool ClearSecrets = false);

public sealed record TestIntegrationCredentialRequest(string ProviderKey);

public sealed record TestIntegrationCredentialResponse(
    bool Success,
    string Message,
    DateTimeOffset TestedAt);

public sealed record IntegrationProviderInfoDto(
    string ProviderKey,
    string DisplayName,
    string Description,
    IReadOnlyList<string> SecretFieldNames);

// —— Expenses ——

public sealed record ExpenseCategoryDto(
    Guid Id,
    string Name,
    Guid? ParentCategoryId,
    string Kind,
    string? Icon,
    decimal? MonthlyBudget,
    int SortOrder,
    bool IsSystem);

public sealed record UpsertExpenseCategoryRequest(
    Guid? Id,
    string Name,
    Guid? ParentCategoryId,
    string? Kind,
    string? Icon,
    decimal? MonthlyBudget,
    int SortOrder);

public sealed record ExpenseListItemDto(
    Guid Id,
    string Title,
    string Status,
    string? CategoryName,
    string? SupplierName,
    DateOnly PurchaseDate,
    decimal TotalAmount,
    decimal GstAmount,
    decimal AmountExGst,
    string GstTreatment,
    string? PaymentMethod,
    bool IsInventoryOrCogsLinked,
    DateTimeOffset UpdatedAt);

public sealed record ExpenseDetailDto(
    Guid Id,
    string Title,
    string? Description,
    Guid? CategoryId,
    string? CategoryName,
    Guid? SupplierId,
    string? SupplierName,
    DateOnly PurchaseDate,
    DateOnly? PaymentDate,
    string? InvoiceReference,
    string Status,
    Guid RecordedById,
    decimal TotalAmount,
    decimal AmountExGst,
    decimal GstAmount,
    string GstTreatment,
    string? PaymentMethod,
    decimal AmountPaid,
    decimal RefundAmount,
    decimal BusinessUsePercent,
    string? Notes,
    Guid? RepairTicketId,
    Guid? InventoryItemId,
    Guid? PurchaseOrderId,
    bool IsInventoryOrCogsLinked,
    Guid? RecurringExpenseId,
    IReadOnlyList<ExpenseAttachmentDto> Attachments,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ExpenseAttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset CreatedAt);

public sealed record UpsertExpenseRequest(
    string Title,
    string? Description,
    Guid? CategoryId,
    Guid? SupplierId,
    string? SupplierName,
    DateOnly PurchaseDate,
    DateOnly? PaymentDate,
    string? InvoiceReference,
    string? Status,
    decimal TotalAmount,
    string GstTreatment,
    decimal? AmountExGst,
    decimal? GstAmount,
    string? PaymentMethod,
    decimal? AmountPaid,
    decimal? RefundAmount,
    decimal? BusinessUsePercent,
    string? Notes,
    Guid? RepairTicketId,
    Guid? InventoryItemId,
    Guid? PurchaseOrderId,
    bool IsInventoryOrCogsLinked = false);

public sealed record ExpenseDashboardDto(
    decimal TotalThisMonth,
    decimal TotalThisFinancialYear,
    decimal GstPaidEligible,
    decimal UnpaidTotal,
    decimal OverdueTotal,
    int RecurringActiveCount,
    IReadOnlyList<NamedMoneyDto> ByCategory,
    IReadOnlyList<NamedMoneyDto> BySupplier,
    IReadOnlyList<NamedMoneyDto> MonthlyTrend,
    DateOnly From,
    DateOnly To,
    string AccountingBasisNote);

public sealed record NamedMoneyDto(string Name, decimal Amount);

public sealed record RecurringExpenseDto(
    Guid Id,
    string Title,
    Guid? CategoryId,
    string? SupplierName,
    decimal TotalAmount,
    string GstTreatment,
    string Frequency,
    int CustomIntervalDays,
    DateOnly StartDate,
    DateOnly NextDueDate,
    DateOnly? EndDate,
    bool AutoCreateDraft,
    bool IsPaused,
    string? Notes);

public sealed record UpsertRecurringExpenseRequest(
    string Title,
    Guid? CategoryId,
    Guid? SupplierId,
    string? SupplierName,
    decimal TotalAmount,
    string GstTreatment,
    string? PaymentMethod,
    string Frequency,
    int CustomIntervalDays,
    DateOnly StartDate,
    DateOnly? NextDueDate,
    DateOnly? EndDate,
    bool AutoCreateDraft,
    bool IsPaused,
    string? Notes);

public sealed record GstCalcRequest(decimal Amount, string Treatment, decimal GstRate = 0.10m);

public sealed record GstCalcResult(
    decimal TotalAmount,
    decimal AmountExGst,
    decimal GstAmount,
    string Treatment);

public sealed record ExpenseExportRowDto(
    DateOnly PurchaseDate,
    string Supplier,
    string Category,
    string Title,
    decimal AmountExGst,
    decimal GstAmount,
    decimal TotalAmount,
    string Status,
    string? PaymentMethod,
    string? InvoiceReference);
