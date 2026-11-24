using MTL.Domain.Common;

namespace MTL.Domain.Inventory;

public enum StockMovementType
{
    Opening,
    Purchase,
    Sale,
    ReturnIn,
    ReturnOut,
    Adjustment,
    Transfer
}

public sealed class StockMovement : CompanyOwnedEntity
{
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public StockMovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public enum ProductSerialStatus
{
    InStock,
    Reserved,
    Sold,
    Returned,
    InWarranty,
    Scrapped
}

public sealed class ProductSerial : CompanyOwnedEntity
{
    public Guid ProductId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public ProductSerialStatus Status { get; set; } = ProductSerialStatus.InStock;
    public decimal CostPrice { get; set; }
    public DateTimeOffset? PurchasedAtUtc { get; set; }
    public DateTimeOffset? SoldAtUtc { get; set; }
    public DateOnly? WarrantyExpiresOn { get; set; }
    public Guid? SalesInvoiceLineId { get; set; }
    public Guid? PurchaseInvoiceLineId { get; set; }
}

