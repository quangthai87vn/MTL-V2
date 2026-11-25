using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MTL.Domain.Catalog;
using MTL.Domain.Crm;
using MTL.Domain.Identity;
using MTL.Domain.Inventory;
using MTL.Domain.Sales;
using MTL.Infrastructure.Identity;
using MTL.Infrastructure.Persistence;

namespace MTL.Api.Controllers;

[ApiController]
[Route("api/sales")]
public sealed class SalesController(AppDbContext db, ICurrentUser currentUser) : ControllerBase
{
    [Authorize(Policy = Permissions.SalesRead)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<SalesInvoiceListItem>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] SalesInvoiceType? type,
        [FromQuery] InvoiceStatus? status,
        [FromQuery] Guid? serviceTypeId,
        [FromQuery] string? repairProgress,
        [FromQuery] string? saleMode,
        [FromQuery] string? paymentMethod,
        [FromQuery] Guid? createdByUserId,
        [FromQuery] Guid? paymentAccountId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.SalesInvoices.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var pattern = $"%{term}%";
            var normalizedTerm = NormalizeSearchKeyword(term);
            var hasBusinessDate = TryParseBusinessDate(term, out var dateStartUtc, out var dateEndUtc);
            var hasMoney = TryParseMoneySearch(term, out var money);
            var matchesSale = normalizedTerm.Contains("ban san pham", StringComparison.Ordinal) || normalizedTerm == "ban hang";
            var matchesRepair = normalizedTerm.Contains("sua chua", StringComparison.Ordinal);
            var matchesInstallment = normalizedTerm.Contains("tra gop", StringComparison.Ordinal);
            var matchesDirect = normalizedTerm.Contains("ban tien mat", StringComparison.Ordinal) || normalizedTerm.Contains("ban truc tiep", StringComparison.Ordinal);
            var matchesMixedPayment = normalizedTerm.Contains("tien mat + chuyen khoan", StringComparison.Ordinal);
            var matchesCashPayment = normalizedTerm.Contains("tien mat", StringComparison.Ordinal);
            var matchesBankPayment = normalizedTerm.Contains("chuyen khoan", StringComparison.Ordinal) || normalizedTerm.Contains("banking", StringComparison.Ordinal);
            var matchesUnallocated = normalizedTerm.Contains("chua phan bo", StringComparison.Ordinal);
            var matchesNoBank = normalizedTerm.Contains("khong co chuyen khoan", StringComparison.Ordinal);
            var matchesProcessed = normalizedTerm.Contains("da xu ly", StringComparison.Ordinal);
            var matchesPending = normalizedTerm.Contains("dang xu ly", StringComparison.Ordinal);
            var matchesCancelled = normalizedTerm.Contains("da huy", StringComparison.Ordinal);
            var matchesLegacyCreator = normalizedTerm.Contains("du lieu cu", StringComparison.Ordinal);
            var matchesMissingPhone = normalizedTerm.Contains("chua co so dien thoai", StringComparison.Ordinal);
            var matchesNoServiceTag = normalizedTerm.Contains("chua gan the", StringComparison.Ordinal);
            query = query.Where(x =>
                EF.Functions.ILike(x.Number, pattern) ||
                EF.Functions.ILike(x.CustomerName, pattern) ||
                (x.CustomerPhone != null && x.CustomerPhone.Contains(term)) ||
                (x.Notes != null && EF.Functions.ILike(x.Notes, pattern)) ||
                (x.InstallmentProvider != null && EF.Functions.ILike(x.InstallmentProvider, pattern)) ||
                x.Lines.Any(line =>
                    EF.Functions.ILike(line.ProductName, pattern) ||
                    EF.Functions.ILike(line.Sku, pattern) ||
                    (line.Notes != null && EF.Functions.ILike(line.Notes, pattern))) ||
                x.Repairs.Any(repair =>
                    EF.Functions.ILike(repair.DeviceModel, pattern) ||
                    (repair.SerialNumber != null && EF.Functions.ILike(repair.SerialNumber, pattern)) ||
                    EF.Functions.ILike(repair.CustomerRequest, pattern) ||
                    (repair.Notes != null && EF.Functions.ILike(repair.Notes, pattern))) ||
                x.ServiceTypes.Any(link =>
                    EF.Functions.ILike(link.ServiceType.Name, pattern) ||
                    EF.Functions.ILike(link.ServiceType.Code, pattern)) ||
                x.Payments.Any(payment =>
                    (payment.Reference != null && EF.Functions.ILike(payment.Reference, pattern)) ||
                    (payment.PaymentAccount != null &&
                        (EF.Functions.ILike(payment.PaymentAccount.Name, pattern) ||
                         EF.Functions.ILike(payment.PaymentAccount.BankName, pattern) ||
                         EF.Functions.ILike(payment.PaymentAccount.AccountNumber, pattern) ||
                         EF.Functions.ILike(payment.PaymentAccount.AccountHolder, pattern)))) ||
                (x.InstallmentPaymentAccount != null &&
                    (EF.Functions.ILike(x.InstallmentPaymentAccount.Name, pattern) ||
                     EF.Functions.ILike(x.InstallmentPaymentAccount.BankName, pattern) ||
                     EF.Functions.ILike(x.InstallmentPaymentAccount.AccountNumber, pattern) ||
                     EF.Functions.ILike(x.InstallmentPaymentAccount.AccountHolder, pattern))) ||
                (x.CreatedByUserId.HasValue && db.Users.Any(user =>
                    user.Id == x.CreatedByUserId.Value && EF.Functions.ILike(user.FullName, pattern))) ||
                (matchesLegacyCreator && !x.CreatedByUserId.HasValue) ||
                (matchesMissingPhone && x.CustomerPhone == null) ||
                (matchesNoServiceTag && !x.ServiceTypes.Any()) ||
                (matchesSale && x.Type == SalesInvoiceType.Sale) ||
                (matchesRepair && x.Type == SalesInvoiceType.Repair) ||
                (matchesInstallment && x.IsInstallment) ||
                (matchesDirect && !x.IsInstallment) ||
                (matchesMixedPayment &&
                    x.Payments.Any(payment => payment.Amount > 0 && payment.Method == PaymentMethod.Cash) &&
                    x.Payments.Any(payment => payment.Amount > 0 && payment.Method == PaymentMethod.BankTransfer)) ||
                (!matchesMixedPayment && matchesCashPayment &&
                    x.Payments.Any(payment => payment.Amount > 0 && payment.Method == PaymentMethod.Cash)) ||
                (!matchesMixedPayment && !matchesNoBank && matchesBankPayment &&
                    x.Payments.Any(payment => payment.Amount > 0 && payment.Method == PaymentMethod.BankTransfer)) ||
                (matchesUnallocated && !x.Payments.Any(payment => payment.Amount > 0)) ||
                (matchesNoBank && !x.Payments.Any(payment => payment.Amount > 0 && payment.Method == PaymentMethod.BankTransfer)) ||
                (matchesProcessed && x.RepairStatus == RepairStatus.Returned) ||
                (matchesPending && x.RepairStatus != RepairStatus.Returned && x.RepairStatus != RepairStatus.Cancelled) ||
                (matchesCancelled && x.RepairStatus == RepairStatus.Cancelled) ||
                (hasMoney &&
                    (x.TotalAmount == money || x.PaidAmount == money || x.TotalAmount - x.PaidAmount == money ||
                     x.Payments.Any(payment => payment.Amount == money))) ||
                (hasBusinessDate &&
                    ((x.InvoiceDateUtc >= dateStartUtc && x.InvoiceDateUtc < dateEndUtc) ||
                     (x.ExpectedReturnAtUtc.HasValue && x.ExpectedReturnAtUtc.Value >= dateStartUtc && x.ExpectedReturnAtUtc.Value < dateEndUtc) ||
                     (x.UpdatedAtUtc >= dateStartUtc && x.UpdatedAtUtc < dateEndUtc))));
        }

        if (type.HasValue) query = query.Where(x => x.Type == type.Value);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (serviceTypeId.HasValue) query = query.Where(x => x.ServiceTypes.Any(link => link.ServiceTypeId == serviceTypeId.Value));
        if (createdByUserId.HasValue) query = query.Where(x => x.CreatedByUserId == createdByUserId.Value);
        if (paymentAccountId.HasValue)
            query = query.Where(x => x.InstallmentPaymentAccountId == paymentAccountId.Value ||
                x.Payments.Any(payment => payment.PaymentAccountId == paymentAccountId.Value));
        if (string.Equals(saleMode, "installment", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.IsInstallment);
        else if (string.Equals(saleMode, "direct", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => !x.IsInstallment);

        var hasCashPayment = (System.Linq.Expressions.Expression<Func<SalesInvoice, bool>>)(invoice =>
            invoice.Payments.Any(payment => payment.Amount > 0 && payment.Method == PaymentMethod.Cash));
        var hasBankPayment = (System.Linq.Expressions.Expression<Func<SalesInvoice, bool>>)(invoice =>
            invoice.Payments.Any(payment => payment.Amount > 0 && payment.Method == PaymentMethod.BankTransfer));
        if (string.Equals(paymentMethod, "cash", StringComparison.OrdinalIgnoreCase))
            query = query.Where(hasCashPayment);
        else if (string.Equals(paymentMethod, "bank", StringComparison.OrdinalIgnoreCase))
            query = query.Where(hasBankPayment);
        else if (string.Equals(paymentMethod, "mixed", StringComparison.OrdinalIgnoreCase))
            query = query.Where(invoice =>
                invoice.Payments.Any(payment => payment.Amount > 0 && payment.Method == PaymentMethod.Cash) &&
                invoice.Payments.Any(payment => payment.Amount > 0 && payment.Method == PaymentMethod.BankTransfer));
        else if (string.Equals(paymentMethod, "unallocated", StringComparison.OrdinalIgnoreCase))
            query = query.Where(invoice => !invoice.Payments.Any(payment => payment.Amount > 0));

        if (string.Equals(repairProgress, "pending", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.RepairStatus != RepairStatus.Returned && x.RepairStatus != RepairStatus.Cancelled);
        else if (string.Equals(repairProgress, "returned", StringComparison.OrdinalIgnoreCase) || string.Equals(repairProgress, "processed", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.RepairStatus == RepairStatus.Returned);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ThenByDescending(x => x.InvoiceDateUtc)
            .ThenByDescending(x => x.Number)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SalesInvoiceListItem(
                x.Id,
                x.Number,
                x.Type,
                x.CustomerId,
                x.CustomerName,
                x.CustomerPhone,
                x.InvoiceDateUtc,
                x.Status,
                x.TotalAmount,
                x.RepairStatus == RepairStatus.Returned ? x.PaidAmount : 0,
                x.RepairStatus == RepairStatus.Returned ? x.TotalAmount - x.PaidAmount : x.TotalAmount,
                x.IsInstallment,
                x.PaymentDueDateUtc,
                x.Repairs.OrderBy(repair => repair.CreatedAtUtc).Select(repair => repair.DeviceModel).FirstOrDefault(),
                x.ExpectedReturnAtUtc,
                x.RepairStatus,
                x.Notes ?? x.Lines.OrderBy(line => line.CreatedAtUtc).Select(line => line.ProductName).FirstOrDefault())
            {
                CreatedByUserId = x.CreatedByUserId,
                RepairDeviceCount = x.Repairs.Count,
                RepairPendingCount = x.RepairStatus != RepairStatus.ReadyForReturn && x.RepairStatus != RepairStatus.Returned && x.RepairStatus != RepairStatus.Cancelled ? x.Repairs.Count : 0,
                RepairReadyCount = x.RepairStatus == RepairStatus.ReadyForReturn ? x.Repairs.Count : 0,
                RepairReturnedCount = x.RepairStatus == RepairStatus.Returned ? x.Repairs.Count : 0,
                ReceivedAmount = x.RepairStatus == RepairStatus.Returned ? x.Payments.Where(payment => !payment.IsScheduled).Sum(payment => payment.Amount) : 0,
                ScheduledAmount = x.RepairStatus == RepairStatus.Returned ? x.Payments.Where(payment => payment.IsScheduled).Sum(payment => payment.Amount) : 0,
                UpdatedAtUtc = x.UpdatedAtUtc,
                InstallmentProvider = x.InstallmentProvider,
                PaymentSources = x.Payments
                    .Where(payment => payment.Amount > 0)
                    .OrderBy(payment => payment.IsScheduled)
                    .ThenBy(payment => payment.PaidAtUtc)
                    .Select(payment => new SalesPaymentSourceListItem(
                        payment.Method,
                        payment.PaymentAccountId,
                        payment.PaymentAccount == null ? null : payment.PaymentAccount.Name,
                        payment.PaymentAccount == null ? null : payment.PaymentAccount.BankName,
                        payment.PaymentAccount == null ? null : payment.PaymentAccount.AccountNumber,
                        payment.Amount,
                        payment.IsScheduled))
                    .ToList(),
                ServiceTypes = x.ServiceTypes.OrderBy(link => link.ServiceType.DisplayOrder).ThenBy(link => link.ServiceType.Name)
                    .Select(link => new ServiceTypeItem(link.ServiceType.Id, link.ServiceType.Name, link.ServiceType.Code, link.ServiceType.IsActive, link.ServiceType.DisplayOrder)).ToList()
            })
            .ToListAsync(cancellationToken);

        var customerIds = items.Where(x => x.CustomerId.HasValue).Select(x => x.CustomerId!.Value).Distinct().ToList();
        if (customerIds.Count > 0)
        {
            var customerStats = await db.Customers.AsNoTracking()
                .Where(x => customerIds.Contains(x.Id))
                .Select(x => new
                {
                    x.Id,
                    x.TotalSpent,
                    InvoiceCount = db.SalesInvoices.Count(invoice => invoice.CustomerId == x.Id && invoice.RepairStatus == RepairStatus.Returned)
                })
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            items = items.Select(item => item.CustomerId.HasValue && customerStats.TryGetValue(item.CustomerId.Value, out var stats)
                ? item with { CustomerTotalSpent = stats.TotalSpent, CustomerInvoiceCount = stats.InvoiceCount }
                : item).ToList();
        }

        var creatorIds = items.Where(x => x.CreatedByUserId.HasValue).Select(x => x.CreatedByUserId!.Value).Distinct().ToList();
        if (creatorIds.Count > 0)
        {
            var creatorNames = await db.Users.AsNoTracking()
                .Where(x => creatorIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);

            items = items.Select(item => item.CreatedByUserId.HasValue && creatorNames.TryGetValue(item.CreatedByUserId.Value, out var creatorName)
                ? item with { CreatedByName = creatorName }
                : item).ToList();
        }

        return Ok(new PagedResult<SalesInvoiceListItem>(items, total, page, pageSize));
    }

    [Authorize(Policy = Permissions.SalesRead)]
    [HttpGet("filter-options")]
    public async Task<ActionResult<SalesFilterOptions>> GetFilterOptions(CancellationToken cancellationToken)
    {
        var creatorIds = await db.SalesInvoices.AsNoTracking()
            .Where(invoice => invoice.CreatedByUserId.HasValue)
            .Select(invoice => invoice.CreatedByUserId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        var creators = await db.Users.AsNoTracking()
            .Where(user => creatorIds.Contains(user.Id))
            .OrderBy(user => user.FullName)
            .Select(user => new SalesCreatorFilterItem(user.Id, user.FullName))
            .ToListAsync(cancellationToken);
        var paymentAccounts = await db.PaymentAccounts.AsNoTracking()
            .OrderByDescending(account => account.IsActive)
            .ThenBy(account => account.DisplayOrder)
            .ThenBy(account => account.Name)
            .Select(account => new PaymentAccountItem(
                account.Id,
                account.Name,
                account.BankName,
                account.AccountNumber,
                account.AccountHolder,
                account.IsActive,
                account.DisplayOrder))
            .ToListAsync(cancellationToken);

        return Ok(new SalesFilterOptions(creators, paymentAccounts));
    }

    [Authorize(Policy = Permissions.SalesRead)]
    [HttpGet("summary")]
    public async Task<ActionResult<SalesSummary>> GetSummary(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var localNow = TimeZoneInfo.ConvertTime(now, GetBusinessTimeZone());
        var localStart = new DateTimeOffset(localNow.Date, localNow.Offset);
        var utcStart = localStart.ToUniversalTime();
        var utcEnd = localStart.AddDays(1).ToUniversalTime();

        var invoiceTotalToday = await db.SalesInvoices
            .Where(x => x.InvoiceDateUtc >= utcStart && x.InvoiceDateUtc < utcEnd && x.RepairStatus == RepairStatus.Returned)
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0;
        var collectedToday = await db.SalesPayments
            .Where(x => x.PaidAtUtc >= utcStart && x.PaidAtUtc < utcEnd && x.SalesInvoice.RepairStatus == RepairStatus.Returned)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
        var outstanding = await db.SalesInvoices
            .Where(x => x.RepairStatus == RepairStatus.Returned && x.Status != InvoiceStatus.Paid)
            .SumAsync(x => (decimal?)(x.TotalAmount - x.PaidAmount), cancellationToken) ?? 0;
        var activeRepairs = await db.SalesInvoices
            .Where(x => x.Type == SalesInvoiceType.Repair &&
                x.RepairStatus != RepairStatus.Returned && x.RepairStatus != RepairStatus.Cancelled)
            .SumAsync(x => x.Repairs.Count, cancellationToken);
        const int readyForReturnRepairs = 0;

        return Ok(new SalesSummary(invoiceTotalToday, collectedToday, outstanding, activeRepairs, readyForReturnRepairs));
    }

    [Authorize(Policy = Permissions.SalesRead)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SalesInvoiceDetails>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await db.SalesInvoices.AsNoTracking()
            .Include(x => x.Lines)
            .Include(x => x.Discounts)
            .Include(x => x.Payments).ThenInclude(x => x.PaymentAccount)
            .Include(x => x.InstallmentPaymentAccount)
            .Include(x => x.Repairs).ThenInclude(x => x.ServiceTypes).ThenInclude(x => x.ServiceType)
            .Include(x => x.ServiceTypes).ThenInclude(x => x.ServiceType)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return invoice is null ? NotFound() : Ok(await ToDetailsAsync(invoice, cancellationToken));
    }

    [Authorize(Policy = Permissions.SalesManage)]
    [HttpPost]
    public async Task<ActionResult<SalesInvoiceDetails>> Create(
        CreateSalesInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        await EnsureDefaultServiceTypesAsync(companyId, cancellationToken);
        await EnsureDefaultInstallmentProvidersAsync(companyId, cancellationToken);
        var activeServiceTypes = await db.ServiceTypes.Where(x => x.IsActive).ToDictionaryAsync(x => x.Id, cancellationToken);
        var defaultRepairTurnaroundDays = await db.Companies.AsNoTracking()
            .Where(x => x.Id == companyId)
            .Select(x => x.DefaultRepairTurnaroundDays)
            .SingleAsync(cancellationToken);
        var selectedInvoiceServiceTypeIds = new HashSet<Guid>();
        if (string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.CustomerPhone))
            return BadRequest(new { message = "Tên và số điện thoại khách hàng là bắt buộc." });

        var normalizedPhone = NormalizePhone(request.CustomerPhone);
        if (normalizedPhone.Length < 8)
            return BadRequest(new { message = "Số điện thoại khách hàng không hợp lệ." });

        var installmentProviderName = Clean(request.InstallmentProvider);
        if (request.IsInstallment)
        {
            if (installmentProviderName is null)
                return BadRequest(new { message = "Hóa đơn trả góp cần chọn đơn vị trả góp." });
            installmentProviderName = await db.InstallmentProviders
                .Where(x => x.IsActive && x.Name == installmentProviderName)
                .Select(x => x.Name)
                .SingleOrDefaultAsync(cancellationToken);
            if (installmentProviderName is null)
                return BadRequest(new { message = "Đơn vị trả góp không tồn tại hoặc đã bị tắt." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Phone == normalizedPhone, cancellationToken);
        if (customer is null)
        {
            customer = new Customer
            {
                CompanyId = companyId,
                Code = await NextCustomerCode(cancellationToken),
                FullName = request.CustomerName.Trim(),
                Phone = normalizedPhone,
                Email = Clean(request.CustomerEmail),
                Address = Clean(request.CustomerAddress),
                Notes = Clean(request.CustomerNotes)
            };
            db.Customers.Add(customer);
        }
        else
        {
            customer.FullName = request.CustomerName.Trim();
            customer.Email = Clean(request.CustomerEmail) ?? customer.Email;
            customer.Address = Clean(request.CustomerAddress) ?? customer.Address;
            customer.Notes = Clean(request.CustomerNotes) ?? customer.Notes;
        }

        var now = DateTimeOffset.UtcNow;
        var invoiceDate = (request.InvoiceDateUtc ?? now).ToUniversalTime();
        var invoice = new SalesInvoice
        {
            CompanyId = companyId,
            Number = await NextInvoiceNumber(request.Type, now, cancellationToken),
            Type = request.Type,
            CustomerId = customer.Id,
            CustomerName = customer.FullName,
            CustomerPhone = customer.Phone,
            CustomerEmail = customer.Email,
            CustomerAddress = customer.Address,
            InvoiceDateUtc = invoiceDate,
            ExpectedReturnAtUtc = request.Type == SalesInvoiceType.Repair ? (request.ExpectedReturnAtUtc?.ToUniversalTime() ?? invoiceDate.AddDays(defaultRepairTurnaroundDays)) : null,
            RepairStatus = NormalizeProcessingStatus(request.RepairStatus),
            IsInstallment = request.IsInstallment,
            InstallmentProvider = installmentProviderName,
            InstallmentReference = Clean(request.InstallmentReference),
            InstallmentPaymentAccountId = request.IsInstallment ? request.InstallmentPaymentAccountId : null,
            PaymentDueDateUtc = request.PaymentDueDateUtc?.ToUniversalTime(),
            Notes = Clean(request.Notes)
        };

        if (!TryAddServiceTypeIds(request.ServiceTypeIds, activeServiceTypes, selectedInvoiceServiceTypeIds, out var serviceTypeError))
            return BadRequest(new { message = serviceTypeError });

        if (request.Type == SalesInvoiceType.Repair)
        {
            var repairRequests = GetRepairRequests(request);
            if (repairRequests.Count == 0)
                return BadRequest(new { message = "Phiếu tiếp nhận cần ít nhất một máy sửa chữa." });

            foreach (var repairRequest in repairRequests)
            {
                if (string.IsNullOrWhiteSpace(repairRequest.DeviceModel))
                    return BadRequest(new { message = "Mỗi máy cần có model." });
                var repairServices = GetRepairServices(repairRequest);
                if (repairServices.Any(x => x.Amount < 0))
                    return BadRequest(new { message = $"Giá dịch vụ cho máy {repairRequest.DeviceModel} không hợp lệ." });
                var repairServiceTypeIds = repairServices.Select(x => x.ServiceTypeId).Distinct().ToList();
                if (repairServiceTypeIds.Count == 0)
                    return BadRequest(new { message = $"Hãy chọn ít nhất một loại dịch vụ cho máy {repairRequest.DeviceModel}." });
                if (!TryAddServiceTypeIds(repairServiceTypeIds, activeServiceTypes, selectedInvoiceServiceTypeIds, out serviceTypeError))
                    return BadRequest(new { message = serviceTypeError });

                var repairTotal = repairServices.Sum(x => x.Amount);
                var repair = new RepairOrder
                {
                    CompanyId = companyId,
                    SalesInvoiceId = invoice.Id,
                    DeviceModel = repairRequest.DeviceModel.Trim(),
                    SerialNumber = Clean(repairRequest.SerialNumber),
                    Accessories = Clean(repairRequest.Accessories),
                    DeviceCondition = Clean(repairRequest.DeviceCondition) ?? string.Empty,
                    CustomerRequest = string.Empty,
                    TechnicianAssessment = null,
                    EstimatedCost = repairTotal,
                    ExpectedReturnAtUtc = invoice.ExpectedReturnAtUtc,
                    Status = invoice.RepairStatus ?? RepairStatus.InProgress,
                    Notes = Clean(repairRequest.Notes)
                };
                foreach (var service in repairServices)
                    repair.ServiceTypes.Add(new RepairOrderServiceType { CompanyId = companyId, ServiceTypeId = service.ServiceTypeId, Notes = Clean(service.Notes), Amount = service.Amount });
                invoice.Repairs.Add(repair);
                invoice.Lines.Add(new SalesInvoiceLine
                {
                    CompanyId = companyId,
                    ProductName = $"Sửa chữa {repairRequest.DeviceModel.Trim()}",
                    Sku = "DICH-VU-SUA-CHUA",
                    Quantity = 1,
                    UnitName = "Dịch vụ",
                    WarrantyPeriod = "Theo phiếu sửa chữa",
                    UnitPrice = repairTotal,
                    LineTotal = repairTotal,
                    Notes = Clean(repairRequest.DeviceCondition)
                });
            }
        }
        if (request.Type == SalesInvoiceType.Sale && request.Lines.Count == 0)
            return BadRequest(new { message = "Hóa đơn cần ít nhất một sản phẩm." });

        if (request.Lines.Count > 0)
        {
            var requestedProductIds = request.Lines.Where(x => x.ProductId.HasValue).Select(x => x.ProductId!.Value).Distinct().ToList();
            var products = await db.Products.Where(x => requestedProductIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
            foreach (var lineRequest in request.Lines)
            {
                if (lineRequest.Quantity <= 0 || lineRequest.UnitPrice < 0)
                    return BadRequest(new { message = "Số lượng hoặc đơn giá không hợp lệ." });

                Product? product = null;
                if (lineRequest.ProductId.HasValue && !products.TryGetValue(lineRequest.ProductId.Value, out product))
                    return BadRequest(new { message = "Có sản phẩm không còn tồn tại trong hệ thống." });
                if (product is null && string.IsNullOrWhiteSpace(lineRequest.ProductName))
                    return BadRequest(new { message = "Tên dòng dịch vụ/sản phẩm không được để trống." });
                if (product?.TrackInventory == true && product.StockQuantity < lineRequest.Quantity)
                    return BadRequest(new { message = $"Sản phẩm {product.Name} chỉ còn {product.StockQuantity:n0}." });

                var lineDiscount = Math.Clamp(lineRequest.DiscountAmount, 0, lineRequest.Quantity * lineRequest.UnitPrice);
                var line = new SalesInvoiceLine
                {
                    CompanyId = companyId,
                    ProductId = product?.Id,
                    Sku = product?.Sku ?? "DICH-VU",
                    ProductName = product?.Name ?? lineRequest.ProductName!.Trim(),
                    Quantity = lineRequest.Quantity,
                    UnitName = product?.Unit ?? lineRequest.UnitName?.Trim() ?? "Cái",
                    WarrantyPeriod = Clean(lineRequest.WarrantyPeriod) ?? "3 tháng",
                    UnitPrice = lineRequest.UnitPrice,
                    DiscountAmount = lineDiscount,
                    LineTotal = lineRequest.Quantity * lineRequest.UnitPrice - lineDiscount,
                    Notes = Clean(lineRequest.Notes)
                };
                invoice.Lines.Add(line);

                var lineServiceTypeIds = lineRequest.ServiceTypeIds?.Distinct().ToList() ?? [];
                if (lineServiceTypeIds.Count == 0 && product is not null)
                {
                    var automaticCode = DetectProductServiceTypeCode(product.Name, product.CategoryName);
                    var automaticType = activeServiceTypes.Values.FirstOrDefault(x => x.Code == automaticCode);
                    if (automaticType is not null) lineServiceTypeIds.Add(automaticType.Id);
                }
                if (!TryAddServiceTypeIds(lineServiceTypeIds, activeServiceTypes, selectedInvoiceServiceTypeIds, out serviceTypeError))
                    return BadRequest(new { message = serviceTypeError });

                if (product?.TrackInventory == true)
                {
                    product.StockQuantity -= lineRequest.Quantity;
                    if (product.StockQuantity <= 0) product.Availability = ProductAvailability.OutOfStock;
                    db.StockMovements.Add(new StockMovement
                    {
                        CompanyId = companyId,
                        ProductId = product.Id,
                        Type = StockMovementType.Sale,
                        Quantity = -lineRequest.Quantity,
                        UnitCost = product.CostPrice,
                        ReferenceType = nameof(SalesInvoice),
                        ReferenceId = invoice.Id,
                        Notes = invoice.Number,
                        OccurredAtUtc = invoice.InvoiceDateUtc
                    });
                }
            }
        }

        foreach (var serviceTypeId in selectedInvoiceServiceTypeIds)
            invoice.ServiceTypes.Add(new SalesInvoiceServiceType { CompanyId = companyId, ServiceTypeId = serviceTypeId });

        invoice.Subtotal = invoice.Lines.Sum(x => x.LineTotal);
        var discountError = await ApplyDiscountsAsync(invoice, request, now, cancellationToken);
        if (discountError is not null) return BadRequest(new { message = discountError });

        if (invoice.IsInstallment && request.Payments.Any(x => x.Amount > 0 && x.Method != PaymentMethod.Cash))
            return BadRequest(new { message = "Hóa đơn trả góp chỉ nhập khoản tiền mặt khách trả trước. Phần còn lại được tự động phân bổ vào tài khoản ngân hàng đã chọn." });

        var paymentAccountIds = request.Payments.Where(x => x.PaymentAccountId.HasValue).Select(x => x.PaymentAccountId!.Value).Distinct().ToList();
        var paymentAccounts = await db.PaymentAccounts.Where(x => paymentAccountIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, cancellationToken);
        foreach (var paymentRequest in request.Payments)
        {
            if (paymentRequest.Amount <= 0) continue;
            PaymentAccount? account = null;
            if (paymentRequest.Method == PaymentMethod.BankTransfer)
            {
                if (!paymentRequest.PaymentAccountId.HasValue || !paymentAccounts.TryGetValue(paymentRequest.PaymentAccountId.Value, out account))
                    return BadRequest(new { message = "Mỗi khoản chuyển khoản phải chọn tài khoản ngân hàng nhận tiền." });
            }
            invoice.Payments.Add(new SalesPayment
            {
                CompanyId = companyId,
                Method = paymentRequest.Method,
                PaymentAccountId = account?.Id,
                PaymentAccount = account,
                Amount = paymentRequest.Amount,
                PaidAtUtc = (paymentRequest.PaidAtUtc ?? invoice.InvoiceDateUtc).ToUniversalTime(),
                IsScheduled = false,
                Reference = Clean(paymentRequest.Reference),
                Notes = Clean(paymentRequest.Notes)
            });
        }

        invoice.PaidAmount = invoice.Payments.Sum(x => x.Amount);
        if (invoice.PaidAmount > invoice.TotalAmount)
            return BadRequest(new { message = "Số tiền đã nhận không được lớn hơn tổng hóa đơn." });
        if (!invoice.IsInstallment && invoice.PaidAmount != invoice.TotalAmount)
            return BadRequest(new { message = "Thanh toán trực tiếp phải được phân bổ đủ 100% giá trị hóa đơn." });
        if (invoice.IsInstallment && invoice.PaidAmount <= 0)
            return BadRequest(new { message = "Hóa đơn trả góp cần nhập số tiền mặt khách trả trước." });
        if (invoice.IsInstallment && invoice.PaidAmount >= invoice.TotalAmount)
            return BadRequest(new { message = "Tiền trả trước phải nhỏ hơn tổng hóa đơn trả góp." });
        if (invoice.IsInstallment && invoice.PaidAmount < invoice.TotalAmount)
        {
            if (!invoice.InstallmentPaymentAccountId.HasValue)
                return BadRequest(new { message = "Hóa đơn trả góp cần chọn tài khoản ngân hàng sẽ nhận tiền góp." });
            invoice.InstallmentPaymentAccount = await db.PaymentAccounts.SingleOrDefaultAsync(
                x => x.Id == invoice.InstallmentPaymentAccountId.Value && x.IsActive,
                cancellationToken);
            if (invoice.InstallmentPaymentAccount is null)
                return BadRequest(new { message = "Tài khoản ngân hàng nhận tiền góp không còn hoạt động." });
        }
        if (invoice.IsInstallment && invoice.PaidAmount < invoice.TotalAmount && invoice.PaymentDueDateUtc is null)
            return BadRequest(new { message = "Hóa đơn trả góp cần ngày dự kiến nhận phần tiền còn lại." });
        if (invoice.IsInstallment && invoice.PaidAmount < invoice.TotalAmount)
        {
            var scheduledAmount = invoice.TotalAmount - invoice.PaidAmount;
            invoice.Payments.Add(new SalesPayment
            {
                CompanyId = companyId,
                Method = PaymentMethod.BankTransfer,
                PaymentAccountId = invoice.InstallmentPaymentAccountId,
                PaymentAccount = invoice.InstallmentPaymentAccount,
                Amount = scheduledAmount,
                PaidAtUtc = invoice.PaymentDueDateUtc!.Value,
                IsScheduled = true,
                Reference = Clean(invoice.InstallmentReference),
                Notes = string.IsNullOrWhiteSpace(invoice.InstallmentProvider)
                    ? "Khoản tiền góp dự kiến"
                    : $"Đơn vị trả góp: {invoice.InstallmentProvider}"
            });
            invoice.PaidAmount += scheduledAmount;
        }
        invoice.Status = ResolveStatus(invoice.TotalAmount, invoice.PaidAmount);

        if (IsProcessed(invoice))
        {
            customer.TotalSpent += invoice.TotalAmount;
            customer.LastPurchaseAtUtc = invoice.InvoiceDateUtc;
        }
        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = invoice.Id }, await ToDetailsAsync(invoice, cancellationToken));
    }

    [Authorize(Policy = Permissions.SalesManage)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SalesInvoiceDetails>> Update(
        Guid id,
        CreateSalesInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        await EnsureDefaultServiceTypesAsync(companyId, cancellationToken);
        await EnsureDefaultInstallmentProvidersAsync(companyId, cancellationToken);
        var activeServiceTypes = await db.ServiceTypes.Where(x => x.IsActive).ToDictionaryAsync(x => x.Id, cancellationToken);
        var defaultRepairTurnaroundDays = await db.Companies.AsNoTracking()
            .Where(x => x.Id == companyId)
            .Select(x => x.DefaultRepairTurnaroundDays)
            .SingleAsync(cancellationToken);
        var selectedInvoiceServiceTypeIds = new HashSet<Guid>();
        var invoice = await db.SalesInvoices
            .Include(x => x.Lines)
            .Include(x => x.Discounts)
            .Include(x => x.Payments)
            .Include(x => x.Repairs).ThenInclude(x => x.ServiceTypes)
            .Include(x => x.ServiceTypes)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (invoice is null) return NotFound();
        if (request.Type != invoice.Type)
            return BadRequest(new { message = "Không thể đổi loại hóa đơn sau khi đã tạo." });
        if (string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.CustomerPhone))
            return BadRequest(new { message = "Tên và số điện thoại khách hàng là bắt buộc." });
        var normalizedPhone = NormalizePhone(request.CustomerPhone);
        if (normalizedPhone.Length < 8) return BadRequest(new { message = "Số điện thoại khách hàng không hợp lệ." });

        var installmentProviderName = Clean(request.InstallmentProvider);
        if (request.IsInstallment)
        {
            if (installmentProviderName is null)
                return BadRequest(new { message = "Hóa đơn trả góp cần chọn đơn vị trả góp." });
            installmentProviderName = await db.InstallmentProviders
                .Where(x => x.IsActive && x.Name == installmentProviderName)
                .Select(x => x.Name)
                .SingleOrDefaultAsync(cancellationToken);
            if (installmentProviderName is null)
                return BadRequest(new { message = "Đơn vị trả góp không tồn tại hoặc đã bị tắt." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var oldTotal = invoice.TotalAmount;
        var oldWasProcessed = IsProcessed(invoice);
        var oldCustomer = invoice.CustomerId.HasValue
            ? await db.Customers.SingleOrDefaultAsync(x => x.Id == invoice.CustomerId.Value, cancellationToken)
            : null;
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Phone == normalizedPhone, cancellationToken);
        if (customer is null)
        {
            customer = new Customer
            {
                CompanyId = companyId,
                Code = await NextCustomerCode(cancellationToken),
                FullName = request.CustomerName.Trim(),
                Phone = normalizedPhone,
                Email = Clean(request.CustomerEmail),
                Address = Clean(request.CustomerAddress),
                Notes = Clean(request.CustomerNotes)
            };
            db.Customers.Add(customer);
        }
        else
        {
            customer.FullName = request.CustomerName.Trim();
            customer.Email = Clean(request.CustomerEmail) ?? customer.Email;
            customer.Address = Clean(request.CustomerAddress) ?? customer.Address;
            customer.Notes = Clean(request.CustomerNotes) ?? customer.Notes;
        }

        var oldProductLines = invoice.Lines.Where(x => x.ProductId.HasValue).ToList();
        var oldProductIds = oldProductLines.Select(x => x.ProductId!.Value).Distinct().ToList();
        var oldProducts = await db.Products.Where(x => oldProductIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        foreach (var oldLine in oldProductLines)
        {
            if (oldProducts.TryGetValue(oldLine.ProductId!.Value, out var product) && product.TrackInventory)
            {
                product.StockQuantity += oldLine.Quantity;
                if (product.StockQuantity > 0) product.Availability = ProductAvailability.InStock;
            }
        }

        var oldMovements = await db.StockMovements.Where(x =>
            x.ReferenceType == nameof(SalesInvoice) && x.ReferenceId == invoice.Id && x.Type == StockMovementType.Sale)
            .ToListAsync(cancellationToken);
        var customerId = customer.Id;
        var oldCustomerId = oldCustomer?.Id;
        db.StockMovements.RemoveRange(oldMovements);
        db.SalesInvoiceLines.RemoveRange(invoice.Lines);
        await RestoreVoucherUsageAsync(invoice, cancellationToken);
        db.SalesDiscountLines.RemoveRange(invoice.Discounts);
        db.SalesPayments.RemoveRange(invoice.Payments);
        db.SalesInvoiceServiceTypes.RemoveRange(invoice.ServiceTypes);
        db.RepairOrderServiceTypes.RemoveRange(invoice.Repairs.SelectMany(x => x.ServiceTypes));
        invoice.Lines.Clear();
        invoice.Discounts.Clear();
        invoice.Payments.Clear();
        invoice.ServiceTypes.Clear();
        if (invoice.Repairs.Count > 0) db.RepairOrders.RemoveRange(invoice.Repairs);
        invoice.Repairs.Clear();
        // Persist soft deletes first so replacing a service link does not violate the filtered unique indexes.
        await db.SaveChangesAsync(cancellationToken);
        // Start the rebuild with a clean relationship snapshot. Otherwise EF can classify new repair-service
        // links as orphans because the previous required children were removed in the same tracked graph.
        db.ChangeTracker.Clear();
        invoice = await db.SalesInvoices.SingleAsync(x => x.Id == id, cancellationToken);
        customer = await db.Customers.SingleAsync(x => x.Id == customerId, cancellationToken);
        oldCustomer = oldCustomerId switch
        {
            null => null,
            var value when value == customerId => customer,
            var value => await db.Customers.SingleOrDefaultAsync(x => x.Id == value, cancellationToken)
        };
        invoice.CustomerId = customer.Id;
        invoice.CustomerName = customer.FullName;
        invoice.CustomerPhone = customer.Phone;
        invoice.CustomerEmail = customer.Email;
        invoice.CustomerAddress = customer.Address;
        invoice.InvoiceDateUtc = (request.InvoiceDateUtc ?? invoice.InvoiceDateUtc).ToUniversalTime();
        invoice.ExpectedReturnAtUtc = request.Type == SalesInvoiceType.Repair
            ? request.ExpectedReturnAtUtc?.ToUniversalTime() ?? invoice.ExpectedReturnAtUtc ?? invoice.InvoiceDateUtc.AddDays(defaultRepairTurnaroundDays)
            : null;
        invoice.RepairStatus = NormalizeProcessingStatus(request.RepairStatus ?? invoice.RepairStatus);
        invoice.IsInstallment = request.IsInstallment;
        invoice.InstallmentProvider = installmentProviderName;
        invoice.InstallmentReference = Clean(request.InstallmentReference);
        invoice.InstallmentPaymentAccountId = request.IsInstallment ? request.InstallmentPaymentAccountId : null;
        invoice.InstallmentPaymentAccount = null;
        invoice.PaymentDueDateUtc = request.PaymentDueDateUtc?.ToUniversalTime();
        invoice.Notes = Clean(request.Notes);
        invoice.PromotionId = null;
        invoice.PromotionName = null;
        invoice.PromotionDiscountAmount = 0;
        invoice.VoucherId = null;
        invoice.VoucherCode = null;
        invoice.VoucherDiscountAmount = 0;

        if (!TryAddServiceTypeIds(request.ServiceTypeIds, activeServiceTypes, selectedInvoiceServiceTypeIds, out var serviceTypeError))
            return BadRequest(new { message = serviceTypeError });

        var rebuiltSubtotal = 0m;
        if (request.Type == SalesInvoiceType.Repair)
        {
            var repairRequests = GetRepairRequests(request);
            if (repairRequests.Count == 0)
                return BadRequest(new { message = "Phiếu tiếp nhận cần ít nhất một máy sửa chữa." });
            foreach (var repairRequest in repairRequests)
            {
                if (string.IsNullOrWhiteSpace(repairRequest.DeviceModel))
                    return BadRequest(new { message = "Mỗi máy cần có model." });
                var repairServices = GetRepairServices(repairRequest);
                if (repairServices.Any(x => x.Amount < 0))
                    return BadRequest(new { message = $"Giá dịch vụ cho máy {repairRequest.DeviceModel} không hợp lệ." });
                var repairServiceTypeIds = repairServices.Select(x => x.ServiceTypeId).Distinct().ToList();
                if (repairServiceTypeIds.Count == 0)
                    return BadRequest(new { message = $"Hãy chọn ít nhất một loại dịch vụ cho máy {repairRequest.DeviceModel}." });
                if (!TryAddServiceTypeIds(repairServiceTypeIds, activeServiceTypes, selectedInvoiceServiceTypeIds, out serviceTypeError))
                    return BadRequest(new { message = serviceTypeError });
                var repairTotal = repairServices.Sum(x => x.Amount);
                rebuiltSubtotal += repairTotal;
                var repair = new RepairOrder
                {
                    CompanyId = companyId,
                    SalesInvoiceId = invoice.Id,
                    DeviceModel = repairRequest.DeviceModel.Trim(),
                    SerialNumber = Clean(repairRequest.SerialNumber),
                    Accessories = Clean(repairRequest.Accessories),
                    DeviceCondition = Clean(repairRequest.DeviceCondition) ?? string.Empty,
                    CustomerRequest = string.Empty,
                    TechnicianAssessment = null,
                    EstimatedCost = repairTotal,
                    ExpectedReturnAtUtc = invoice.ExpectedReturnAtUtc,
                    Status = invoice.RepairStatus ?? RepairStatus.InProgress,
                    Notes = Clean(repairRequest.Notes)
                };
                foreach (var service in repairServices)
                    db.RepairOrderServiceTypes.Add(new RepairOrderServiceType
                    {
                        CompanyId = companyId,
                        RepairOrderId = repair.Id,
                        ServiceTypeId = service.ServiceTypeId,
                        Notes = Clean(service.Notes),
                        Amount = service.Amount
                    });
                db.RepairOrders.Add(repair);
                db.SalesInvoiceLines.Add(new SalesInvoiceLine
                {
                    CompanyId = companyId,
                    SalesInvoiceId = invoice.Id,
                    ProductName = $"Sửa chữa {repairRequest.DeviceModel.Trim()}",
                    Sku = "DICH-VU-SUA-CHUA",
                    Quantity = 1,
                    UnitName = "Dịch vụ",
                    WarrantyPeriod = "Theo phiếu sửa chữa",
                    UnitPrice = repairTotal,
                    LineTotal = repairTotal,
                    Notes = Clean(repairRequest.DeviceCondition)
                });
            }
        }
        if (request.Type == SalesInvoiceType.Sale && request.Lines.Count == 0)
            return BadRequest(new { message = "Hóa đơn cần ít nhất một sản phẩm hoặc dịch vụ." });

        if (request.Lines.Count > 0)
        {
            var requestedProductIds = request.Lines.Where(x => x.ProductId.HasValue).Select(x => x.ProductId!.Value).Distinct().ToList();
            var products = await db.Products.Where(x => requestedProductIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
            foreach (var lineRequest in request.Lines)
            {
                if (lineRequest.Quantity <= 0 || lineRequest.UnitPrice < 0)
                    return BadRequest(new { message = "Số lượng hoặc đơn giá không hợp lệ." });
                Product? product = null;
                if (lineRequest.ProductId.HasValue && !products.TryGetValue(lineRequest.ProductId.Value, out product))
                    return BadRequest(new { message = "Có sản phẩm không còn tồn tại trong hệ thống." });
                if (product is null && string.IsNullOrWhiteSpace(lineRequest.ProductName))
                    return BadRequest(new { message = "Tên dòng dịch vụ/sản phẩm không được để trống." });
                if (product?.TrackInventory == true && product.StockQuantity < lineRequest.Quantity)
                    return BadRequest(new { message = $"Sản phẩm {product.Name} chỉ còn {product.StockQuantity:n0}." });
                var lineDiscount = Math.Clamp(lineRequest.DiscountAmount, 0, lineRequest.Quantity * lineRequest.UnitPrice);
                rebuiltSubtotal += lineRequest.Quantity * lineRequest.UnitPrice - lineDiscount;
                db.SalesInvoiceLines.Add(new SalesInvoiceLine
                {
                    CompanyId = companyId,
                    SalesInvoiceId = invoice.Id,
                    ProductId = product?.Id,
                    Sku = product?.Sku ?? "DICH-VU",
                    ProductName = product?.Name ?? lineRequest.ProductName!.Trim(),
                    Quantity = lineRequest.Quantity,
                    UnitName = product?.Unit ?? lineRequest.UnitName?.Trim() ?? "Cái",
                    WarrantyPeriod = Clean(lineRequest.WarrantyPeriod) ?? "3 tháng",
                    UnitPrice = lineRequest.UnitPrice,
                    DiscountAmount = lineDiscount,
                    LineTotal = lineRequest.Quantity * lineRequest.UnitPrice - lineDiscount,
                    Notes = Clean(lineRequest.Notes)
                });
                var lineServiceTypeIds = lineRequest.ServiceTypeIds?.Distinct().ToList() ?? [];
                if (lineServiceTypeIds.Count == 0 && product is not null)
                {
                    var automaticCode = DetectProductServiceTypeCode(product.Name, product.CategoryName);
                    var automaticType = activeServiceTypes.Values.FirstOrDefault(x => x.Code == automaticCode);
                    if (automaticType is not null) lineServiceTypeIds.Add(automaticType.Id);
                }
                if (!TryAddServiceTypeIds(lineServiceTypeIds, activeServiceTypes, selectedInvoiceServiceTypeIds, out serviceTypeError))
                    return BadRequest(new { message = serviceTypeError });
                if (product?.TrackInventory == true)
                {
                    product.StockQuantity -= lineRequest.Quantity;
                    if (product.StockQuantity <= 0) product.Availability = ProductAvailability.OutOfStock;
                    db.StockMovements.Add(new StockMovement
                    {
                        CompanyId = companyId,
                        ProductId = product.Id,
                        Type = StockMovementType.Sale,
                        Quantity = -lineRequest.Quantity,
                        UnitCost = product.CostPrice,
                        ReferenceType = nameof(SalesInvoice),
                        ReferenceId = invoice.Id,
                        Notes = invoice.Number,
                        OccurredAtUtc = invoice.InvoiceDateUtc
                    });
                }
            }
        }

        foreach (var serviceTypeId in selectedInvoiceServiceTypeIds)
            db.SalesInvoiceServiceTypes.Add(new SalesInvoiceServiceType
            {
                CompanyId = companyId,
                SalesInvoiceId = invoice.Id,
                ServiceTypeId = serviceTypeId
            });

        invoice.Subtotal = rebuiltSubtotal;
        var now = DateTimeOffset.UtcNow;
        var discountError = await ApplyDiscountsAsync(invoice, request, now, cancellationToken);
        if (discountError is not null) return BadRequest(new { message = discountError });

        if (invoice.IsInstallment && request.Payments.Any(x => x.Amount > 0 && x.Method != PaymentMethod.Cash))
            return BadRequest(new { message = "Hóa đơn trả góp chỉ nhập khoản tiền mặt khách trả trước. Phần còn lại được tự động phân bổ vào tài khoản ngân hàng đã chọn." });

        var paymentAccountIds = request.Payments.Where(x => x.PaymentAccountId.HasValue).Select(x => x.PaymentAccountId!.Value).Distinct().ToList();
        var paymentAccounts = await db.PaymentAccounts.Where(x => paymentAccountIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, cancellationToken);
        var immediatePaymentTotal = 0m;
        foreach (var paymentRequest in request.Payments.Where(x => x.Amount > 0))
        {
            PaymentAccount? account = null;
            if (paymentRequest.Method == PaymentMethod.BankTransfer)
            {
                if (!paymentRequest.PaymentAccountId.HasValue || !paymentAccounts.TryGetValue(paymentRequest.PaymentAccountId.Value, out account))
                    return BadRequest(new { message = "Mỗi khoản chuyển khoản phải chọn tài khoản ngân hàng nhận tiền." });
            }
            db.SalesPayments.Add(new SalesPayment
            {
                CompanyId = companyId,
                SalesInvoiceId = invoice.Id,
                Method = paymentRequest.Method,
                PaymentAccountId = account?.Id,
                PaymentAccount = account,
                Amount = paymentRequest.Amount,
                PaidAtUtc = (paymentRequest.PaidAtUtc ?? invoice.InvoiceDateUtc).ToUniversalTime(),
                IsScheduled = false,
                Reference = Clean(paymentRequest.Reference),
                Notes = Clean(paymentRequest.Notes)
            });
            immediatePaymentTotal += paymentRequest.Amount;
        }
        invoice.PaidAmount = immediatePaymentTotal;
        if (invoice.PaidAmount > invoice.TotalAmount)
            return BadRequest(new { message = "Số tiền đã nhận không được lớn hơn tổng hóa đơn." });
        if (!invoice.IsInstallment && invoice.PaidAmount != invoice.TotalAmount)
            return BadRequest(new { message = "Thanh toán trực tiếp phải được phân bổ đủ 100% giá trị hóa đơn." });
        if (invoice.IsInstallment && invoice.PaidAmount <= 0)
            return BadRequest(new { message = "Hóa đơn trả góp cần nhập số tiền mặt khách trả trước." });
        if (invoice.IsInstallment && invoice.PaidAmount >= invoice.TotalAmount)
            return BadRequest(new { message = "Tiền trả trước phải nhỏ hơn tổng hóa đơn trả góp." });
        if (invoice.IsInstallment && invoice.PaidAmount < invoice.TotalAmount)
        {
            if (!invoice.InstallmentPaymentAccountId.HasValue)
                return BadRequest(new { message = "Hóa đơn trả góp cần chọn tài khoản ngân hàng sẽ nhận tiền góp." });
            invoice.InstallmentPaymentAccount = await db.PaymentAccounts.SingleOrDefaultAsync(
                x => x.Id == invoice.InstallmentPaymentAccountId.Value && x.IsActive,
                cancellationToken);
            if (invoice.InstallmentPaymentAccount is null)
                return BadRequest(new { message = "Tài khoản ngân hàng nhận tiền góp không còn hoạt động." });
        }
        if (invoice.IsInstallment && invoice.PaidAmount < invoice.TotalAmount && invoice.PaymentDueDateUtc is null)
            return BadRequest(new { message = "Hóa đơn trả góp cần ngày dự kiến nhận phần tiền còn lại." });
        if (invoice.IsInstallment && invoice.PaidAmount < invoice.TotalAmount)
        {
            var scheduledAmount = invoice.TotalAmount - invoice.PaidAmount;
            db.SalesPayments.Add(new SalesPayment
            {
                CompanyId = companyId,
                SalesInvoiceId = invoice.Id,
                Method = PaymentMethod.BankTransfer,
                PaymentAccountId = invoice.InstallmentPaymentAccountId,
                PaymentAccount = invoice.InstallmentPaymentAccount,
                Amount = scheduledAmount,
                PaidAtUtc = invoice.PaymentDueDateUtc!.Value,
                IsScheduled = true,
                Reference = Clean(invoice.InstallmentReference),
                Notes = string.IsNullOrWhiteSpace(invoice.InstallmentProvider)
                    ? "Khoản tiền góp dự kiến"
                    : $"Đơn vị trả góp: {invoice.InstallmentProvider}"
            });
            invoice.PaidAmount += scheduledAmount;
        }
        invoice.Status = ResolveStatus(invoice.TotalAmount, invoice.PaidAmount);

        var newContribution = IsProcessed(invoice) ? invoice.TotalAmount : 0;
        var oldContribution = oldWasProcessed ? oldTotal : 0;
        if (oldCustomer?.Id == customer.Id)
            customer.TotalSpent = Math.Max(0, customer.TotalSpent - oldContribution) + newContribution;
        else
        {
            if (oldCustomer is not null) oldCustomer.TotalSpent = Math.Max(0, oldCustomer.TotalSpent - oldContribution);
            customer.TotalSpent += newContribution;
        }
        if (IsProcessed(invoice)) customer.LastPurchaseAtUtc = invoice.InvoiceDateUtc;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        db.ChangeTracker.Clear();
        var refreshed = await db.SalesInvoices.AsNoTracking()
            .Include(x => x.Lines)
            .Include(x => x.Discounts)
            .Include(x => x.Payments).ThenInclude(x => x.PaymentAccount)
            .Include(x => x.InstallmentPaymentAccount)
            .Include(x => x.Repairs).ThenInclude(x => x.ServiceTypes).ThenInclude(x => x.ServiceType)
            .Include(x => x.ServiceTypes).ThenInclude(x => x.ServiceType)
            .SingleAsync(x => x.Id == id, cancellationToken);
        return Ok(await ToDetailsAsync(refreshed, cancellationToken));
    }

    [Authorize(Policy = Permissions.SalesManage)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await db.SalesInvoices
            .Include(x => x.Lines)
            .Include(x => x.Discounts)
            .Include(x => x.Payments)
            .Include(x => x.Repairs).ThenInclude(x => x.ServiceTypes)
            .Include(x => x.ServiceTypes)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (invoice is null) return NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var productLines = invoice.Lines.Where(x => x.ProductId.HasValue).ToList();
        var productIds = productLines.Select(x => x.ProductId!.Value).Distinct().ToList();
        var products = await db.Products.Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        foreach (var line in productLines)
        {
            if (products.TryGetValue(line.ProductId!.Value, out var product) && product.TrackInventory)
            {
                product.StockQuantity += line.Quantity;
                if (product.StockQuantity > 0) product.Availability = ProductAvailability.InStock;
            }
        }
        var movements = await db.StockMovements.Where(x => x.ReferenceType == nameof(SalesInvoice) && x.ReferenceId == invoice.Id)
            .ToListAsync(cancellationToken);
        db.StockMovements.RemoveRange(movements);
        await RestoreVoucherUsageAsync(invoice, cancellationToken);
        if (invoice.CustomerId.HasValue)
        {
            var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == invoice.CustomerId.Value, cancellationToken);
            if (customer is not null && IsProcessed(invoice)) customer.TotalSpent = Math.Max(0, customer.TotalSpent - invoice.TotalAmount);
        }
        db.SalesInvoiceLines.RemoveRange(invoice.Lines);
        db.SalesDiscountLines.RemoveRange(invoice.Discounts);
        db.SalesPayments.RemoveRange(invoice.Payments);
        db.SalesInvoiceServiceTypes.RemoveRange(invoice.ServiceTypes);
        if (invoice.Repairs.Count > 0) db.RepairOrders.RemoveRange(invoice.Repairs);
        db.SalesInvoices.Remove(invoice);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = Permissions.SalesManage)]
    [HttpPost("{id:guid}/payments")]
    public async Task<ActionResult<SalesInvoiceDetails>> AddPayment(
        Guid id,
        AddSalesPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var invoice = await db.SalesInvoices
            .Include(x => x.Lines)
            .Include(x => x.Discounts)
            .Include(x => x.Payments).ThenInclude(x => x.PaymentAccount)
            .Include(x => x.Repairs).ThenInclude(x => x.ServiceTypes).ThenInclude(x => x.ServiceType)
            .Include(x => x.ServiceTypes).ThenInclude(x => x.ServiceType)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (invoice is null) return NotFound();
        if (invoice.IsInstallment)
            return BadRequest(new { message = "Nguồn tiền trả góp đã được hệ thống tự động phân bổ. Hãy sửa hóa đơn nếu cần đổi tiền trả trước, tài khoản hoặc ngày nhận tiền góp." });
        if (request.Amount <= 0 || request.Amount > invoice.TotalAmount - invoice.PaidAmount)
            return BadRequest(new { message = "Số tiền thu không hợp lệ hoặc vượt quá công nợ." });

        PaymentAccount? account = null;
        if (request.Method == PaymentMethod.BankTransfer)
        {
            if (!request.PaymentAccountId.HasValue) return BadRequest(new { message = "Hãy chọn tài khoản ngân hàng nhận tiền." });
            account = await db.PaymentAccounts.SingleOrDefaultAsync(x => x.Id == request.PaymentAccountId && x.IsActive, cancellationToken);
            if (account is null) return BadRequest(new { message = "Tài khoản ngân hàng không hợp lệ." });
        }

        var payment = new SalesPayment
        {
            CompanyId = invoice.CompanyId,
            SalesInvoiceId = invoice.Id,
            Method = request.Method,
            PaymentAccountId = account?.Id,
            PaymentAccount = account,
            Amount = request.Amount,
            PaidAtUtc = (request.PaidAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime(),
            IsScheduled = false,
            Reference = Clean(request.Reference),
            Notes = Clean(request.Notes)
        };
        invoice.Payments.Add(payment);
        invoice.PaidAmount += payment.Amount;
        invoice.Status = ResolveStatus(invoice.TotalAmount, invoice.PaidAmount);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await ToDetailsAsync(invoice, cancellationToken));
    }

    [Authorize(Policy = Permissions.SalesRead)]
    [HttpGet("payment-accounts")]
    public async Task<ActionResult<IReadOnlyList<PaymentAccountItem>>> GetPaymentAccounts(CancellationToken cancellationToken) =>
        Ok(await db.PaymentAccounts.AsNoTracking().OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .Select(x => new PaymentAccountItem(x.Id, x.Name, x.BankName, x.AccountNumber, x.AccountHolder, x.IsActive, x.DisplayOrder))
            .ToListAsync(cancellationToken));

    [Authorize(Policy = Permissions.SalesManage)]
    [HttpPost("payment-accounts")]
    public async Task<ActionResult<PaymentAccountItem>> CreatePaymentAccount(SavePaymentAccountRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.BankName) || string.IsNullOrWhiteSpace(request.AccountNumber))
            return BadRequest(new { message = "Tên tài khoản, ngân hàng và số tài khoản là bắt buộc." });
        var account = new PaymentAccount
        {
            CompanyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException(),
            Name = request.Name.Trim(),
            BankName = request.BankName.Trim(),
            AccountNumber = request.AccountNumber.Trim(),
            AccountHolder = request.AccountHolder?.Trim() ?? string.Empty,
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder
        };
        db.PaymentAccounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new PaymentAccountItem(account.Id, account.Name, account.BankName, account.AccountNumber, account.AccountHolder, account.IsActive, account.DisplayOrder));
    }

    [Authorize(Policy = Permissions.SalesManage)]
    [HttpPut("payment-accounts/{id:guid}")]
    public async Task<ActionResult<PaymentAccountItem>> UpdatePaymentAccount(Guid id, SavePaymentAccountRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.BankName) || string.IsNullOrWhiteSpace(request.AccountNumber))
            return BadRequest(new { message = "Tên tài khoản, ngân hàng và số tài khoản là bắt buộc." });
        var account = await db.PaymentAccounts.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (account is null) return NotFound();
        account.Name = request.Name.Trim();
        account.BankName = request.BankName.Trim();
        account.AccountNumber = request.AccountNumber.Trim();
        account.AccountHolder = request.AccountHolder?.Trim() ?? string.Empty;
        account.IsActive = request.IsActive;
        account.DisplayOrder = request.DisplayOrder;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new PaymentAccountItem(account.Id, account.Name, account.BankName, account.AccountNumber, account.AccountHolder, account.IsActive, account.DisplayOrder));
    }

    [Authorize(Policy = Permissions.SalesManage)]
    [HttpDelete("payment-accounts/{id:guid}")]
    public async Task<IActionResult> DeletePaymentAccount(Guid id, CancellationToken cancellationToken)
    {
        var account = await db.PaymentAccounts.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (account is null) return NotFound();
        db.PaymentAccounts.Remove(account);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = Permissions.SalesRead)]
    [HttpGet("installment-providers")]
    public async Task<ActionResult<IReadOnlyList<InstallmentProviderItem>>> GetInstallmentProviders(CancellationToken cancellationToken)
    {
        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        await EnsureDefaultInstallmentProvidersAsync(companyId, cancellationToken);
        return Ok(await db.InstallmentProviders.AsNoTracking()
            .OrderByDescending(x => x.IsActive).ThenBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .Select(x => new InstallmentProviderItem(x.Id, x.Name, x.Code, x.IsActive, x.DisplayOrder))
            .ToListAsync(cancellationToken));
    }

    [Authorize(Policy = Permissions.SalesManage)]
    [HttpPost("installment-providers")]
    public async Task<ActionResult<InstallmentProviderItem>> CreateInstallmentProvider(SaveInstallmentProviderRequest request, CancellationToken cancellationToken)
    {
        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        var name = Clean(request.Name);
        var code = NormalizeServiceTypeCode(request.Code ?? request.Name);
        if (name is null || code.Length == 0) return BadRequest(new { message = "Tên đơn vị trả góp là bắt buộc." });
        if (await db.InstallmentProviders.AnyAsync(x => x.Code == code, cancellationToken))
            return Conflict(new { message = "Mã đơn vị trả góp đã tồn tại." });
        var provider = new InstallmentProvider
        {
            CompanyId = companyId,
            Name = name,
            Code = code,
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder
        };
        db.InstallmentProviders.Add(provider);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new InstallmentProviderItem(provider.Id, provider.Name, provider.Code, provider.IsActive, provider.DisplayOrder));
    }

    [Authorize(Policy = Permissions.SalesManage)]
    [HttpPut("installment-providers/{id:guid}")]
    public async Task<ActionResult<InstallmentProviderItem>> UpdateInstallmentProvider(Guid id, SaveInstallmentProviderRequest request, CancellationToken cancellationToken)
    {
        var name = Clean(request.Name);
        var code = NormalizeServiceTypeCode(request.Code ?? request.Name);
        if (name is null || code.Length == 0) return BadRequest(new { message = "Tên đơn vị trả góp là bắt buộc." });
        if (await db.InstallmentProviders.AnyAsync(x => x.Id != id && x.Code == code, cancellationToken))
            return Conflict(new { message = "Mã đơn vị trả góp đã tồn tại." });
        var provider = await db.InstallmentProviders.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (provider is null) return NotFound();
        provider.Name = name;
        provider.Code = code;
        provider.IsActive = request.IsActive;
        provider.DisplayOrder = request.DisplayOrder;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new InstallmentProviderItem(provider.Id, provider.Name, provider.Code, provider.IsActive, provider.DisplayOrder));
    }

    [Authorize(Policy = Permissions.SalesManage)]
    [HttpDelete("installment-providers/{id:guid}")]
    public async Task<IActionResult> DeleteInstallmentProvider(Guid id, CancellationToken cancellationToken)
    {
        var provider = await db.InstallmentProviders.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (provider is null) return NotFound();
        db.InstallmentProviders.Remove(provider);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = Permissions.SalesRead)]
    [HttpGet("service-types")]
    public async Task<ActionResult<IReadOnlyList<ServiceTypeItem>>> GetServiceTypes(CancellationToken cancellationToken)
    {
        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        await EnsureDefaultServiceTypesAsync(companyId, cancellationToken);
        return Ok(await db.ServiceTypes.AsNoTracking()
            .OrderByDescending(x => x.IsActive).ThenBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .Select(x => new ServiceTypeItem(x.Id, x.Name, x.Code, x.IsActive, x.DisplayOrder))
            .ToListAsync(cancellationToken));
    }

    [Authorize(Policy = Permissions.SalesManage)]
    [HttpPost("service-types")]
    public async Task<ActionResult<ServiceTypeItem>> CreateServiceType(SaveServiceTypeRequest request, CancellationToken cancellationToken)
    {
        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        var name = Clean(request.Name);
        var code = NormalizeServiceTypeCode(request.Code ?? request.Name);
        if (name is null || code.Length == 0) return BadRequest(new { message = "Tên loại dịch vụ là bắt buộc." });
        if (await db.ServiceTypes.AnyAsync(x => x.Code == code, cancellationToken))
            return Conflict(new { message = "Mã loại dịch vụ đã tồn tại." });
        var serviceType = new ServiceType { CompanyId = companyId, Name = name, Code = code, IsActive = request.IsActive, DisplayOrder = request.DisplayOrder };
        db.ServiceTypes.Add(serviceType);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new ServiceTypeItem(serviceType.Id, serviceType.Name, serviceType.Code, serviceType.IsActive, serviceType.DisplayOrder));
    }

    [Authorize(Policy = Permissions.SalesManage)]
    [HttpPut("service-types/{id:guid}")]
    public async Task<ActionResult<ServiceTypeItem>> UpdateServiceType(Guid id, SaveServiceTypeRequest request, CancellationToken cancellationToken)
    {
        var name = Clean(request.Name);
        var code = NormalizeServiceTypeCode(request.Code ?? request.Name);
        if (name is null || code.Length == 0) return BadRequest(new { message = "Tên loại dịch vụ là bắt buộc." });
        if (await db.ServiceTypes.AnyAsync(x => x.Id != id && x.Code == code, cancellationToken))
            return Conflict(new { message = "Mã loại dịch vụ đã tồn tại." });
        var serviceType = await db.ServiceTypes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (serviceType is null) return NotFound();
        serviceType.Name = name;
        serviceType.Code = code;
        serviceType.IsActive = request.IsActive;
        serviceType.DisplayOrder = request.DisplayOrder;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new ServiceTypeItem(serviceType.Id, serviceType.Name, serviceType.Code, serviceType.IsActive, serviceType.DisplayOrder));
    }

    [Authorize(Policy = Permissions.SalesManage)]
    [HttpDelete("service-types/{id:guid}")]
    public async Task<IActionResult> DeleteServiceType(Guid id, CancellationToken cancellationToken)
    {
        var serviceType = await db.ServiceTypes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (serviceType is null) return NotFound();
        var isUsed = await db.SalesInvoiceServiceTypes.AnyAsync(x => x.ServiceTypeId == id, cancellationToken)
            || await db.RepairOrderServiceTypes.AnyAsync(x => x.ServiceTypeId == id, cancellationToken);
        if (isUsed) return BadRequest(new { message = "Loại dịch vụ đã được dùng trên hóa đơn. Hãy chuyển sang trạng thái tắt thay vì xóa." });
        db.ServiceTypes.Remove(serviceType);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = Permissions.SalesRead)]
    [HttpGet("promotions")]
    public async Task<ActionResult<IReadOnlyList<PromotionItem>>> GetPromotions(CancellationToken cancellationToken) =>
        Ok(await db.Promotions.AsNoTracking().OrderByDescending(x => x.IsActive).ThenBy(x => x.Name)
            .Select(x => new PromotionItem(x.Id, x.Name, x.Description, x.DiscountType, x.Value, x.MinimumOrderAmount, x.MaximumDiscountAmount, x.StartsAtUtc, x.EndsAtUtc, x.IsActive))
            .ToListAsync(cancellationToken));

    [Authorize(Policy = Permissions.SalesManage)]
    [HttpPost("promotions")]
    public async Task<ActionResult<PromotionItem>> CreatePromotion(SavePromotionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Value < 0)
            return BadRequest(new { message = "Tên chương trình không được để trống và giá trị khuyến mãi không được âm." });
        var promotion = new Promotion
        {
            CompanyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException(),
            Name = request.Name.Trim(),
            Description = Clean(request.Description),
            DiscountType = request.DiscountType,
            Value = request.Value,
            MinimumOrderAmount = Math.Max(0, request.MinimumOrderAmount),
            MaximumDiscountAmount = request.MaximumDiscountAmount,
            StartsAtUtc = request.StartsAtUtc,
            EndsAtUtc = request.EndsAtUtc,
            IsActive = request.IsActive
        };
        db.Promotions.Add(promotion);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new PromotionItem(promotion.Id, promotion.Name, promotion.Description, promotion.DiscountType, promotion.Value, promotion.MinimumOrderAmount, promotion.MaximumDiscountAmount, promotion.StartsAtUtc, promotion.EndsAtUtc, promotion.IsActive));
    }

    [Authorize(Policy = Permissions.SalesManage)]
    [HttpPut("promotions/{id:guid}")]
    public async Task<ActionResult<PromotionItem>> UpdatePromotion(Guid id, SavePromotionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Value < 0)
            return BadRequest(new { message = "Tên chương trình không được để trống và giá trị khuyến mãi không được âm." });
        if (request.StartsAtUtc.HasValue && request.EndsAtUtc.HasValue && request.EndsAtUtc < request.StartsAtUtc)
            return BadRequest(new { message = "Ngày kết thúc phải sau ngày bắt đầu." });
        var promotion = await db.Promotions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (promotion is null) return NotFound();
        promotion.Name = request.Name.Trim();
        promotion.Description = Clean(request.Description);
        promotion.DiscountType = request.DiscountType;
        promotion.Value = request.Value;
        promotion.MinimumOrderAmount = Math.Max(0, request.MinimumOrderAmount);
        promotion.MaximumDiscountAmount = request.MaximumDiscountAmount;
        promotion.StartsAtUtc = request.StartsAtUtc;
        promotion.EndsAtUtc = request.EndsAtUtc;
        promotion.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new PromotionItem(promotion.Id, promotion.Name, promotion.Description, promotion.DiscountType, promotion.Value, promotion.MinimumOrderAmount, promotion.MaximumDiscountAmount, promotion.StartsAtUtc, promotion.EndsAtUtc, promotion.IsActive));
    }

    [Authorize(Policy = Permissions.SalesManage)]
    [HttpDelete("promotions/{id:guid}")]
    public async Task<IActionResult> DeletePromotion(Guid id, CancellationToken cancellationToken)
    {
        var promotion = await db.Promotions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (promotion is null) return NotFound();
        db.Promotions.Remove(promotion);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = Permissions.SalesRead)]
    [HttpGet("vouchers")]
    public async Task<ActionResult<IReadOnlyList<VoucherItem>>> GetVouchers(CancellationToken cancellationToken) =>
        Ok(await db.Vouchers.AsNoTracking().OrderByDescending(x => x.IsActive).ThenBy(x => x.Code)
            .Select(x => new VoucherItem(x.Id, x.Code, x.Name, x.DiscountType, x.Value, x.MinimumOrderAmount, x.MaximumDiscountAmount, x.StartsAtUtc, x.EndsAtUtc, x.UsageLimit, x.UsedCount, x.IsActive))
            .ToListAsync(cancellationToken));

    [Authorize(Policy = Permissions.SalesRead)]
    [HttpGet("vouchers/validate")]
    public async Task<ActionResult<VoucherValidationResult>> ValidateVoucher([FromQuery] string code, [FromQuery] decimal subtotal, CancellationToken cancellationToken)
    {
        var result = await FindValidVoucher(code, subtotal, DateTimeOffset.UtcNow, cancellationToken);
        if (result.Error is not null) return BadRequest(new { message = result.Error });
        var voucher = result.Voucher!;
        return Ok(new VoucherValidationResult(voucher.Id, voucher.Code, voucher.Name, CalculateDiscount(voucher.DiscountType, voucher.Value, voucher.MaximumDiscountAmount, subtotal)));
    }

    [Authorize(Policy = Permissions.SalesManage)]
    [HttpPost("vouchers")]
    public async Task<ActionResult<VoucherItem>> CreateVoucher(SaveVoucherRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name) || request.Value <= 0)
            return BadRequest(new { message = "Mã, tên và giá trị voucher không hợp lệ." });
        if (await db.Vouchers.AnyAsync(x => x.Code == code, cancellationToken))
            return Conflict(new { message = "Mã voucher đã tồn tại." });
        var voucher = new Voucher
        {
            CompanyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException(),
            Code = code,
            Name = request.Name.Trim(),
            DiscountType = request.DiscountType,
            Value = request.Value,
            MinimumOrderAmount = Math.Max(0, request.MinimumOrderAmount),
            MaximumDiscountAmount = request.MaximumDiscountAmount,
            StartsAtUtc = request.StartsAtUtc,
            EndsAtUtc = request.EndsAtUtc,
            UsageLimit = request.UsageLimit,
            IsActive = request.IsActive
        };
        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new VoucherItem(voucher.Id, voucher.Code, voucher.Name, voucher.DiscountType, voucher.Value, voucher.MinimumOrderAmount, voucher.MaximumDiscountAmount, voucher.StartsAtUtc, voucher.EndsAtUtc, voucher.UsageLimit, voucher.UsedCount, voucher.IsActive));
    }

    [Authorize(Policy = Permissions.SalesManage)]
    [HttpPut("vouchers/{id:guid}")]
    public async Task<ActionResult<VoucherItem>> UpdateVoucher(Guid id, SaveVoucherRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name) || request.Value <= 0)
            return BadRequest(new { message = "Mã, tên và giá trị voucher không hợp lệ." });
        if (request.StartsAtUtc.HasValue && request.EndsAtUtc.HasValue && request.EndsAtUtc < request.StartsAtUtc)
            return BadRequest(new { message = "Ngày kết thúc phải sau ngày bắt đầu." });
        if (await db.Vouchers.AnyAsync(x => x.Code == code && x.Id != id, cancellationToken))
            return Conflict(new { message = "Mã voucher đã tồn tại." });
        var voucher = await db.Vouchers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (voucher is null) return NotFound();
        voucher.Code = code;
        voucher.Name = request.Name.Trim();
        voucher.DiscountType = request.DiscountType;
        voucher.Value = request.Value;
        voucher.MinimumOrderAmount = Math.Max(0, request.MinimumOrderAmount);
        voucher.MaximumDiscountAmount = request.MaximumDiscountAmount;
        voucher.StartsAtUtc = request.StartsAtUtc;
        voucher.EndsAtUtc = request.EndsAtUtc;
        voucher.UsageLimit = request.UsageLimit;
        voucher.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new VoucherItem(voucher.Id, voucher.Code, voucher.Name, voucher.DiscountType, voucher.Value, voucher.MinimumOrderAmount, voucher.MaximumDiscountAmount, voucher.StartsAtUtc, voucher.EndsAtUtc, voucher.UsageLimit, voucher.UsedCount, voucher.IsActive));
    }

    [Authorize(Policy = Permissions.SalesManage)]
    [HttpDelete("vouchers/{id:guid}")]
    public async Task<IActionResult> DeleteVoucher(Guid id, CancellationToken cancellationToken)
    {
        var voucher = await db.Vouchers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (voucher is null) return NotFound();
        db.Vouchers.Remove(voucher);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task EnsureDefaultServiceTypesAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var defaults = new (string Name, string Code, int Order)[]
        {
            ("Laptop", "LAPTOP", 10),
            ("PC", "PC", 20),
            ("Vệ sinh", "VE-SINH", 30),
            ("Sửa Mainboard", "SUA-MAINBOARD", 40),
            ("Thay LCD", "THAY-LCD", 50),
            ("Thay Phím", "THAY-PHIM", 60),
            ("Thay Loa", "THAY-LOA", 70)
        };
        var existingCodes = await db.ServiceTypes.Select(x => x.Code).ToListAsync(cancellationToken);
        var missing = defaults.Where(item => !existingCodes.Contains(item.Code, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Count == 0) return;
        db.ServiceTypes.AddRange(missing.Select(item => new ServiceType
        {
            CompanyId = companyId,
            Name = item.Name,
            Code = item.Code,
            IsActive = true,
            DisplayOrder = item.Order
        }));
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureDefaultInstallmentProvidersAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var defaults = new (string Name, string Code, int Order)[]
        {
            ("HD Saison", "HD-SAISON", 10),
            ("Home Credit", "HOME-CREDIT", 20),
            ("FE Credit", "FE-CREDIT", 30),
            ("Mirae Asset Finance", "MIRAE-ASSET", 40),
            ("Shinhan Finance", "SHINHAN-FINANCE", 50),
            ("JACCS", "JACCS", 60),
            ("Trả góp nội bộ", "NOI-BO", 70)
        };
        var existingCodes = await db.InstallmentProviders.IgnoreQueryFilters()
            .Where(x => x.CompanyId == companyId)
            .Select(x => x.Code)
            .ToListAsync(cancellationToken);
        var missing = defaults.Where(item => !existingCodes.Contains(item.Code, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Count == 0) return;
        db.InstallmentProviders.AddRange(missing.Select(item => new InstallmentProvider
        {
            CompanyId = companyId,
            Name = item.Name,
            Code = item.Code,
            IsActive = true,
            DisplayOrder = item.Order
        }));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<CreateRepairOrderRequest> GetRepairRequests(CreateSalesInvoiceRequest request)
    {
        if (request.Repairs is { Count: > 0 }) return request.Repairs;
        return request.Repair is null ? [] : [request.Repair];
    }

    private static IReadOnlyList<CreateRepairServiceRequest> GetRepairServices(CreateRepairOrderRequest request)
    {
        if (request.Services is { Count: > 0 })
            return request.Services
                .GroupBy(x => x.ServiceTypeId)
                .Select(group => new CreateRepairServiceRequest(
                    group.Key,
                    group.Sum(x => x.Amount),
                    group.Select(x => Clean(x.Notes)).FirstOrDefault(x => x is not null)))
                .ToList();

        var legacyIds = request.ServiceTypeIds?.Distinct().ToList() ?? [];
        return legacyIds.Select((id, index) => new CreateRepairServiceRequest(id, index == 0 ? request.EstimatedCost : 0, null)).ToList();
    }

    private static bool TryAddServiceTypeIds(
        IEnumerable<Guid>? requestedIds,
        IReadOnlyDictionary<Guid, ServiceType> activeServiceTypes,
        ISet<Guid> selectedIds,
        out string? error)
    {
        foreach (var id in requestedIds ?? [])
        {
            if (!activeServiceTypes.ContainsKey(id))
            {
                error = "Có loại dịch vụ không tồn tại hoặc đã bị tắt.";
                return false;
            }
            selectedIds.Add(id);
        }
        error = null;
        return true;
    }

    private static string? DetectProductServiceTypeCode(string productName, string? categoryName)
    {
        var normalized = $" {NormalizeSearchText(categoryName)} {NormalizeSearchText(productName)} ";
        if (normalized.Contains(" LAPTOP ") || normalized.Contains(" MAY TINH XACH TAY ") || normalized.Contains(" NOTEBOOK ")) return "LAPTOP";
        if (normalized.Contains(" PC ") || normalized.Contains(" DESKTOP ") || normalized.Contains(" MAY TINH DE BAN ") || normalized.Contains(" FULL BO ")) return "PC";
        return null;
    }

    private static string NormalizeSearchText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var decomposed = value.Trim().ToUpperInvariant().Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormalizeServiceTypeCode(string value) => NormalizeSearchText(value).Replace(' ', '-');

    private async Task<string?> ApplyDiscountsAsync(
        SalesInvoice invoice,
        CreateSalesInvoiceRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        invoice.PromotionId = null;
        invoice.PromotionName = null;
        invoice.PromotionDiscountAmount = 0;
        invoice.VoucherId = null;
        invoice.VoucherCode = null;
        invoice.VoucherDiscountAmount = 0;
        invoice.DiscountAmount = 0;

        var requestedDiscounts = request.Discounts?.ToList() ?? [];
        if (requestedDiscounts.Count == 0)
        {
            if (request.PromotionId.HasValue)
                requestedDiscounts.Add(new CreateSalesDiscountRequest(SalesDiscountSource.Promotion, request.PromotionId, null, null, 0, null));
            if (request.ManualDiscountAmount > 0)
                requestedDiscounts.Add(new CreateSalesDiscountRequest(SalesDiscountSource.Manual, null, null, request.ManualDiscountName, request.ManualDiscountAmount, null));
            if (!string.IsNullOrWhiteSpace(request.VoucherCode))
                requestedDiscounts.Add(new CreateSalesDiscountRequest(SalesDiscountSource.Voucher, null, request.VoucherCode, null, 0, null));
        }

        var seenPromotions = new HashSet<Guid>();
        var seenVouchers = new HashSet<Guid>();
        var remaining = invoice.Subtotal;
        foreach (var discountRequest in requestedDiscounts)
        {
            if (remaining <= 0) break;
            string name;
            string? code = null;
            Guid? promotionId = null;
            Guid? voucherId = null;
            decimal amount;

            switch (discountRequest.Source)
            {
                case SalesDiscountSource.Promotion:
                {
                    if (!discountRequest.PromotionId.HasValue)
                        return "Dòng khuyến mãi chưa chọn chương trình.";
                    var promotion = await db.Promotions.SingleOrDefaultAsync(
                        x => x.Id == discountRequest.PromotionId.Value && x.IsActive,
                        cancellationToken);
                    if (promotion is null || !IsAvailable(promotion.StartsAtUtc, promotion.EndsAtUtc, now) || invoice.Subtotal < promotion.MinimumOrderAmount)
                        return "Có chương trình khuyến mãi không còn hợp lệ cho hóa đơn này.";
                    if (!seenPromotions.Add(promotion.Id))
                        return $"Khuyến mãi {promotion.Name} đã được thêm trùng.";
                    promotionId = promotion.Id;
                    name = promotion.Name;
                    amount = CalculateDiscount(promotion.DiscountType, promotion.Value, promotion.MaximumDiscountAmount, invoice.Subtotal);
                    invoice.PromotionId ??= promotion.Id;
                    invoice.PromotionName = JoinDiscountNames(invoice.PromotionName, promotion.Name);
                    break;
                }
                case SalesDiscountSource.Voucher:
                {
                    if (string.IsNullOrWhiteSpace(discountRequest.VoucherCode))
                        return "Dòng voucher chưa có mã.";
                    var voucherResult = await FindValidVoucher(discountRequest.VoucherCode, invoice.Subtotal, now, cancellationToken);
                    if (voucherResult.Error is not null) return voucherResult.Error;
                    var voucher = voucherResult.Voucher!;
                    if (!seenVouchers.Add(voucher.Id))
                        return $"Voucher {voucher.Code} đã được thêm trùng.";
                    voucherId = voucher.Id;
                    code = voucher.Code;
                    name = voucher.Name;
                    amount = CalculateDiscount(voucher.DiscountType, voucher.Value, voucher.MaximumDiscountAmount, invoice.Subtotal);
                    voucher.UsedCount++;
                    invoice.VoucherId ??= voucher.Id;
                    invoice.VoucherCode = JoinDiscountNames(invoice.VoucherCode, voucher.Code);
                    break;
                }
                case SalesDiscountSource.Manual:
                {
                    if (discountRequest.Amount <= 0)
                        return "Số tiền giảm nhập tay phải lớn hơn 0.";
                    name = Clean(discountRequest.Name) ?? "Giảm giá nhập tay";
                    amount = discountRequest.Amount;
                    invoice.PromotionName = JoinDiscountNames(invoice.PromotionName, name);
                    break;
                }
                default:
                    return "Loại giảm giá không hợp lệ.";
            }

            amount = Math.Min(remaining, Math.Max(0, amount));
            if (amount <= 0) continue;
            var discount = new SalesDiscountLine
            {
                CompanyId = invoice.CompanyId,
                SalesInvoiceId = invoice.Id,
                Source = discountRequest.Source,
                PromotionId = promotionId,
                VoucherId = voucherId,
                Name = name,
                Code = code,
                Amount = amount,
                Notes = Clean(discountRequest.Notes)
            };
            db.SalesDiscountLines.Add(discount);
            if (!invoice.Discounts.Contains(discount)) invoice.Discounts.Add(discount);
            if (discountRequest.Source == SalesDiscountSource.Voucher)
                invoice.VoucherDiscountAmount += amount;
            else
                invoice.PromotionDiscountAmount += amount;
            invoice.DiscountAmount += amount;
            remaining -= amount;
        }

        invoice.TotalAmount = Math.Max(0, invoice.Subtotal - invoice.DiscountAmount + invoice.TaxAmount);
        return null;
    }

    private async Task RestoreVoucherUsageAsync(SalesInvoice invoice, CancellationToken cancellationToken)
    {
        var voucherIds = invoice.Discounts
            .Where(x => x.Source == SalesDiscountSource.Voucher && x.VoucherId.HasValue)
            .Select(x => x.VoucherId!.Value)
            .Distinct()
            .ToList();
        if (voucherIds.Count == 0 && invoice.VoucherId.HasValue) voucherIds.Add(invoice.VoucherId.Value);
        if (voucherIds.Count == 0) return;
        var vouchers = await db.Vouchers.IgnoreQueryFilters()
            .Where(x => voucherIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        foreach (var voucher in vouchers)
            if (voucher.UsedCount > 0) voucher.UsedCount--;
    }

    private async Task<string> NextInvoiceNumber(SalesInvoiceType type, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var localNow = TimeZoneInfo.ConvertTime(now, GetBusinessTimeZone());
        var prefix = $"{(type == SalesInvoiceType.Repair ? "SC" : "HD")}{localNow:yyyyMMdd}-";
        var last = await db.SalesInvoices.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.CompanyId == currentUser.CompanyId && x.Number.StartsWith(prefix))
            .OrderByDescending(x => x.Number)
            .Select(x => x.Number)
            .FirstOrDefaultAsync(cancellationToken);
        var next = last is not null && int.TryParse(last[prefix.Length..], out var parsed) ? parsed + 1 : 1;
        return $"{prefix}{next:000}";
    }

    private async Task<string> NextCustomerCode(CancellationToken cancellationToken)
    {
        var codes = await db.Customers.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.CompanyId == currentUser.CompanyId && x.Code.StartsWith("KH"))
            .Select(x => x.Code)
            .ToListAsync(cancellationToken);
        var nextNumber = codes
            .Select(code => int.TryParse(code.AsSpan(2), out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"KH{nextNumber:000000}";
    }

    private async Task<(Voucher? Voucher, string? Error)> FindValidVoucher(string code, decimal subtotal, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var voucher = await db.Vouchers.SingleOrDefaultAsync(x => x.Code == normalized, cancellationToken);
        if (voucher is null || !voucher.IsActive) return (null, "Mã voucher không tồn tại hoặc đã tắt.");
        if (!IsAvailable(voucher.StartsAtUtc, voucher.EndsAtUtc, now)) return (null, "Mã voucher chưa đến thời gian sử dụng hoặc đã hết hạn.");
        if (voucher.UsageLimit.HasValue && voucher.UsedCount >= voucher.UsageLimit.Value) return (null, "Mã voucher đã hết lượt sử dụng.");
        if (subtotal < voucher.MinimumOrderAmount) return (null, $"Hóa đơn chưa đạt giá trị tối thiểu {voucher.MinimumOrderAmount:n0} đ.");
        return (voucher, null);
    }

    private static InvoiceStatus ResolveStatus(decimal total, decimal paid) => paid >= total
        ? InvoiceStatus.Paid
        : paid > 0 ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Confirmed;

    private static decimal CalculateDiscount(DiscountType type, decimal value, decimal? maximum, decimal subtotal)
    {
        var discount = type == DiscountType.Percentage ? subtotal * value / 100m : value;
        if (maximum.HasValue) discount = Math.Min(discount, maximum.Value);
        return Math.Clamp(discount, 0, subtotal);
    }

    private static bool IsAvailable(DateTimeOffset? startsAt, DateTimeOffset? endsAt, DateTimeOffset now) =>
        (!startsAt.HasValue || startsAt <= now) && (!endsAt.HasValue || endsAt >= now);

    private static string NormalizePhone(string value) => new(value.Where(char.IsDigit).ToArray());
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string JoinDiscountNames(string? current, string next) => string.IsNullOrWhiteSpace(current) ? next : $"{current} + {next}";
    private static RepairStatus NormalizeProcessingStatus(RepairStatus? status) => status switch
    {
        RepairStatus.Returned => RepairStatus.Returned,
        RepairStatus.Cancelled => RepairStatus.Cancelled,
        _ => RepairStatus.InProgress
    };
    private static bool IsProcessed(SalesInvoice invoice) => invoice.RepairStatus == RepairStatus.Returned;
    private static TimeZoneInfo GetBusinessTimeZone() => TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    private static bool TryParseBusinessDate(string value, out DateTimeOffset startUtc, out DateTimeOffset endUtc)
    {
        var formats = new[] { "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "dd-MM-yyyy", "yyyy-M-d", "yyyy-MM-dd" };
        if (!DateTime.TryParseExact(value.Trim(), formats, CultureInfo.GetCultureInfo("vi-VN"), DateTimeStyles.AllowWhiteSpaces, out var date))
        {
            startUtc = default;
            endUtc = default;
            return false;
        }

        var localStart = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
        startUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, GetBusinessTimeZone()), TimeSpan.Zero);
        endUtc = startUtc.AddDays(1);
        return true;
    }

    private static bool TryParseMoneySearch(string value, out decimal amount)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.Any(character =>
                !char.IsDigit(character) && !char.IsWhiteSpace(character) && character is not '.' and not ',' and not 'đ' and not 'Đ' and not '₫'))
        {
            amount = default;
            return false;
        }

        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        return decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out amount);
    }

    private static string NormalizeSearchKeyword(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(character));
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private async Task<SalesInvoiceDetails> ToDetailsAsync(SalesInvoice invoice, CancellationToken cancellationToken)
    {
        var company = await db.Companies.AsNoTracking().SingleAsync(x => x.Id == invoice.CompanyId, cancellationToken);
        var issuer = invoice.CreatedByUserId.HasValue
            ? await db.Users.AsNoTracking().Where(x => x.Id == invoice.CreatedByUserId.Value)
                .Select(x => new InvoiceIssuerItem(x.Id, x.FullName, x.Email))
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        issuer ??= new InvoiceIssuerItem(currentUser.UserId, currentUser.Email ?? "Nhân viên", currentUser.Email);

        var discounts = invoice.Discounts
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new SalesDiscountItem(x.Id, x.Source, x.PromotionId, x.VoucherId, x.Name, x.Code, x.Amount, x.Notes))
            .ToList();
        if (discounts.Count == 0)
        {
            if (invoice.PromotionDiscountAmount > 0)
                discounts.Add(new SalesDiscountItem(Guid.Empty, SalesDiscountSource.Promotion, invoice.PromotionId, null, invoice.PromotionName ?? "Khuyến mãi", null, invoice.PromotionDiscountAmount, null));
            if (invoice.VoucherDiscountAmount > 0)
                discounts.Add(new SalesDiscountItem(Guid.Empty, SalesDiscountSource.Voucher, null, invoice.VoucherId, invoice.VoucherCode ?? "Voucher", invoice.VoucherCode, invoice.VoucherDiscountAmount, null));
        }

        var serviceTypes = await db.SalesInvoiceServiceTypes.AsNoTracking()
            .Where(x => x.SalesInvoiceId == invoice.Id)
            .OrderBy(x => x.ServiceType.DisplayOrder).ThenBy(x => x.ServiceType.Name)
            .Select(x => new ServiceTypeItem(x.ServiceType.Id, x.ServiceType.Name, x.ServiceType.Code, x.ServiceType.IsActive, x.ServiceType.DisplayOrder))
            .ToListAsync(cancellationToken);
        var repairs = await db.RepairOrders.AsNoTracking()
            .Where(x => x.SalesInvoiceId == invoice.Id)
            .Include(x => x.ServiceTypes).ThenInclude(x => x.ServiceType)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var repairItems = repairs.Select(repair => new RepairOrderItem(
            repair.DeviceModel,
            repair.SerialNumber,
            repair.Accessories,
            repair.DeviceCondition,
            repair.CustomerRequest,
            repair.TechnicianAssessment,
            repair.EstimatedCost,
            repair.ExpectedReturnAtUtc,
            repair.Status,
            repair.Notes)
        {
            Id = repair.Id,
            ServiceTypes = repair.ServiceTypes.OrderBy(x => x.ServiceType.DisplayOrder).ThenBy(x => x.ServiceType.Name)
                .Select(x => new ServiceTypeItem(x.ServiceType.Id, x.ServiceType.Name, x.ServiceType.Code, x.ServiceType.IsActive, x.ServiceType.DisplayOrder)).ToList(),
            Services = repair.ServiceTypes.OrderBy(x => x.ServiceType.DisplayOrder).ThenBy(x => x.ServiceType.Name)
                .Select(x => new RepairServiceItem(x.ServiceType.Id, x.ServiceType.Name, x.ServiceType.Code, x.Amount, x.Notes)).ToList()
        }).ToList();

        return new SalesInvoiceDetails(
            invoice.Id,
            invoice.Number,
            invoice.Type,
            invoice.CustomerId,
            invoice.CustomerName,
            invoice.CustomerPhone,
            invoice.CustomerEmail,
            invoice.CustomerAddress,
            invoice.InvoiceDateUtc,
            invoice.Status,
            invoice.Subtotal,
            invoice.DiscountAmount,
            invoice.PromotionId,
            invoice.PromotionName,
            invoice.PromotionDiscountAmount,
            invoice.VoucherCode,
            invoice.VoucherDiscountAmount,
            invoice.TotalAmount,
            invoice.PaidAmount,
            invoice.TotalAmount - invoice.PaidAmount,
            invoice.IsInstallment,
            invoice.InstallmentProvider,
            invoice.InstallmentReference,
            invoice.PaymentDueDateUtc,
            invoice.Notes ?? invoice.Lines.OrderBy(x => x.CreatedAtUtc).Select(x => x.ProductName).FirstOrDefault(),
            new InvoiceCompanyItem(company.Name, company.TaxCode, company.Address, company.Phone, company.Email, company.Website, company.LogoDataUrl),
            issuer,
            discounts,
            invoice.Lines.OrderBy(x => x.CreatedAtUtc).Select(x => new SalesInvoiceLineItem(x.Id, x.ProductId, x.Sku, x.ProductName, x.Quantity, x.UnitName, x.UnitPrice, x.DiscountAmount, x.LineTotal, x.Notes) { WarrantyPeriod = x.WarrantyPeriod }).ToList(),
            invoice.Payments.OrderBy(x => x.PaidAtUtc).Select(x => new SalesPaymentItem(x.Id, x.Method, x.PaymentAccountId, x.PaymentAccount?.Name, x.Amount, x.PaidAtUtc, x.IsScheduled, x.Reference, x.Notes)).ToList(),
            repairItems.FirstOrDefault())
        {
            InstallmentPaymentAccountId = invoice.InstallmentPaymentAccountId,
            InstallmentPaymentAccountName = invoice.InstallmentPaymentAccount?.Name,
            RepairStatus = invoice.RepairStatus,
            ExpectedReturnAtUtc = invoice.ExpectedReturnAtUtc,
            Repairs = repairItems,
            ServiceTypes = serviceTypes
        };
    }
}

