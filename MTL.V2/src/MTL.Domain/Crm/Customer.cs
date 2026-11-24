using MTL.Domain.Common;

namespace MTL.Domain.Crm;

public sealed class Customer : CompanyOwnedEntity
{
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? CitizenId { get; set; }
    public string? TaxCode { get; set; }
    public DateOnly? Birthday { get; set; }
    public string? Notes { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTimeOffset? LastPurchaseAtUtc { get; set; }
    public string? LegacySource { get; set; }
    public string? LegacyId { get; set; }
}

