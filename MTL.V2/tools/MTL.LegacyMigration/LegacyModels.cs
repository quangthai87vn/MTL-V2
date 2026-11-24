namespace MTL.LegacyMigration;

internal sealed record LegacyCustomer(
    int Id,
    string? CitizenId,
    string FullName,
    string? Address,
    string? Phone,
    string? TaxCode,
    string? Notes,
    string? Email,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

internal sealed record LegacyInvoice(
    string Number,
    int? CustomerId,
    int DocumentTypeId,
    DateTime InvoiceDate,
    DateTime? ReturnDate,
    string? Notes,
    string? InternalNotes,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    bool IsProcessed,
    string? Serial,
    string? Model,
    string? CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    string? CustomerAddress);

internal sealed record LegacyInvoiceLine(
    int Id,
    string InvoiceNumber,
    int? ProductId,
    string? Sku,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal LineTotal,
    string? Notes,
    string? Warranty);

internal sealed record LegacyTag(int Id, string Name);
internal sealed record LegacyInvoiceTag(string InvoiceNumber, int TagId);

internal sealed record LegacyPurchase(
    int Id,
    string Number,
    int? SupplierId,
    DateTime InvoiceDate,
    string? Notes,
    string? InternalNotes,
    decimal TotalAmount,
    decimal DiscountAmount,
    decimal PaidAmount,
    string? SupplierInvoiceNumber,
    string? SupplierName,
    string? SupplierPhone,
    string? SupplierEmail,
    string? SupplierAddress,
    string? SupplierTaxCode);

internal sealed record LegacyPurchaseLine(
    int Id,
    string InvoiceNumber,
    int? ProductId,
    string? Sku,
    string ProductName,
    decimal Quantity,
    string? UnitName,
    decimal UnitCost,
    decimal DiscountAmount,
    decimal LineTotal,
    string? Notes);

internal sealed record ArchiveInvoice(
    int Id,
    string CustomerName,
    DateTime InvoiceDate,
    string? Notes,
    decimal TotalAmount,
    bool IsProcessed);

internal sealed record ArchiveInvoiceLine(
    int Id,
    int InvoiceId,
    string ProductName,
    string? UnitName,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string? Warranty);

internal sealed class LegacyDataset
{
    public List<LegacyCustomer> Customers { get; } = [];
    public List<LegacyInvoice> Invoices { get; } = [];
    public List<LegacyInvoiceLine> InvoiceLines { get; } = [];
    public List<LegacyTag> Tags { get; } = [];
    public List<LegacyInvoiceTag> InvoiceTags { get; } = [];
    public List<LegacyPurchase> Purchases { get; } = [];
    public List<LegacyPurchaseLine> PurchaseLines { get; } = [];
    public List<ArchiveInvoice> ArchiveInvoices { get; } = [];
    public List<ArchiveInvoiceLine> ArchiveInvoiceLines { get; } = [];
}

internal sealed record MigrationOptions(
    bool Apply,
    bool IncludeArchive,
    int SourceCompanyId,
    Guid TargetCompanyId,
    string SourceConnectionString,
    string TargetConnectionString,
    string ReportPath);

internal sealed class MigrationReport
{
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string Mode { get; init; } = "dry-run";
    public int SourceCompanyId { get; init; }
    public Guid TargetCompanyId { get; init; }
    public int SourceCustomers { get; init; }
    public int SourceCustomersWithCitizenId { get; init; }
    public int CurrentInvoices { get; init; }
    public int CurrentInvoiceLines { get; init; }
    public decimal CurrentInvoiceTotal { get; init; }
    public int ProcessedCurrentInvoices { get; init; }
    public int ProcessingCurrentInvoices { get; init; }
    public int ExcludedQuotes { get; init; }
    public decimal ExcludedQuoteTotal { get; init; }
    public int ArchiveInvoices { get; init; }
    public int ArchiveInvoiceLines { get; init; }
    public int ArchiveOrphanLines { get; init; }
    public decimal ArchiveInvoiceTotal { get; init; }
    public int PurchaseInvoices { get; init; }
    public int PurchaseInvoiceLines { get; init; }
    public decimal PurchaseInvoiceTotal { get; init; }
    public int TargetCustomersBefore { get; init; }
    public int TargetInvoicesBefore { get; init; }
    public int TargetPurchasesBefore { get; init; }
    public int PlannedNewCustomers { get; set; }
    public int PlannedMatchedCustomers { get; set; }
    public int PlannedNewInvoices { get; set; }
    public int PlannedNewPurchases { get; set; }
    public int ExistingImportedInvoices { get; set; }
    public int NumberCollisions { get; set; }
    public int InsertedCustomers { get; set; }
    public int MatchedCustomers { get; set; }
    public int InsertedInvoices { get; set; }
    public int InsertedLines { get; set; }
    public int InsertedCashPayments { get; set; }
    public int InsertedServiceTypeLinks { get; set; }
    public int InsertedSuppliers { get; set; }
    public int InsertedPurchases { get; set; }
    public int InsertedPurchaseLines { get; set; }
    public int InsertedPurchasePayments { get; set; }
    public int TargetCustomersAfter { get; set; }
    public int TargetInvoicesAfter { get; set; }
    public int TargetPurchasesAfter { get; set; }
    public decimal TargetImportedInvoiceTotal { get; set; }
    public decimal TargetImportedPurchaseTotal { get; set; }
    public List<string> Warnings { get; init; } = [];
}
