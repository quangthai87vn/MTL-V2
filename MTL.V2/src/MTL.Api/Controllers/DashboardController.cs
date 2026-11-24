using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MTL.Domain.Identity;
using MTL.Domain.Sales;
using MTL.Infrastructure.Persistence;

namespace MTL.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = Permissions.DashboardRead)]
public sealed class DashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var productCount = await db.Products.CountAsync(cancellationToken);
        var lowStockCount = await db.Products.CountAsync(x => x.TrackInventory && x.StockQuantity <= 2, cancellationToken);
        var customerCount = await db.Customers.CountAsync(cancellationToken);
        var monthlyRevenue = await db.SalesInvoices
            .Where(x => x.InvoiceDateUtc >= monthStart && x.RepairStatus == RepairStatus.Returned)
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0;

        return Ok(new
        {
            productCount,
            lowStockCount,
            customerCount,
            monthlyRevenue,
            generatedAtUtc = now
        });
    }
}

