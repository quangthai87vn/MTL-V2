using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MTL.Domain.Catalog;
using MTL.Domain.Identity;
using MTL.Domain.Inventory;
using MTL.Domain.Purchasing;
using MTL.Domain.Sales;
using MTL.Infrastructure.Identity;
using MTL.Infrastructure.Persistence;

namespace MTL.Api.Controllers;

[ApiController]
[Route("api/purchases")]
public sealed class PurchasesController(AppDbContext db, ICurrentUser currentUser) : ControllerBase
{
    [Authorize(Policy = Permissions.PurchasesRead)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<PurchaseListItem>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] PurchaseInvoiceStatus? status,
        [FromQuery] string? paymentStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.PurchaseInvoices.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.Number, $"%{term}%") ||
                EF.Functions.ILike(x.Supplier.Name, $"%{term}%") ||
                (x.SupplierInvoiceNumber != null && EF.Functions.ILike(x.SupplierInvoiceNumber, $"%{term}%")) ||
                x.Lines.Any(line => EF.Functions.ILike(line.ProductName, $"%{term}%") || EF.Functions.ILike(line.Sku, $"%{term}%")));
        }

        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        query = paymentStatus?.ToLowerInvariant() switch
        {
            "paid" => query.Where(x => x.PaidAmount >= x.TotalAmount && x.TotalAmount > 0),
            "partial" => query.Where(x => x.PaidAmount > 0 && x.PaidAmount < x.TotalAmount),
            "unpaid" => query.Where(x => x.PaidAmount <= 0 && x.TotalAmount > 0),
            _ => query
        };

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.ReceivedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PurchaseListItem(
                x.Id, x.Number, x.SupplierId, x.Supplier.Name, x.Supplier.TaxCode,
                x.InvoiceDateUtc, x.ReceivedAtUtc, x.DueDateUtc, x.Status,
                x.Subtotal, x.DiscountAmount, x.TaxAmount, x.TotalAmount,
                x.PaidAmount, x.TotalAmount - x.PaidAmount, x.SupplierInvoiceNumber,
                x.SupplierInvoiceSeries, x.WarehouseReceiptNumber, x.InventoryPosted,
                x.Lines.Count, x.Lines.Sum(line => line.ReceivedQuantity), x.Notes))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<PurchaseListItem>(items, total, page, pageSize));
    }

    [Authorize(Policy = Permissions.PurchasesRead)]
    [HttpGet("summary")]
    public async Task<ActionResult<PurchaseSummary>> GetSummary(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var localNow = TimeZoneInfo.ConvertTime(now, GetBusinessTimeZone());
        var localStart = new DateTimeOffset(localNow.Date, localNow.Offset);
        var utcStart = localStart.ToUniversalTime();
        var utcEnd = localStart.AddDays(1).ToUniversalTime();

        var processedValue = await db.PurchaseInvoices
            .Where(x => x.Status == PurchaseInvoiceStatus.Processed)
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0;
        var supplierDebt = await db.PurchaseInvoices
            .Where(x => x.Status == PurchaseInvoiceStatus.Processed && x.PaidAmount < x.TotalAmount)
            .SumAsync(x => (decimal?)(x.TotalAmount - x.PaidAmount), cancellationToken) ?? 0;
        var receivedToday = await db.PurchaseInvoices
            .Where(x => x.Status == PurchaseInvoiceStatus.Processed && x.ReceivedAtUtc >= utcStart && x.ReceivedAtUtc < utcEnd)
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0;
        var inProgress = await db.PurchaseInvoices.CountAsync(x => x.Status == PurchaseInvoiceStatus.InProgress, cancellationToken);

        return Ok(new PurchaseSummary(processedValue, supplierDebt, receivedToday, inProgress));
    }

    [Authorize(Policy = Permissions.PurchasesRead)]
    [HttpGet("suppliers")]
    public async Task<ActionResult<IReadOnlyList<SupplierItem>>> GetSuppliers(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var query = db.Suppliers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Name, $"%{term}%") ||
                EF.Functions.ILike(x.Code, $"%{term}%") ||
                (x.Phone != null && x.Phone.Contains(term)) ||
                (x.TaxCode != null && x.TaxCode.Contains(term)));
        }

        return Ok(await query.OrderByDescending(x => x.IsActive).ThenBy(x => x.Name)
            .Select(x => new SupplierItem(x.Id, x.Code, x.Name, x.ContactName, x.Phone, x.Email, x.Address, x.TaxCode, x.Notes, x.IsActive))
            .Take(200).ToListAsync(cancellationToken));
    }

    [Authorize(Policy = Permissions.PurchasesManage)]
    [HttpPost("suppliers")]
    public async Task<ActionResult<SupplierItem>> CreateSupplier(SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "Tên nhà cung cấp là bắt buộc." });
        var supplier = new Supplier
        {
            CompanyId = companyId,
            Code = await NextSupplierCode(cancellationToken),
            Name = request.Name.Trim(),
            ContactName = Clean(request.ContactName), Phone = Clean(request.Phone), Email = Clean(request.Email),
            Address = Clean(request.Address), TaxCode = Clean(request.TaxCode), Notes = Clean(request.Notes), IsActive = request.IsActive
        };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetSuppliers), ToSupplierItem(supplier));
    }

    [Authorize(Policy = Permissions.PurchasesManage)]
    [HttpPut("suppliers/{id:guid}")]
    public async Task<ActionResult<SupplierItem>> UpdateSupplier(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (supplier is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "Tên nhà cung cấp là bắt buộc." });
        supplier.Name = request.Name.Trim();
        supplier.ContactName = Clean(request.ContactName); supplier.Phone = Clean(request.Phone); supplier.Email = Clean(request.Email);
        supplier.Address = Clean(request.Address); supplier.TaxCode = Clean(request.TaxCode); supplier.Notes = Clean(request.Notes); supplier.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToSupplierItem(supplier));
    }

    [Authorize(Policy = Permissions.PurchasesRead)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PurchaseDetails>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await db.PurchaseInvoices.AsNoTracking()
            .Include(x => x.Supplier).Include(x => x.Lines).Include(x => x.Payments).ThenInclude(x => x.PaymentAccount)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return invoice is null ? NotFound() : Ok(ToDetails(invoice));
    }

    [Authorize(Policy = Permissions.PurchasesManage)]
    [HttpPost]
    public async Task<ActionResult<PurchaseDetails>> Create(SavePurchaseRequest request, CancellationToken cancellationToken)
    {
        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        var invoice = new PurchaseInvoice { CompanyId = companyId, Number = await NextPurchaseNumber(cancellationToken) };
        var error = await PopulateInvoice(invoice, request, companyId, cancellationToken);
        if (error is not null) return BadRequest(new { message = error });

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.PurchaseInvoices.Add(invoice);
        if (invoice.Status == PurchaseInvoiceStatus.Processed)
        {
            error = await PostInventory(invoice, cancellationToken);
            if (error is not null) return BadRequest(new { message = error });
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = invoice.Id }, ToDetails(invoice));
    }

    [Authorize(Policy = Permissions.PurchasesManage)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PurchaseDetails>> Update(Guid id, SavePurchaseRequest request, CancellationToken cancellationToken)
    {
        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        var invoice = await db.PurchaseInvoices
            .Include(x => x.Supplier).Include(x => x.Lines).Include(x => x.Payments).ThenInclude(x => x.PaymentAccount)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (invoice is null) return NotFound();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (invoice.InventoryPosted)
        {
            var reverseError = await ReverseInventory(invoice, cancellationToken);
            if (reverseError is not null) return BadRequest(new { message = reverseError });
        }

        db.PurchaseInvoiceLines.RemoveRange(invoice.Lines);
        db.PurchasePayments.RemoveRange(invoice.Payments);
        invoice.Lines.Clear(); invoice.Payments.Clear();
        var error = await PopulateInvoice(invoice, request, companyId, cancellationToken);
        if (error is not null) return BadRequest(new { message = error });

        if (invoice.Status == PurchaseInvoiceStatus.Processed && invoice.LegacySource is null)
        {
            error = await PostInventory(invoice, cancellationToken);
            if (error is not null) return BadRequest(new { message = error });
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(ToDetails(invoice));
    }

    [Authorize(Policy = Permissions.PurchasesManage)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await db.PurchaseInvoices.Include(x => x.Lines).Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (invoice is null) return NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (invoice.InventoryPosted)
        {
            var error = await ReverseInventory(invoice, cancellationToken);
            if (error is not null) return BadRequest(new { message = error });
        }
        db.PurchaseInvoiceLines.RemoveRange(invoice.Lines);
        db.PurchasePayments.RemoveRange(invoice.Payments);
        db.PurchaseInvoices.Remove(invoice);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    private async Task<string?> PopulateInvoice(PurchaseInvoice invoice, SavePurchaseRequest request, Guid companyId, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Status)) return "Trạng thái phiếu nhập không hợp lệ.";
        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == request.SupplierId && x.IsActive, cancellationToken);
        if (supplier is null) return "Nhà cung cấp không tồn tại hoặc đã ngừng sử dụng.";
        if (request.Lines.Count == 0) return "Phiếu nhập cần ít nhất một dòng hàng.";

        var productIds = request.Lines.Where(x => x.ProductId.HasValue).Select(x => x.ProductId!.Value).Distinct().ToList();
        var products = await db.Products.Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        if (products.Count != productIds.Count) return "Có sản phẩm không tồn tại trong danh mục.";

        invoice.SupplierId = supplier.Id; invoice.Supplier = supplier;
        invoice.InvoiceDateUtc = request.InvoiceDateUtc.ToUniversalTime();
        invoice.ReceivedAtUtc = request.ReceivedAtUtc.ToUniversalTime();
        invoice.DueDateUtc = request.DueDateUtc?.ToUniversalTime();
        invoice.Status = request.Status;
        invoice.SupplierInvoiceNumber = Clean(request.SupplierInvoiceNumber);
        invoice.SupplierInvoiceSeries = Clean(request.SupplierInvoiceSeries);
        invoice.WarehouseReceiptNumber = Clean(request.WarehouseReceiptNumber) ?? invoice.Number;
        invoice.Notes = Clean(request.Notes);
        invoice.InventoryPosted = false;

        decimal subtotal = 0, discount = 0, tax = 0;
        var allSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var input in request.Lines)
        {
            if (input.Quantity <= 0) return "Số lượng trên từng dòng phải lớn hơn 0.";
            if (input.ReceivedQuantity < 0 || input.ReceivedQuantity > input.Quantity) return "Số lượng thực nhập phải từ 0 đến số lượng theo hóa đơn.";
            if (input.UnitCost < 0 || input.DiscountAmount < 0) return "Đơn giá hoặc chiết khấu không hợp lệ.";
            var product = input.ProductId.HasValue ? products[input.ProductId.Value] : null;
            var sku = Clean(input.Sku) ?? product?.Sku ?? string.Empty;
            var name = Clean(input.ProductName) ?? product?.Name;
            if (string.IsNullOrWhiteSpace(name)) return "Tên hàng hóa là bắt buộc.";
            var gross = decimal.Round(input.Quantity * input.UnitCost, 2);
            var lineDiscount = Math.Min(gross, input.DiscountAmount);
            var taxable = gross - lineDiscount;
            var vatRate = input.VatRate is < 0 or > 100 ? null : input.VatRate;
            var lineTax = vatRate.HasValue ? decimal.Round(taxable * vatRate.Value / 100m, 2) : 0;
            var serials = NormalizeSerials(input.SerialNumbers);
            foreach (var serial in serials)
                if (!allSerials.Add(serial)) return $"Serial {serial} bị trùng trong phiếu nhập.";
            if (serials.Count > input.ReceivedQuantity) return $"Số serial của {name} vượt số lượng thực nhập.";

            invoice.Lines.Add(new PurchaseInvoiceLine
            {
                CompanyId = companyId, ProductId = product?.Id, Sku = sku, ProductName = name,
                Quantity = input.Quantity, ReceivedQuantity = input.ReceivedQuantity,
                UnitName = Clean(input.UnitName) ?? product?.Unit ?? "Cái", UnitCost = input.UnitCost,
                DiscountAmount = lineDiscount, VatRate = vatRate, TaxAmount = lineTax,
                LineTotal = taxable + lineTax, SerialNumbersJson = JsonSerializer.Serialize(serials), Notes = Clean(input.Notes)
            });
            subtotal += gross; discount += lineDiscount; tax += lineTax;
        }

        invoice.Subtotal = subtotal; invoice.DiscountAmount = discount; invoice.TaxAmount = tax;
        invoice.TotalAmount = subtotal - discount + tax;

        decimal paid = 0;
        var accountIds = request.Payments.Where(x => x.PaymentAccountId.HasValue).Select(x => x.PaymentAccountId!.Value).Distinct().ToList();
        var accounts = await db.PaymentAccounts.Where(x => accountIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, cancellationToken);
        if (accounts.Count != accountIds.Count) return "Tài khoản thanh toán không tồn tại hoặc đã ngừng sử dụng.";
        foreach (var input in request.Payments.Where(x => x.Amount > 0))
        {
            if (input.Method == PaymentMethod.BankTransfer && !input.PaymentAccountId.HasValue)
                return "Thanh toán chuyển khoản cần chọn tài khoản chi.";
            paid += input.Amount;
            invoice.Payments.Add(new PurchasePayment
            {
                CompanyId = companyId, Method = input.Method, PaymentAccountId = input.PaymentAccountId,
                PaymentAccount = input.PaymentAccountId.HasValue ? accounts[input.PaymentAccountId.Value] : null,
                Amount = input.Amount, PaidAtUtc = input.PaidAtUtc.ToUniversalTime(), Notes = Clean(input.Notes)
            });
        }
        if (paid > invoice.TotalAmount) return "Tổng tiền đã trả không được vượt tổng phiếu nhập.";
        invoice.PaidAmount = paid;
        return null;
    }

    private async Task<string?> PostInventory(PurchaseInvoice invoice, CancellationToken cancellationToken)
    {
        var lines = invoice.Lines.Where(x => x.ProductId.HasValue && x.ReceivedQuantity > 0).ToList();
        var productIds = lines.Select(x => x.ProductId!.Value).Distinct().ToList();
        var products = await db.Products.Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var serials = lines.SelectMany(ParseSerials).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (serials.Count > 0)
        {
            var duplicate = await db.ProductSerials.AsNoTracking().Where(x => serials.Contains(x.SerialNumber)).Select(x => x.SerialNumber).FirstOrDefaultAsync(cancellationToken);
            if (duplicate is not null) return $"Serial {duplicate} đã tồn tại trong kho.";
        }

        foreach (var line in lines)
        {
            if (!products.TryGetValue(line.ProductId!.Value, out var product)) return $"Không tìm thấy sản phẩm {line.ProductName}.";
            var oldStock = Math.Max(0, product.StockQuantity);
            var netUnitCost = line.Quantity > 0 ? (line.Quantity * line.UnitCost - line.DiscountAmount) / line.Quantity : line.UnitCost;
            var newStock = oldStock + line.ReceivedQuantity;
            product.CostPrice = newStock > 0 ? decimal.Round((oldStock * product.CostPrice + line.ReceivedQuantity * netUnitCost) / newStock, 2) : netUnitCost;
            if (product.TrackInventory)
            {
                product.StockQuantity = newStock;
                product.Availability = ProductAvailability.InStock;
            }
            product.ConcurrencyToken = Guid.NewGuid();
            db.StockMovements.Add(new StockMovement
            {
                CompanyId = invoice.CompanyId, ProductId = product.Id, Type = StockMovementType.Purchase,
                Quantity = line.ReceivedQuantity, UnitCost = netUnitCost, ReferenceType = "PurchaseInvoice",
                ReferenceId = invoice.Id, Notes = invoice.Number, OccurredAtUtc = invoice.ReceivedAtUtc
            });
            foreach (var serial in ParseSerials(line))
                db.ProductSerials.Add(new ProductSerial
                {
                    CompanyId = invoice.CompanyId, ProductId = product.Id, SerialNumber = serial,
                    Status = ProductSerialStatus.InStock, CostPrice = netUnitCost,
                    PurchasedAtUtc = invoice.ReceivedAtUtc, PurchaseInvoiceLineId = line.Id
                });
        }
        invoice.InventoryPosted = true;
        return null;
    }

    private async Task<string?> ReverseInventory(PurchaseInvoice invoice, CancellationToken cancellationToken)
    {
        var movements = await db.StockMovements.Where(x => x.ReferenceType == "PurchaseInvoice" && x.ReferenceId == invoice.Id).ToListAsync(cancellationToken);
        var productIds = movements.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products.Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        foreach (var movement in movements)
        {
            if (!products.TryGetValue(movement.ProductId, out var product)) continue;
            if (product.TrackInventory && product.StockQuantity < movement.Quantity)
                return $"Không thể hoàn tác phiếu vì tồn kho {product.Name} đã thấp hơn số lượng nhập.";
        }
        var lineIds = invoice.Lines.Select(x => x.Id).ToList();
        var serials = await db.ProductSerials.Where(x => x.PurchaseInvoiceLineId.HasValue && lineIds.Contains(x.PurchaseInvoiceLineId.Value)).ToListAsync(cancellationToken);
        var usedSerial = serials.FirstOrDefault(x => x.Status != ProductSerialStatus.InStock);
        if (usedSerial is not null) return $"Không thể hoàn tác vì serial {usedSerial.SerialNumber} đã phát sinh giao dịch.";

        foreach (var movement in movements)
        {
            if (!products.TryGetValue(movement.ProductId, out var product) || !product.TrackInventory) continue;
            product.StockQuantity -= movement.Quantity;
            product.Availability = product.StockQuantity > 0 ? ProductAvailability.InStock : ProductAvailability.OutOfStock;
            product.ConcurrencyToken = Guid.NewGuid();
        }
        db.StockMovements.RemoveRange(movements);
        db.ProductSerials.RemoveRange(serials);
        invoice.InventoryPosted = false;
        return null;
    }

    private async Task<string> NextPurchaseNumber(CancellationToken cancellationToken)
    {
        var prefix = $"PN{TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, GetBusinessTimeZone()):yyyyMMdd}-";
        var values = await db.PurchaseInvoices.AsNoTracking().Where(x => x.Number.StartsWith(prefix)).Select(x => x.Number).ToListAsync(cancellationToken);
        var next = values.Select(x => int.TryParse(x[prefix.Length..], out var value) ? value : 0).DefaultIfEmpty().Max() + 1;
        return $"{prefix}{next:000}";
    }

    private async Task<string> NextSupplierCode(CancellationToken cancellationToken)
    {
        var codes = await db.Suppliers.AsNoTracking().Where(x => x.Code.StartsWith("NCC"))
            .Select(x => x.Code).ToListAsync(cancellationToken);
        var next = codes.Select(x => int.TryParse(x[3..], out var value) ? value : 0).DefaultIfEmpty().Max() + 1;
        return $"NCC{next:000000}";
    }

    private static List<string> NormalizeSerials(IReadOnlyCollection<string>? values) => values is null
        ? []
        : values.SelectMany(x => x.Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries))
            .Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    private static List<string> ParseSerials(PurchaseInvoiceLine line) => string.IsNullOrWhiteSpace(line.SerialNumbersJson)
        ? []
        : JsonSerializer.Deserialize<List<string>>(line.SerialNumbersJson) ?? [];
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static TimeZoneInfo GetBusinessTimeZone() => TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    private static SupplierItem ToSupplierItem(Supplier x) => new(x.Id, x.Code, x.Name, x.ContactName, x.Phone, x.Email, x.Address, x.TaxCode, x.Notes, x.IsActive);
    private static PurchaseDetails ToDetails(PurchaseInvoice x) => new(
        x.Id, x.Number, ToSupplierItem(x.Supplier), x.InvoiceDateUtc, x.ReceivedAtUtc, x.DueDateUtc, x.Status,
        x.Subtotal, x.DiscountAmount, x.TaxAmount, x.TotalAmount, x.PaidAmount,
        x.SupplierInvoiceNumber, x.SupplierInvoiceSeries, x.WarehouseReceiptNumber, x.Notes, x.InventoryPosted,
        x.Lines.OrderBy(line => line.CreatedAtUtc).Select(line => new PurchaseLineItem(
            line.Id, line.ProductId, line.Sku, line.ProductName, line.Quantity, line.ReceivedQuantity,
            line.UnitName, line.UnitCost, line.DiscountAmount, line.VatRate, line.TaxAmount, line.LineTotal,
            ParseSerials(line), line.Notes)).ToList(),
        x.Payments.OrderBy(payment => payment.PaidAtUtc).Select(payment => new PurchasePaymentItem(
            payment.Id, payment.Method, payment.PaymentAccountId, payment.PaymentAccount?.Name,
            payment.Amount, payment.PaidAtUtc, payment.Notes)).ToList());
}

