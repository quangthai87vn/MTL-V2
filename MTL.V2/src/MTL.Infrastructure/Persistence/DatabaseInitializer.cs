using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MTL.Domain.Companies;
using MTL.Domain.Identity;
using MTL.Domain.Sales;
using MTL.Infrastructure.Identity;

namespace MTL.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<SeedOptions> seedOptions,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);

        var options = seedOptions.Value;
        var company = await db.Companies.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Code == options.CompanyCode, cancellationToken);

        if (company is null)
        {
            company = new Company
            {
                Name = options.CompanyName,
                Code = options.CompanyCode,
                Website = "https://mytholaptop.vn"
            };
            db.Companies.Add(company);
            await db.SaveChangesAsync(cancellationToken);
        }

        var googleReviewPromotion = await db.Promotions.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.CompanyId == company.Id && x.Name == "Đánh giá Google Maps 5 sao", cancellationToken);
        if (googleReviewPromotion is null)
        {
            db.Promotions.Add(new Promotion
            {
                CompanyId = company.Id,
                Name = "Đánh giá Google Maps 5 sao",
                Description = "Khách hàng đánh giá Google Maps 5 sao tại cửa hàng.",
                DiscountType = DiscountType.FixedAmount,
                Value = 200000,
                IsActive = true
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        await EnsureRoleAsync(SystemRoles.Administrator, Permissions.All, cancellationToken);
        await EnsureRoleAsync(SystemRoles.Manager, Permissions.All.Where(x => x != Permissions.RolesManage), cancellationToken);
        await EnsureRoleAsync(SystemRoles.Sales,
        [
            Permissions.DashboardRead,
            Permissions.ProductsRead,
            Permissions.SalesRead,
            Permissions.SalesManage,
            Permissions.CustomersRead,
            Permissions.CustomersManage
        ], cancellationToken);
        await EnsureRoleAsync(SystemRoles.Warehouse,
        [
            Permissions.DashboardRead,
            Permissions.ProductsRead,
            Permissions.ProductsManage,
            Permissions.PurchasesRead,
            Permissions.PurchasesManage,
            Permissions.InventoryRead,
            Permissions.InventoryManage
        ], cancellationToken);

        if (string.IsNullOrWhiteSpace(options.AdminEmail) || string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            logger.LogWarning("Admin seed skipped. Configure Seed:AdminEmail and Seed:AdminPassword.");
            return;
        }

        var admin = await userManager.FindByEmailAsync(options.AdminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = options.AdminEmail,
                Email = options.AdminEmail,
                EmailConfirmed = true,
                FullName = "Quản trị hệ thống",
                CompanyId = company.Id,
                IsActive = true,
                MustChangePassword = true
            };

            var result = await userManager.CreateAsync(admin, options.AdminPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Cannot seed administrator: {string.Join(", ", result.Errors.Select(x => x.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(admin, SystemRoles.Administrator))
        {
            await userManager.AddToRoleAsync(admin, SystemRoles.Administrator);
        }
    }

    private async Task EnsureRoleAsync(
        string roleName,
        IEnumerable<string> permissions,
        CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByNameAsync(roleName);
        var created = role is null;
        if (role is null)
        {
            role = new IdentityRole<Guid>(roleName);
            var createResult = await roleManager.CreateAsync(role);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Cannot seed role {roleName}: {string.Join(", ", createResult.Errors.Select(x => x.Description))}");
            }
        }

        // Chỉ gán bộ quyền mặc định khi vai trò được tạo lần đầu. Sau đó quyền do
        // quản trị viên cấu hình trên màn hình Nhân viên phải được giữ nguyên.
        if (!created)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        var expected = permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = (await roleManager.GetClaimsAsync(role))
            .Where(x => x.Type == "permission")
            .ToList();

        foreach (var claim in existing.Where(x => !expected.Contains(x.Value)))
        {
            await roleManager.RemoveClaimAsync(role, claim);
        }

        foreach (var permission in expected.Where(x => existing.All(c => c.Value != x)))
        {
            await roleManager.AddClaimAsync(role, new Claim("permission", permission));
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}

