namespace MTL.Infrastructure.Persistence;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";
    public string CompanyName { get; set; } = "MỸ THO LAPTOP";
    public string CompanyCode { get; set; } = "MTL";
    public string? AdminEmail { get; set; }
    public string? AdminPassword { get; set; }
}

