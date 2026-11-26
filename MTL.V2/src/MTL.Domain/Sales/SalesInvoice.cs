using MTL.Domain.Common;

namespace MTL.Domain.Sales;

public enum InvoiceStatus
{
    Draft,
    Confirmed,
    PartiallyPaid,
    Paid,
    Cancelled,
    Refunded
}

public enum SalesInvoiceType
{
    Sale,
    Repair
}

public enum PaymentMethod
{
    Cash,
    BankTransfer
}

public enum DiscountType
{
    FixedAmount,
    Percentage
}

public enum SalesDiscountSource
{
    Promotion,
    Voucher,
    Manual
}

public enum RepairStatus
{
    Received,
    Diagnosing,
    Quoted,
    InProgress,
    ReadyForReturn,
    Returned,
    Cancelled
}

public sealed class SalesInvoice : CompanyOwnedEntity
{
    public string Number { get; set; } = string.Empty;
    public SalesInvoiceType Type { get; set; } = SalesInvoiceType.Sale;
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerAddress { get; set; }
    public DateTimeOffset InvoiceDateUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpectedReturnAtUtc { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public RepairStatus? RepairStatus { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public Guid? PromotionId { get; set; }
    public string? PromotionName { get; set; }
    public decimal PromotionDiscountAmount { get; set; }
    public Guid? VoucherId { get; set; }
    public string? VoucherCode { get; set; }
    public decimal VoucherDiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public bool IsInstallment { get; set; }
    public string? InstallmentProvider { get; set; }
    public string? InstallmentReference { get; set; }
    public Guid? InstallmentPaymentAccountId { get; set; }
    public PaymentAccount? InstallmentPaymentAccount { get; set; }
    public DateTimeOffset? PaymentDueDateUtc { get; set; }
    public string? Notes { get; set; }
    public string? PrintNotes { get; set; }
    public string? LegacySource { get; set; }
    public string? LegacyId { get; set; }
    public ICollection<SalesInvoiceLine> Lines { get; set; } = [];
    public ICollection<SalesDiscountLine> Discounts { get; set; } = [];
    public ICollection<SalesPayment> Payments { get; set; } = [];
    public ICollection<RepairOrder> Repairs { get; set; } = [];
    public ICollection<SalesInvoiceServiceType> ServiceTypes { get; set; } = [];
}

public sealed class SalesInvoiceLine : CompanyOwnedEntity
{
    public Guid SalesInvoiceId { get; set; }
    public SalesInvoice SalesInvoice { get; set; } = null!;
    public Guid? ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string UnitName { get; set; } = "Cái";
    public string WarrantyPeriod { get; set; } = "3 tháng";
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public string? SerialNumbersJson { get; set; }
    public string? Notes { get; set; }
}

public sealed class SalesDiscountLine : CompanyOwnedEntity
{
    public Guid SalesInvoiceId { get; set; }
    public SalesInvoice SalesInvoice { get; set; } = null!;
    public SalesDiscountSource Source { get; set; }
    public Guid? PromotionId { get; set; }
    public Guid? VoucherId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

public sealed class PaymentAccount : CompanyOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public sealed class InstallmentProvider : CompanyOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public sealed class SalesPayment : CompanyOwnedEntity
{
    public Guid SalesInvoiceId { get; set; }
    public SalesInvoice SalesInvoice { get; set; } = null!;
    public PaymentMethod Method { get; set; }
    public Guid? PaymentAccountId { get; set; }
    public PaymentAccount? PaymentAccount { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset PaidAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool IsScheduled { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}

public sealed class Promotion : CompanyOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DiscountType DiscountType { get; set; } = DiscountType.FixedAmount;
    public decimal Value { get; set; }
    public decimal MinimumOrderAmount { get; set; }
    public decimal? MaximumDiscountAmount { get; set; }
    public DateTimeOffset? StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Voucher : CompanyOwnedEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DiscountType DiscountType { get; set; } = DiscountType.FixedAmount;
    public decimal Value { get; set; }
    public decimal MinimumOrderAmount { get; set; }
    public decimal? MaximumDiscountAmount { get; set; }
    public DateTimeOffset? StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class RepairOrder : CompanyOwnedEntity
{
    public Guid SalesInvoiceId { get; set; }
    public SalesInvoice SalesInvoice { get; set; } = null!;
    public string DeviceModel { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? Accessories { get; set; }
    public string DeviceCondition { get; set; } = string.Empty;
    public string CustomerRequest { get; set; } = string.Empty;
    public string? TechnicianAssessment { get; set; }
    public decimal EstimatedCost { get; set; }
    public DateTimeOffset? ExpectedReturnAtUtc { get; set; }
    public RepairStatus Status { get; set; } = RepairStatus.Received;
    public string? Notes { get; set; }
    public ICollection<RepairOrderServiceType> ServiceTypes { get; set; } = [];
}

public sealed class ServiceType : CompanyOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public ICollection<SalesInvoiceServiceType> SalesInvoices { get; set; } = [];
    public ICollection<RepairOrderServiceType> RepairOrders { get; set; } = [];
}

public sealed class SalesInvoiceServiceType : CompanyOwnedEntity
{
    public Guid SalesInvoiceId { get; set; }
    public SalesInvoice SalesInvoice { get; set; } = null!;
    public Guid ServiceTypeId { get; set; }
    public ServiceType ServiceType { get; set; } = null!;
}

public sealed class RepairOrderServiceType : CompanyOwnedEntity
{
    public Guid RepairOrderId { get; set; }
    public RepairOrder RepairOrder { get; set; } = null!;
    public Guid ServiceTypeId { get; set; }
    public ServiceType ServiceType { get; set; } = null!;
    public string? Notes { get; set; }
    public decimal Amount { get; set; }
}