public sealed record PurchaseSummary(decimal ProcessedValue, decimal SupplierDebt, decimal ReceivedToday, int InProgressCount);
public sealed record PurchaseListItem(Guid Id, string Number, Guid SupplierId, string SupplierName, string? SupplierTaxCode,
    DateTimeOffset InvoiceDateUtc, DateTimeOffset ReceivedAtUtc, DateTimeOffset? DueDateUtc, PurchaseInvoiceStatus Status,
    decimal Subtotal, decimal DiscountAmount, decimal TaxAmount, decimal TotalAmount, decimal PaidAmount, decimal OutstandingAmount,
    string? SupplierInvoiceNumber, string? SupplierInvoiceSeries, string? WarehouseReceiptNumber, bool InventoryPosted,
    int LineCount, decimal ReceivedQuantity, string? Notes);
public sealed record SupplierItem(Guid Id, string Code, string Name, string? ContactName, string? Phone, string? Email,
    string? Address, string? TaxCode, string? Notes, bool IsActive);
public sealed record SaveSupplierRequest(string Name, string? ContactName, string? Phone, string? Email, string? Address,
    string? TaxCode, string? Notes, bool IsActive = true);
public sealed record PurchaseDetails(Guid Id, string Number, SupplierItem Supplier, DateTimeOffset InvoiceDateUtc,
    DateTimeOffset ReceivedAtUtc, DateTimeOffset? DueDateUtc, PurchaseInvoiceStatus Status, decimal Subtotal,
    decimal DiscountAmount, decimal TaxAmount, decimal TotalAmount, decimal PaidAmount, string? SupplierInvoiceNumber,
    string? SupplierInvoiceSeries, string? WarehouseReceiptNumber, string? Notes, bool InventoryPosted,
    IReadOnlyList<PurchaseLineItem> Lines, IReadOnlyList<PurchasePaymentItem> Payments);
