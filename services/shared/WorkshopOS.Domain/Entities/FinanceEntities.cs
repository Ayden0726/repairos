namespace WorkshopOS.Domain.Entities;

/// <summary>Encrypted third-party credentials. Secrets never leave the server unencrypted.</summary>
public class IntegrationCredential : SoftDeleteEntity
{
    /// <summary>Stable key: woocommerce, pos, openai, twilio, smtp, imei, shipping, custom.{slug}</summary>
    public string ProviderKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    /// <summary>Non-secret JSON (URLs, merchant IDs, model names, etc.).</summary>
    public string? PublicConfigJson { get; set; }
    /// <summary>AES-GCM ciphertext of secret JSON map (v1:iv:tag:cipher).</summary>
    public string? SecretCipher { get; set; }
    /// <summary>Key version used when encrypting (for planned rotation).</summary>
    public int KeyVersion { get; set; } = 1;
    /// <summary>Masked hint only, e.g. ••••ab12 — never the secret.</summary>
    public string? SecretHint { get; set; }
    public bool HasSecret => !string.IsNullOrWhiteSpace(SecretCipher);
    public DateTimeOffset? LastTestedAt { get; set; }
    public DateTimeOffset? LastSuccessAt { get; set; }
    public string? LastError { get; set; }
    /// <summary>NotConfigured | Configured | Connected | Error | Disabled</summary>
    public string ConnectionStatus { get; set; } = "NotConfigured";
    public Guid? UpdatedById { get; set; }
}

public class ExpenseCategory : SoftDeleteEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentCategoryId { get; set; }
    public ExpenseCategory? ParentCategory { get; set; }
    public ICollection<ExpenseCategory> Children { get; set; } = new List<ExpenseCategory>();
    /// <summary>Operating | Stock | Cogs | Capital | Other</summary>
    public string Kind { get; set; } = "Operating";
    public string? Icon { get; set; }
    public decimal? MonthlyBudget { get; set; }
    public int SortOrder { get; set; }
    public bool IsSystem { get; set; }
}

public class BusinessExpense : SoftDeleteEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public ExpenseCategory? Category { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string? SupplierName { get; set; }
    public DateOnly PurchaseDate { get; set; }
    public DateOnly? PaymentDate { get; set; }
    public string? InvoiceReference { get; set; }
    /// <summary>Draft | Unpaid | PartiallyPaid | Paid | Overdue | Cancelled</summary>
    public string Status { get; set; } = "Draft";
    public Guid RecordedById { get; set; }

    /// <summary>Total paid / payable in AUD (invoice total).</summary>
    public decimal TotalAmount { get; set; }
    public decimal AmountExGst { get; set; }
    public decimal GstAmount { get; set; }
    /// <summary>Inclusive | Exclusive | GstFree | InputTaxed | NoCredit</summary>
    public string GstTreatment { get; set; } = "Inclusive";
    public string? PaymentMethod { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal RefundAmount { get; set; }
    /// <summary>0–100 business-use percentage for mixed personal/business purchases.</summary>
    public decimal BusinessUsePercent { get; set; } = 100m;
    public string? Notes { get; set; }

    public Guid? RepairTicketId { get; set; }
    public Guid? InventoryItemId { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    /// <summary>When true, exclude from operating-expense totals (already counted as stock/COGS).</summary>
    public bool IsInventoryOrCogsLinked { get; set; }

    public Guid? RecurringExpenseId { get; set; }
    public RecurringExpense? RecurringExpense { get; set; }
    /// <summary>Idempotency key for auto-created recurring instances (provider|period).</summary>
    public string? RecurrenceInstanceKey { get; set; }

    public ICollection<ExpenseAttachment> Attachments { get; set; } = new List<ExpenseAttachment>();
}

public class ExpenseAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExpenseId { get; set; }
    public BusinessExpense Expense { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public Guid UploadedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class RecurringExpense : SoftDeleteEntity
{
    public string Title { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public ExpenseCategory? Category { get; set; }
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public decimal TotalAmount { get; set; }
    public string GstTreatment { get; set; } = "Inclusive";
    public string? PaymentMethod { get; set; }
    /// <summary>Weekly | Fortnightly | Monthly | Quarterly | Yearly | Custom</summary>
    public string Frequency { get; set; } = "Monthly";
    public int CustomIntervalDays { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly NextDueDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool AutoCreateDraft { get; set; } = true;
    public bool IsPaused { get; set; }
    public string? Notes { get; set; }
    public Guid CreatedById { get; set; }
}
