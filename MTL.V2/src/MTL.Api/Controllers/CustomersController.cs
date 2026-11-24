using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MTL.Domain.Crm;
using MTL.Domain.Identity;
using MTL.Domain.Sales;
using MTL.Infrastructure.Identity;
using MTL.Infrastructure.Persistence;

namespace MTL.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(AppDbContext db, ICurrentUser currentUser) : ControllerBase
{
    [Authorize(Policy = Permissions.CustomersRead)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDetailsItem>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return customer is null ? NotFound() : Ok(ToDetails(customer));
    }

    [Authorize(Policy = Permissions.CustomersRead)]
    [HttpGet("lookup")]
    public async Task<ActionResult<CustomerLookupResult>> LookupByPhone(
        [FromQuery] string phone,
        CancellationToken cancellationToken)
    {
        var normalizedPhone = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (normalizedPhone.Length < 8)
            return BadRequest(new { message = "Số điện thoại chưa hợp lệ." });

        var customer = await db.Customers.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Phone == normalizedPhone, cancellationToken);
        if (customer is null) return NotFound(new { message = "Chưa tìm thấy khách hàng theo số điện thoại này." });

        var invoices = await db.SalesInvoices.AsNoTracking()
            .Where(x => x.CustomerId == customer.Id)
            .OrderByDescending(x => x.InvoiceDateUtc)
            .Take(30)
            .Select(x => new CustomerInvoiceHistoryItem(
                x.Id,
                x.Number,
                x.Type,
                x.InvoiceDateUtc,
                x.Status,
                x.TotalAmount,
                x.PaidAmount,
                x.TotalAmount - x.PaidAmount))
            .ToListAsync(cancellationToken);

        return Ok(new CustomerLookupResult(
            customer.Id,
            customer.Code,
            customer.FullName,
            customer.Phone,
            customer.Email,
            customer.Address,
            customer.CitizenId,
            customer.Notes,
            customer.TotalSpent,
            customer.LastPurchaseAtUtc,
            invoices));
    }

    [Authorize(Policy = Permissions.CustomersRead)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<CustomerListItem>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.FullName, $"%{term}%") ||
                (x.Phone != null && x.Phone.Contains(term)) ||
                (x.CitizenId != null && x.CitizenId.Contains(term)) ||
                (x.Email != null && EF.Functions.ILike(x.Email, $"%{term}%")));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CustomerListItem(
                x.Id,
                x.Code,
                x.FullName,
                x.Phone,
                x.Email,
                x.CitizenId,
                x.TotalSpent,
                x.LastPurchaseAtUtc))
            .ToListAsync(cancellationToken);
        return Ok(new PagedResult<CustomerListItem>(items, total, page, pageSize));
    }

    [Authorize(Policy = Permissions.CustomersManage)]
    [HttpPost]
    public async Task<ActionResult<CustomerDetailsItem>> Create(
        SaveCustomerRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { message = "Tên khách hàng là bắt buộc." });
        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        var normalizedPhone = NormalizePhone(request.Phone);
        if (normalizedPhone is not null && await db.Customers.AnyAsync(x => x.Phone == normalizedPhone, cancellationToken))
            return Conflict(new { message = "Số điện thoại này đã thuộc một khách hàng khác." });
        var customer = new Customer
        {
            CompanyId = companyId,
            Code = await NextCustomerCode(companyId, cancellationToken),
            FullName = request.FullName.Trim(),
            Phone = normalizedPhone,
            Email = Clean(request.Email),
            Address = Clean(request.Address),
            CitizenId = Clean(request.CitizenId),
            TaxCode = Clean(request.TaxCode),
            Birthday = request.Birthday,
            Notes = Clean(request.Notes)
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToDetails(customer));
    }

    [Authorize(Policy = Permissions.CustomersManage)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerDetailsItem>> Update(
        Guid id,
        SaveCustomerRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { message = "Tên khách hàng là bắt buộc." });
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (customer is null) return NotFound();
        var normalizedPhone = NormalizePhone(request.Phone);
        if (normalizedPhone is not null && await db.Customers.AnyAsync(x => x.Id != id && x.Phone == normalizedPhone, cancellationToken))
            return Conflict(new { message = "Số điện thoại này đã thuộc một khách hàng khác." });

        customer.FullName = request.FullName.Trim();
        customer.Phone = normalizedPhone;
        customer.Email = Clean(request.Email);
        customer.Address = Clean(request.Address);
        customer.CitizenId = Clean(request.CitizenId);
        customer.TaxCode = Clean(request.TaxCode);
        customer.Birthday = request.Birthday;
        customer.Notes = Clean(request.Notes);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToDetails(customer));
    }

    private static CustomerDetailsItem ToDetails(Customer customer) => new(
        customer.Id,
        customer.Code,
        customer.FullName,
        customer.Phone,
        customer.Email,
        customer.Address,
        customer.CitizenId,
        customer.TaxCode,
        customer.Birthday,
        customer.Notes,
        customer.TotalSpent,
        customer.LastPurchaseAtUtc);

    private static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = new string(value.Where(char.IsDigit).ToArray());
        return normalized.Length == 0 ? null : normalized;
    }

    private async Task<string> NextCustomerCode(Guid companyId, CancellationToken cancellationToken)
    {
        var codes = await db.Customers.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.Code.StartsWith("KH"))
            .Select(x => x.Code)
            .ToListAsync(cancellationToken);
        var nextNumber = codes
            .Select(code => int.TryParse(code.AsSpan(2), out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"KH{nextNumber:000000}";
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record SaveCustomerRequest(
    string FullName,
    string? Phone,
    string? Email,
    string? Address,
    string? CitizenId,
    string? TaxCode,
    DateOnly? Birthday,
    string? Notes);

public sealed record CustomerListItem(
    Guid Id,
    string Code,
    string FullName,
    string? Phone,
    string? Email,
    string? CitizenId,
    decimal TotalSpent,
    DateTimeOffset? LastPurchaseAtUtc);

public sealed record CustomerDetailsItem(
    Guid Id,
    string Code,
    string FullName,
    string? Phone,
    string? Email,
    string? Address,
    string? CitizenId,
    string? TaxCode,
    DateOnly? Birthday,
    string? Notes,
    decimal TotalSpent,
    DateTimeOffset? LastPurchaseAtUtc);

public sealed record CustomerLookupResult(
    Guid Id,
    string Code,
    string FullName,
    string? Phone,
    string? Email,
    string? Address,
    string? CitizenId,
    string? Notes,
    decimal TotalSpent,
    DateTimeOffset? LastPurchaseAtUtc,
    IReadOnlyList<CustomerInvoiceHistoryItem> Invoices);

public sealed record CustomerInvoiceHistoryItem(
    Guid Id,
    string Number,
    SalesInvoiceType Type,
    DateTimeOffset InvoiceDateUtc,
    InvoiceStatus Status,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal OutstandingAmount);

