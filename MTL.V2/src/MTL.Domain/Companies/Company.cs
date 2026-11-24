using MTL.Domain.Common;

namespace MTL.Domain.Companies;

public sealed class Company : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? TaxCode { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? LogoDataUrl { get; set; }
    public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";
    public string Currency { get; set; } = "VND";
    public int DefaultRepairTurnaroundDays { get; set; } = 5;
    public bool IsActive { get; set; } = true;
}

