using Microsoft.AspNetCore.Identity;

namespace MTL.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public Guid CompanyId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;
    public DateTimeOffset? LastLoginAtUtc { get; set; }
}

