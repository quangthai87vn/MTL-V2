using MTL.Domain.Common;
using MTL.Domain.Sales;

namespace MTL.Domain.Purchasing;

public enum PurchaseInvoiceStatus
{
    InProgress,
    Processed,
    Cancelled
}

public sealed class PurchaseInvoice : CompanyOwnedEntity
{
    public string Number { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public DateTimeOffset InvoiceDateUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ReceivedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DueDateUtc { get; set; }
    public PurchaseInvoiceStatus Status { get; set; } = PurchaseInvoiceStatus.InProgress;
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string? SupplierInvoiceNumber { get; set; }
    public string? SupplierInvoiceSeries { get; set; }
    public string? WarehouseReceiptNumber { get; set; }
    public string? Notes { get; set; }
    public bool InventoryPosted { get; set; }
    public string? LegacySource { get; set; }
    public string? LegacyId { get; set; }
    public ICollection<PurchaseInvoiceLine> Lines { get; set; } = [];
    public ICollection<PurchasePayment> Payments { get; set; } = [];
}

public sealed class PurchaseInvoiceLine : CompanyOwnedEntity
{
    public Guid PurchaseInvoiceId { get; set; }
    public PurchaseInvoice PurchaseInvoice { get; set; } = null!;
    public Guid? ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public string UnitName { get; set; } = "Cái";
    public decimal UnitCost { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal? VatRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public string? SerialNumbersJson { get; set; }
    public string? Notes { get; set; }
}

public sealed class PurchasePayment : CompanyOwnedEntity
{
    public Guid PurchaseInvoiceId { get; set; }
    public PurchaseInvoice PurchaseInvoice { get; set; } = null!;
    public PaymentMethod Method { get; set; }
    public Guid? PaymentAccountId { get; set; }
    public PaymentAccount? PaymentAccount { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset PaidAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? Notes { get; set; }
}

