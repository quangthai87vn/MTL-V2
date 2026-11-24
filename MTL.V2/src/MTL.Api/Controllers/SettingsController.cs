using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MTL.Domain.Identity;
using MTL.Infrastructure.Identity;
using MTL.Infrastructure.Persistence;

namespace MTL.Api.Controllers;

[ApiController]
[Route("api/settings")]
public sealed class SettingsController(AppDbContext db, ICurrentUser currentUser) : ControllerBase
{
    [Authorize(Policy = Permissions.SettingsManage)]
    [HttpGet("company")]
    public async Task<ActionResult<CompanySettingsItem>> GetCompany(CancellationToken cancellationToken)
    {
        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        var company = await db.Companies.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == companyId && !x.IsDeleted, cancellationToken);
        return company is null ? NotFound() : Ok(new CompanySettingsItem(
            company.Id,
            company.Name,
            company.Code,
            company.TaxCode,
            company.Address,
            company.Phone,
            company.Email,
            company.Website,
            company.LogoDataUrl));
    }

    [Authorize(Policy = Permissions.SettingsManage)]
    [HttpPut("company")]
    public async Task<ActionResult<CompanySettingsItem>> UpdateCompany(
        SaveCompanySettingsRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        var company = await db.Companies.SingleOrDefaultAsync(x => x.Id == companyId, cancellationToken);
        if (company is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Tên công ty là bắt buộc." });
        if (!string.IsNullOrWhiteSpace(request.LogoDataUrl))
        {
            if (!request.LogoDataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Logo phải là tập tin hình ảnh." });
            if (request.LogoDataUrl.Length > 3_000_000)
                return BadRequest(new { message = "Logo không được lớn hơn 2 MB." });
        }

        company.Name = request.Name.Trim();
        company.TaxCode = Clean(request.TaxCode);
        company.Address = Clean(request.Address);
        company.Phone = Clean(request.Phone);
        company.Email = Clean(request.Email);
        company.Website = Clean(request.Website);
        company.LogoDataUrl = Clean(request.LogoDataUrl);
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new CompanySettingsItem(
            company.Id,
            company.Name,
            company.Code,
            company.TaxCode,
            company.Address,
            company.Phone,
            company.Email,
            company.Website,
            company.LogoDataUrl));
    }

    [Authorize(Policy = Permissions.SalesRead)]
    [HttpGet("invoice")]
    public async Task<ActionResult<InvoiceSettingsItem>> GetInvoice(CancellationToken cancellationToken)
    {
        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        var settings = await db.Companies.AsNoTracking()
            .Where(x => x.Id == companyId && !x.IsDeleted)
            .Select(x => new InvoiceSettingsItem(x.DefaultRepairTurnaroundDays))
            .SingleOrDefaultAsync(cancellationToken);
        return settings is null ? NotFound() : Ok(settings);
    }

    [Authorize(Policy = Permissions.SettingsManage)]
    [HttpPut("invoice")]
    public async Task<ActionResult<InvoiceSettingsItem>> UpdateInvoice(
        SaveInvoiceSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DefaultRepairTurnaroundDays is < 1 or > 90)
            return BadRequest(new { message = "Số ngày dự kiến trả máy phải từ 1 đến 90 ngày." });

        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        var company = await db.Companies.SingleOrDefaultAsync(x => x.Id == companyId, cancellationToken);
        if (company is null) return NotFound();

        company.DefaultRepairTurnaroundDays = request.DefaultRepairTurnaroundDays;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new InvoiceSettingsItem(company.DefaultRepairTurnaroundDays));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record CompanySettingsItem(
    Guid Id,
    string Name,
    string Code,
    string? TaxCode,
    string? Address,
    string? Phone,
    string? Email,
    string? Website,
    string? LogoDataUrl);

public sealed record SaveCompanySettingsRequest(
    string Name,
    string? TaxCode,
    string? Address,
    string? Phone,
    string? Email,
    string? Website,
    string? LogoDataUrl);

public sealed record InvoiceSettingsItem(int DefaultRepairTurnaroundDays);
public sealed record SaveInvoiceSettingsRequest(int DefaultRepairTurnaroundDays);