public sealed record PurchaseLineItem(Guid Id, Guid? ProductId, string Sku, string ProductName, decimal Quantity,
    decimal ReceivedQuantity, string UnitName, decimal UnitCost, decimal DiscountAmount, decimal? VatRate,
    decimal TaxAmount, decimal LineTotal, IReadOnlyList<string> SerialNumbers, string? Notes);
public sealed record PurchasePaymentItem(Guid Id, PaymentMethod Method, Guid? PaymentAccountId, string? PaymentAccountName,
    decimal Amount, DateTimeOffset PaidAtUtc, string? Notes);
public sealed record SavePurchaseRequest(Guid SupplierId, DateTimeOffset InvoiceDateUtc, DateTimeOffset ReceivedAtUtc,
    DateTimeOffset? DueDateUtc, PurchaseInvoiceStatus Status, string? SupplierInvoiceNumber, string? SupplierInvoiceSeries,
    string? WarehouseReceiptNumber, string? Notes, IReadOnlyList<SavePurchaseLineRequest> Lines,
    IReadOnlyList<SavePurchasePaymentRequest> Payments);
public sealed record SavePurchaseLineRequest(Guid? ProductId, string? Sku, string? ProductName, decimal Quantity,
    decimal ReceivedQuantity, string? UnitName, decimal UnitCost, decimal DiscountAmount, decimal? VatRate,
    IReadOnlyCollection<string>? SerialNumbers, string? Notes);
public sealed record SavePurchasePaymentRequest(PaymentMethod Method, Guid? PaymentAccountId, decimal Amount,
    DateTimeOffset PaidAtUtc, string? Notes);
