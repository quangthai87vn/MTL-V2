using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace MTL.Infrastructure.Identity;

public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? CompanyId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
}

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId => ParseGuid(Principal?.FindFirstValue(ClaimTypes.NameIdentifier));
    public Guid? CompanyId => ParseGuid(Principal?.FindFirstValue("company_id"));
    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    private static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var id) ? id : null;
}