public sealed record SalesSummary(decimal InvoiceTotalToday, decimal CollectedToday, decimal Outstanding, int ActiveRepairs, int ReadyForReturnRepairs);
public sealed record SalesInvoiceListItem(Guid Id, string Number, SalesInvoiceType Type, Guid? CustomerId, string CustomerName, string? CustomerPhone, DateTimeOffset InvoiceDateUtc, InvoiceStatus Status, decimal TotalAmount, decimal PaidAmount, decimal OutstandingAmount, bool IsInstallment, DateTimeOffset? PaymentDueDateUtc, string? RepairDeviceModel, DateTimeOffset? ExpectedReturnAtUtc, RepairStatus? RepairStatus, string? Notes)
{
    public Guid? CreatedByUserId { get; init; }
    public string CreatedByName { get; init; } = "Dữ liệu cũ";
    public decimal ReceivedAmount { get; init; }
    public decimal ScheduledAmount { get; init; }
    public decimal CustomerTotalSpent { get; init; }
    public int CustomerInvoiceCount { get; init; }
    public int RepairDeviceCount { get; init; }
    public int RepairPendingCount { get; init; }
    public int RepairReadyCount { get; init; }
    public int RepairReturnedCount { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
    public string? InstallmentProvider { get; init; }
    public IReadOnlyList<SalesPaymentSourceListItem> PaymentSources { get; init; } = [];
    public IReadOnlyList<ServiceTypeItem> ServiceTypes { get; init; } = [];
}
public sealed record SalesPaymentSourceListItem(PaymentMethod Method, Guid? PaymentAccountId, string? PaymentAccountName, string? BankName, string? AccountNumber, decimal Amount, bool IsScheduled);
public sealed record SalesCreatorFilterItem(Guid Id, string Name);
public sealed record SalesFilterOptions(IReadOnlyList<SalesCreatorFilterItem> Creators, IReadOnlyList<PaymentAccountItem> PaymentAccounts);
public sealed record SalesInvoiceDetails(Guid Id, string Number, SalesInvoiceType Type, Guid? CustomerId, string CustomerName, string? CustomerPhone, string? CustomerEmail, string? CustomerAddress, DateTimeOffset InvoiceDateUtc, InvoiceStatus Status, decimal Subtotal, decimal DiscountAmount, Guid? PromotionId, string? PromotionName, decimal PromotionDiscountAmount, string? VoucherCode, decimal VoucherDiscountAmount, decimal TotalAmount, decimal PaidAmount, decimal OutstandingAmount, bool IsInstallment, string? InstallmentProvider, string? InstallmentReference, DateTimeOffset? PaymentDueDateUtc, string? Notes, InvoiceCompanyItem Company, InvoiceIssuerItem Issuer, IReadOnlyList<SalesDiscountItem> Discounts, IReadOnlyList<SalesInvoiceLineItem> Lines, IReadOnlyList<SalesPaymentItem> Payments, RepairOrderItem? Repair)
{
    public Guid? InstallmentPaymentAccountId { get; init; }
    public string? InstallmentPaymentAccountName { get; init; }
    public RepairStatus? RepairStatus { get; init; }
    public DateTimeOffset? ExpectedReturnAtUtc { get; init; }
    public IReadOnlyList<RepairOrderItem> Repairs { get; init; } = [];
    public IReadOnlyList<ServiceTypeItem> ServiceTypes { get; init; } = [];
}
public sealed record SalesDiscountItem(Guid Id, SalesDiscountSource Source, Guid? PromotionId, Guid? VoucherId, string Name, string? Code, decimal Amount, string? Notes);
public sealed record SalesInvoiceLineItem(Guid Id, Guid? ProductId, string Sku, string ProductName, decimal Quantity, string UnitName, decimal UnitPrice, decimal DiscountAmount, decimal LineTotal, string? Notes)
{
    public string WarrantyPeriod { get; init; } = string.Empty;
}
public sealed record SalesPaymentItem(Guid Id, PaymentMethod Method, Guid? PaymentAccountId, string? PaymentAccountName, decimal Amount, DateTimeOffset PaidAtUtc, bool IsScheduled, string? Reference, string? Notes);
public sealed record RepairOrderItem(string DeviceModel, string? SerialNumber, string? Accessories, string DeviceCondition, string CustomerRequest, string? TechnicianAssessment, decimal EstimatedCost, DateTimeOffset? ExpectedReturnAtUtc, RepairStatus Status, string? Notes)
{
    public Guid Id { get; init; }
    public IReadOnlyList<ServiceTypeItem> ServiceTypes { get; init; } = [];
    public IReadOnlyList<RepairServiceItem> Services { get; init; } = [];
}
public sealed record InvoiceCompanyItem(string Name, string? TaxCode, string? Address, string? Phone, string? Email, string? Website, string? LogoDataUrl);
public sealed record InvoiceIssuerItem(Guid? Id, string Name, string? Email);

public sealed record CreateSalesInvoiceRequest(SalesInvoiceType Type, string CustomerName, string CustomerPhone, string? CustomerEmail, string? CustomerAddress, string? CustomerNotes, DateTimeOffset? InvoiceDateUtc, IReadOnlyList<CreateSalesInvoiceLineRequest> Lines, Guid? PromotionId, string? ManualDiscountName, decimal ManualDiscountAmount, string? VoucherCode, IReadOnlyList<CreateSalesPaymentRequest> Payments, bool IsInstallment, string? InstallmentProvider, string? InstallmentReference, DateTimeOffset? PaymentDueDateUtc, CreateRepairOrderRequest? Repair, string? Notes)
{
    public IReadOnlyList<CreateSalesDiscountRequest>? Discounts { get; init; }
    public Guid? InstallmentPaymentAccountId { get; init; }
    public IReadOnlyList<CreateRepairOrderRequest>? Repairs { get; init; }
    public IReadOnlyList<Guid>? ServiceTypeIds { get; init; }
    public RepairStatus? RepairStatus { get; init; }
    public DateTimeOffset? ExpectedReturnAtUtc { get; init; }
}
public sealed record CreateSalesInvoiceLineRequest(Guid? ProductId, string? ProductName, decimal Quantity, string? UnitName, decimal UnitPrice, decimal DiscountAmount, string? Notes)
{
    public string? WarrantyPeriod { get; init; }
    public IReadOnlyList<Guid>? ServiceTypeIds { get; init; }
}
public sealed record CreateSalesDiscountRequest(SalesDiscountSource Source, Guid? PromotionId, string? VoucherCode, string? Name, decimal Amount, string? Notes);
public sealed record CreateSalesPaymentRequest(PaymentMethod Method, Guid? PaymentAccountId, decimal Amount, DateTimeOffset? PaidAtUtc, string? Reference, string? Notes);
public sealed record CreateRepairOrderRequest(string DeviceModel, string? SerialNumber, string? Accessories, string? DeviceCondition, string? CustomerRequest, string? TechnicianAssessment, decimal EstimatedCost, DateTimeOffset? ExpectedReturnAtUtc, string? Notes)
{
    public IReadOnlyList<Guid>? ServiceTypeIds { get; init; }
    public IReadOnlyList<CreateRepairServiceRequest>? Services { get; init; }
    public RepairStatus? Status { get; init; }
}
public sealed record CreateRepairServiceRequest(Guid ServiceTypeId, decimal Amount, string? Notes);
public sealed record AddSalesPaymentRequest(PaymentMethod Method, Guid? PaymentAccountId, decimal Amount, DateTimeOffset? PaidAtUtc, string? Reference, string? Notes);

public sealed record PaymentAccountItem(Guid Id, string Name, string BankName, string AccountNumber, string AccountHolder, bool IsActive, int DisplayOrder);
public sealed record SavePaymentAccountRequest(string Name, string BankName, string AccountNumber, string? AccountHolder, bool IsActive = true, int DisplayOrder = 0);
public sealed record InstallmentProviderItem(Guid Id, string Name, string Code, bool IsActive, int DisplayOrder);
public sealed record SaveInstallmentProviderRequest(string Name, string? Code, bool IsActive = true, int DisplayOrder = 0);
public sealed record PromotionItem(Guid Id, string Name, string? Description, DiscountType DiscountType, decimal Value, decimal MinimumOrderAmount, decimal? MaximumDiscountAmount, DateTimeOffset? StartsAtUtc, DateTimeOffset? EndsAtUtc, bool IsActive);
public sealed record SavePromotionRequest(string Name, string? Description, DiscountType DiscountType, decimal Value, decimal MinimumOrderAmount, decimal? MaximumDiscountAmount, DateTimeOffset? StartsAtUtc, DateTimeOffset? EndsAtUtc, bool IsActive = true);
public sealed record VoucherItem(Guid Id, string Code, string Name, DiscountType DiscountType, decimal Value, decimal MinimumOrderAmount, decimal? MaximumDiscountAmount, DateTimeOffset? StartsAtUtc, DateTimeOffset? EndsAtUtc, int? UsageLimit, int UsedCount, bool IsActive);
public sealed record SaveVoucherRequest(string Code, string Name, DiscountType DiscountType, decimal Value, decimal MinimumOrderAmount, decimal? MaximumDiscountAmount, DateTimeOffset? StartsAtUtc, DateTimeOffset? EndsAtUtc, int? UsageLimit, bool IsActive = true);
public sealed record VoucherValidationResult(Guid Id, string Code, string Name, decimal DiscountAmount);
public sealed record ServiceTypeItem(Guid Id, string Name, string Code, bool IsActive, int DisplayOrder);
public sealed record RepairServiceItem(Guid Id, string Name, string Code, decimal Amount, string? Notes);
public sealed record SaveServiceTypeRequest(string Name, string? Code, bool IsActive = true, int DisplayOrder = 0);
