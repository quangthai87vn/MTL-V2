using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MTL.Domain.Identity;
using MTL.Infrastructure.Identity;

namespace MTL.Api.Controllers;

[ApiController]
[Route("api/employees")]
public sealed class EmployeesController(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    ICurrentUser currentUser)
    : ControllerBase
{
    [Authorize(Policy = Permissions.EmployeesRead)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeListItem>>> GetAll(CancellationToken cancellationToken)
    {
        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        var users = await userManager.Users.AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);

        var result = new List<EmployeeListItem>(users.Count);
        foreach (var user in users) result.Add(await ToItem(user));
        return Ok(result);
    }

    [Authorize(Policy = Permissions.EmployeesRead)]
    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyList<RoleItem>>> GetRoles(CancellationToken cancellationToken)
    {
        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        var roles = await roleManager.Roles.OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var result = new List<RoleItem>(roles.Count);
        foreach (var role in roles)
        {
            var permissions = (await roleManager.GetClaimsAsync(role))
                .Where(x => x.Type == "permission")
                .Select(x => x.Value)
                .OrderBy(x => x)
                .ToArray();
            var users = string.IsNullOrWhiteSpace(role.Name)
                ? []
                : await userManager.GetUsersInRoleAsync(role.Name);
            result.Add(new RoleItem(
                role.Id,
                role.Name ?? string.Empty,
                permissions,
                IsSystemRole(role.Name),
                users.Count(x => x.CompanyId == companyId)));
        }
        return Ok(result);
    }

    [Authorize(Policy = Permissions.RolesManage)]
    [HttpGet("permissions")]
    public ActionResult<IReadOnlyList<PermissionItem>> GetPermissions() => Ok(PermissionLabels.All);

    [Authorize(Policy = Permissions.EmployeesManage)]
    [HttpPost]
    public async Task<ActionResult<EmployeeListItem>> Create(CreateEmployeeRequest request)
    {
        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        var role = await roleManager.FindByNameAsync(request.Role);
        if (role is null) return BadRequest(new { message = "Vai trò không tồn tại." });
        if (request.Role == SystemRoles.Administrator && !User.HasClaim("permission", Permissions.RolesManage)) return Forbid();

        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            EmailConfirmed = true,
            FullName = request.FullName.Trim(),
            PhoneNumber = Clean(request.Phone),
            CompanyId = companyId,
            IsActive = request.IsActive,
            MustChangePassword = true
        };

        var result = await userManager.CreateAsync(user, request.InitialPassword);
        if (!result.Succeeded) return IdentityValidation(result);
        await userManager.AddToRoleAsync(user, role.Name!);
        return CreatedAtAction(nameof(GetAll), new { id = user.Id }, await ToItem(user));
    }

    [Authorize(Policy = Permissions.EmployeesManage)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmployeeListItem>> Update(Guid id, UpdateEmployeeRequest request)
    {
        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        var user = await userManager.Users.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId);
        if (user is null) return NotFound();
        var role = await roleManager.FindByNameAsync(request.Role);
        if (role is null) return BadRequest(new { message = "Vai trò không tồn tại." });
        if (request.Role == SystemRoles.Administrator && !User.HasClaim("permission", Permissions.RolesManage)) return Forbid();
        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentUser.UserId == id && !request.IsActive)
            return BadRequest(new { message = "Bạn không thể tự khóa tài khoản đang đăng nhập." });
        if (currentUser.UserId == id && (currentRoles.Count != 1 || !currentRoles.Contains(role.Name!, StringComparer.OrdinalIgnoreCase)))
            return BadRequest(new { message = "Bạn không thể tự thay đổi vai trò của tài khoản đang đăng nhập." });
        if (!request.IsActive && await IsLastActiveAdministrator(user, currentRoles))
            return BadRequest(new { message = "Không thể khóa quản trị viên đang hoạt động cuối cùng." });
        if (!currentRoles.Contains(role.Name!, StringComparer.OrdinalIgnoreCase) &&
            await IsLastActiveAdministrator(user, currentRoles))
            return BadRequest(new { message = "Không thể gỡ vai trò quản trị viên đang hoạt động cuối cùng." });

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = Clean(request.Phone);
        user.IsActive = request.IsActive;
        var email = request.Email.Trim();
        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await userManager.SetEmailAsync(user, email);
            if (!emailResult.Succeeded) return IdentityValidation(emailResult);
            var usernameResult = await userManager.SetUserNameAsync(user, email);
            if (!usernameResult.Succeeded) return IdentityValidation(usernameResult);
        }

        if (currentRoles.Count != 1 || !currentRoles.Contains(role.Name!, StringComparer.OrdinalIgnoreCase))
        {
            if (currentRoles.Count > 0)
            {
                var removeResult = await userManager.RemoveFromRolesAsync(user, currentRoles);
                if (!removeResult.Succeeded) return IdentityValidation(removeResult);
            }
            var addResult = await userManager.AddToRoleAsync(user, role.Name!);
            if (!addResult.Succeeded) return IdentityValidation(addResult);
        }
        user.SecurityStamp = Guid.NewGuid().ToString();
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) return IdentityValidation(result);
        return Ok(await ToItem(user));
    }

    [Authorize(Policy = Permissions.EmployeesManage)]
    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateEmployeeStatusRequest request)
    {
        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        var user = await userManager.Users.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId);
        if (user is null) return NotFound();
        if (currentUser.UserId == user.Id && !request.IsActive)
            return BadRequest(new { message = "Bạn không thể tự khóa tài khoản đang đăng nhập." });
        var roles = await userManager.GetRolesAsync(user);
        if (!request.IsActive && await IsLastActiveAdministrator(user, roles))
            return BadRequest(new { message = "Không thể khóa quản trị viên đang hoạt động cuối cùng." });
        user.IsActive = request.IsActive;
        if (request.IsActive)
        {
            user.LockoutEnd = null;
            user.AccessFailedCount = 0;
        }
        user.SecurityStamp = Guid.NewGuid().ToString();
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded ? NoContent() : BadRequest(result.Errors);
    }

    [Authorize(Policy = Permissions.EmployeesManage)]
    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, ResetEmployeePasswordRequest request)
    {
        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        var user = await userManager.Users.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId);
        if (user is null) return NotFound();
        if (currentUser.UserId == id)
            return BadRequest(new { message = "Hãy dùng chức năng Đổi mật khẩu trong menu tài khoản để đổi mật khẩu của chính bạn." });

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded) return IdentityValidation(result);

        user.MustChangePassword = request.RequireChangeOnNextLogin;
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        user.SecurityStamp = Guid.NewGuid().ToString();
        var updateResult = await userManager.UpdateAsync(user);
        return updateResult.Succeeded ? NoContent() : IdentityValidation(updateResult);
    }

    [Authorize(Policy = Permissions.RolesManage)]
    [HttpPost("roles")]
    public async Task<ActionResult<RoleItem>> CreateRole(SaveRoleRequest request)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Tên vai trò là bắt buộc." });
        if (await roleManager.RoleExistsAsync(name)) return Conflict(new { message = "Vai trò đã tồn tại." });
        var role = new IdentityRole<Guid>(name);
        var createResult = await roleManager.CreateAsync(role);
        if (!createResult.Succeeded) return IdentityValidation(createResult);
        await ReplaceRolePermissions(role, request.Permissions);
        return Ok(new RoleItem(role.Id, role.Name!, NormalizePermissions(request.Permissions), false, 0));
    }

    [Authorize(Policy = Permissions.RolesManage)]
    [HttpPut("roles/{id:guid}")]
    public async Task<ActionResult<RoleItem>> UpdateRole(Guid id, SaveRoleRequest request)
    {
        var role = await roleManager.FindByIdAsync(id.ToString());
        if (role is null) return NotFound();
        if (role.Name == SystemRoles.Administrator)
            return BadRequest(new { message = "Vai trò Administrator được bảo vệ để tránh mất quyền quản trị hệ thống." });
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Tên vai trò là bắt buộc." });
        if (IsSystemRole(role.Name) && !string.Equals(role.Name, name, StringComparison.Ordinal))
            return BadRequest(new { message = "Không thể đổi tên vai trò hệ thống. Bạn vẫn có thể điều chỉnh các quyền được cấp." });
        if (!string.Equals(role.Name, name, StringComparison.OrdinalIgnoreCase) && await roleManager.RoleExistsAsync(name))
            return Conflict(new { message = "Tên vai trò đã tồn tại." });
        role.Name = name;
        var updateResult = await roleManager.UpdateAsync(role);
        if (!updateResult.Succeeded) return IdentityValidation(updateResult);
        await ReplaceRolePermissions(role, request.Permissions);
        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        var users = await userManager.GetUsersInRoleAsync(role.Name!);
        return Ok(new RoleItem(role.Id, role.Name!, NormalizePermissions(request.Permissions), IsSystemRole(role.Name), users.Count(x => x.CompanyId == companyId)));
    }

    [Authorize(Policy = Permissions.RolesManage)]
    [HttpDelete("roles/{id:guid}")]
    public async Task<IActionResult> DeleteRole(Guid id)
    {
        var role = await roleManager.FindByIdAsync(id.ToString());
        if (role is null) return NotFound();
        if (IsSystemRole(role.Name))
            return BadRequest(new { message = "Vai trò hệ thống không thể xóa." });
        if (!string.IsNullOrWhiteSpace(role.Name) && (await userManager.GetUsersInRoleAsync(role.Name)).Count > 0)
            return Conflict(new { message = "Vai trò đang được gán cho nhân viên. Hãy chuyển nhân viên sang vai trò khác trước khi xóa." });

        var result = await roleManager.DeleteAsync(role);
        return result.Succeeded ? NoContent() : IdentityValidation(result);
    }

    private async Task ReplaceRolePermissions(IdentityRole<Guid> role, IEnumerable<string> requestedPermissions)
    {
        var requested = NormalizePermissions(requestedPermissions).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = (await roleManager.GetClaimsAsync(role)).Where(x => x.Type == "permission").ToList();
        foreach (var claim in existing.Where(x => !requested.Contains(x.Value))) await roleManager.RemoveClaimAsync(role, claim);
        foreach (var permission in requested.Where(x => existing.All(c => c.Value != x)))
            await roleManager.AddClaimAsync(role, new Claim("permission", permission));
    }

    private static string[] NormalizePermissions(IEnumerable<string>? permissions)
    {
        var normalized = (permissions ?? [])
            .Where(Permissions.All.Contains)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dependencies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Permissions.EmployeesManage] = Permissions.EmployeesRead,
            [Permissions.RolesManage] = Permissions.EmployeesRead,
            [Permissions.ProductsManage] = Permissions.ProductsRead,
            [Permissions.WooCommerceManage] = Permissions.ProductsRead,
            [Permissions.SalesManage] = Permissions.SalesRead,
            [Permissions.PurchasesManage] = Permissions.PurchasesRead,
            [Permissions.CustomersManage] = Permissions.CustomersRead,
            [Permissions.InventoryManage] = Permissions.InventoryRead
        };
        foreach (var dependency in dependencies.Where(x => normalized.Contains(x.Key))) normalized.Add(dependency.Value);
        return normalized.OrderBy(x => x).ToArray();
    }

    private async Task<bool> IsLastActiveAdministrator(ApplicationUser user, IEnumerable<string> roles)
    {
        if (!roles.Contains(SystemRoles.Administrator, StringComparer.OrdinalIgnoreCase)) return false;
        var administrators = await userManager.GetUsersInRoleAsync(SystemRoles.Administrator);
        return administrators.Count(x => x.CompanyId == user.CompanyId && x.IsActive) <= 1;
    }

    private async Task<EmployeeListItem> ToItem(ApplicationUser user) => new(
        user.Id,
        user.FullName,
        user.Email ?? string.Empty,
        user.PhoneNumber,
        user.IsActive,
        user.MustChangePassword,
        user.LockoutEnd,
        user.LastLoginAtUtc,
        (await userManager.GetRolesAsync(user)).ToArray());

    private ActionResult IdentityValidation(IdentityResult result) => ValidationProblem(new ValidationProblemDetails(
        result.Errors.GroupBy(x => x.Code).ToDictionary(x => x.Key, x => x.Select(e => e.Description).ToArray())));

    private static bool IsSystemRole(string? name) => name is SystemRoles.Administrator or SystemRoles.Manager or SystemRoles.Sales or SystemRoles.Warehouse;
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record CreateEmployeeRequest(string FullName, string Email, string? Phone, string InitialPassword, string Role, bool IsActive = true);
public sealed record UpdateEmployeeRequest(string FullName, string Email, string? Phone, string Role, bool IsActive);
public sealed record UpdateEmployeeStatusRequest(bool IsActive);
public sealed record ResetEmployeePasswordRequest(string NewPassword, bool RequireChangeOnNextLogin = true);
public sealed record EmployeeListItem(Guid Id, string FullName, string Email, string? Phone, bool IsActive, bool MustChangePassword, DateTimeOffset? LockoutEndUtc, DateTimeOffset? LastLoginAtUtc, IReadOnlyCollection<string> Roles);
public sealed record RoleItem(Guid Id, string Name, IReadOnlyCollection<string> Permissions, bool IsSystem, int UserCount);
public sealed record SaveRoleRequest(string Name, IReadOnlyCollection<string> Permissions);
public sealed record PermissionItem(string Key, string Group, string Label);

internal static class PermissionLabels
{
    public static readonly IReadOnlyList<PermissionItem> All =
    [
        new(Permissions.DashboardRead, "Tổng quan", "Xem tổng quan"),
        new(Permissions.EmployeesRead, "Nhân viên", "Xem nhân viên"),
        new(Permissions.EmployeesManage, "Nhân viên", "Thêm và sửa nhân viên"),
        new(Permissions.RolesManage, "Nhân viên", "Quản lý vai trò và phân quyền"),
        new(Permissions.ProductsRead, "Sản phẩm", "Xem sản phẩm"),
        new(Permissions.ProductsManage, "Sản phẩm", "Thêm, sửa và xóa sản phẩm"),
        new(Permissions.WooCommerceManage, "Sản phẩm", "Quản lý đồng bộ WooCommerce"),
        new(Permissions.SalesRead, "Bán hàng", "Xem hóa đơn"),
        new(Permissions.SalesManage, "Bán hàng", "Tạo, sửa, xóa và thu tiền"),
        new(Permissions.PurchasesRead, "Nhập hàng", "Xem hóa đơn nhập"),
        new(Permissions.PurchasesManage, "Nhập hàng", "Quản lý nhập hàng"),
        new(Permissions.CustomersRead, "Khách hàng", "Xem khách hàng"),
        new(Permissions.CustomersManage, "Khách hàng", "Thêm và sửa khách hàng"),
        new(Permissions.InventoryRead, "Kho", "Xem tồn kho và serial"),
        new(Permissions.InventoryManage, "Kho", "Điều chỉnh kho và serial"),
        new(Permissions.ReportsRead, "Báo cáo", "Xem báo cáo"),
        new(Permissions.SettingsManage, "Cấu hình", "Quản lý cấu hình hệ thống"),
        new(Permissions.AuditRead, "Bảo mật", "Xem nhật ký thay đổi")
    ];
}
