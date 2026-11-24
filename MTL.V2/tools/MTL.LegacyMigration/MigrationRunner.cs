using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MTL.Domain.Crm;
using MTL.Domain.Purchasing;
using MTL.Domain.Sales;
using MTL.Infrastructure.Identity;
using MTL.Infrastructure.Persistence;

namespace MTL.LegacyMigration;

internal sealed class MigrationRunner(MigrationOptions options, LegacyDataset source)
{
    private const string LegacySource = "APP.MTL";
    private readonly TimeZoneInfo _businessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public async Task<MigrationReport> RunAsync(CancellationToken cancellationToken)
    {
        await using var db = CreateDbContext();
        if (!await db.Companies.IgnoreQueryFilters().AnyAsync(x => x.Id == options.TargetCompanyId, cancellationToken))
            throw new InvalidOperationException($"Không tìm thấy công ty đích {options.TargetCompanyId}.");

        var report = await BuildReportAsync(db, cancellationToken);
        if (!options.Apply) return report;
        if (report.NumberCollisions > 0)
            throw new InvalidOperationException($"Có {report.NumberCollisions} mã hóa đơn trùng với dữ liệu đích. Migration đã dừng trước khi ghi dữ liệu.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var customerMap = await ImportCustomersAsync(db, report, cancellationToken);
            var serviceTypeMap = await EnsureServiceTypesAsync(db, cancellationToken);
            await ImportCurrentInvoicesAsync(db, customerMap, serviceTypeMap, report, cancellationToken);
            if (options.IncludeArchive)
                await ImportArchiveInvoicesAsync(db, report, cancellationToken);
            await ReconcileLegacyCashPaymentsAsync(db, report, cancellationToken);
            await ImportPurchasesAsync(db, report, cancellationToken);
            await RecalculateCustomerStatisticsAsync(db, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        db.ChangeTracker.Clear();
        report.TargetCustomersAfter = await db.Customers.IgnoreQueryFilters()
            .CountAsync(x => x.CompanyId == options.TargetCompanyId && !x.IsDeleted, cancellationToken);
        report.TargetInvoicesAfter = await db.SalesInvoices.IgnoreQueryFilters()
            .CountAsync(x => x.CompanyId == options.TargetCompanyId && !x.IsDeleted, cancellationToken);
        report.TargetPurchasesAfter = await db.PurchaseInvoices.IgnoreQueryFilters()
            .CountAsync(x => x.CompanyId == options.TargetCompanyId && !x.IsDeleted, cancellationToken);
        report.TargetImportedInvoiceTotal = await db.SalesInvoices.IgnoreQueryFilters()
            .Where(x => x.CompanyId == options.TargetCompanyId && !x.IsDeleted && x.LegacySource == LegacySource)
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0;
        report.TargetImportedPurchaseTotal = await db.PurchaseInvoices.IgnoreQueryFilters()
            .Where(x => x.CompanyId == options.TargetCompanyId && !x.IsDeleted && x.LegacySource == LegacySource)
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0;
        return report;
    }

    private AppDbContext CreateDbContext()
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        builder.UseNpgsql(options.TargetConnectionString, sql => sql.CommandTimeout(180));
        return new AppDbContext(builder.Options, new MigrationCurrentUser(options.TargetCompanyId));
    }

    private async Task<MigrationReport> BuildReportAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var importedCurrent = source.Invoices.Where(x => x.DocumentTypeId != 4).ToList();
        var quotes = source.Invoices.Where(x => x.DocumentTypeId == 4).ToList();
        var importedNumbers = importedCurrent.Select(x => x.Number)
            .Concat(options.IncludeArchive ? source.ArchiveInvoices.Select(x => ArchiveNumber(x.Id)) : [])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingLegacyIds = await db.SalesInvoices.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.CompanyId == options.TargetCompanyId && x.LegacySource == LegacySource && x.LegacyId != null)
            .Select(x => x.LegacyId!)
            .ToHashSetAsync(cancellationToken);
        var existingNumbers = await db.SalesInvoices.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.CompanyId == options.TargetCompanyId && importedNumbers.Contains(x.Number))
            .Select(x => new { x.Number, x.LegacySource, x.LegacyId })
            .ToListAsync(cancellationToken);
        var collisionCount = existingNumbers.Count(x => x.LegacySource != LegacySource);
        var existingPurchaseLegacyIds = await db.PurchaseInvoices.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.CompanyId == options.TargetCompanyId && x.LegacySource == LegacySource && x.LegacyId != null)
            .Select(x => x.LegacyId!).ToHashSetAsync(cancellationToken);

        var existingCustomers = await db.Customers.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.CompanyId == options.TargetCompanyId && !x.IsDeleted)
            .Select(x => new { x.LegacySource, x.LegacyId, x.Phone })
            .ToListAsync(cancellationToken);
        var sourcePhoneCounts = source.Customers.GroupBy(x => NormalizePhone(x.Phone))
            .Where(x => !string.IsNullOrEmpty(x.Key))
            .ToDictionary(x => x.Key!, x => x.Count());
        var targetPhoneCounts = existingCustomers.GroupBy(x => x.Phone)
            .Where(x => !string.IsNullOrEmpty(x.Key))
            .ToDictionary(x => x.Key!, x => x.Count());
        var matchedCustomers = source.Customers.Count(customer =>
            existingCustomers.Any(x => x.LegacySource == LegacySource && x.LegacyId == CustomerLegacyId(customer.Id)) ||
            (NormalizePhone(customer.Phone) is { Length: > 0 } phone && sourcePhoneCounts[phone] == 1 &&
             targetPhoneCounts.TryGetValue(phone, out var targetCount) && targetCount == 1));

        var customerIds = source.Customers.Select(x => x.Id).ToHashSet();
        var importedInvoiceNumbers = importedCurrent.Select(x => x.Number).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentLines = source.InvoiceLines.Where(x => importedInvoiceNumbers.Contains(x.InvoiceNumber)).ToList();
        var archiveIds = source.ArchiveInvoices.Select(x => x.Id).ToHashSet();
        var archiveOrphans = source.ArchiveInvoiceLines.Count(x => !archiveIds.Contains(x.InvoiceId));
        var report = new MigrationReport
        {
            Mode = options.Apply ? "apply" : "dry-run",
            SourceCompanyId = options.SourceCompanyId,
            TargetCompanyId = options.TargetCompanyId,
            SourceCustomers = source.Customers.Count,
            SourceCustomersWithCitizenId = source.Customers.Count(x => !string.IsNullOrWhiteSpace(x.CitizenId)),
            CurrentInvoices = importedCurrent.Count,
            CurrentInvoiceLines = currentLines.Count,
            CurrentInvoiceTotal = importedCurrent.Sum(x => x.TotalAmount),
            ProcessedCurrentInvoices = importedCurrent.Count(x => x.IsProcessed),
            ProcessingCurrentInvoices = importedCurrent.Count(x => !x.IsProcessed),
            ExcludedQuotes = quotes.Count,
            ExcludedQuoteTotal = quotes.Sum(x => x.TotalAmount),
            ArchiveInvoices = options.IncludeArchive ? source.ArchiveInvoices.Count : 0,
            ArchiveInvoiceLines = options.IncludeArchive ? source.ArchiveInvoiceLines.Count - archiveOrphans : 0,
            ArchiveOrphanLines = options.IncludeArchive ? archiveOrphans : 0,
            ArchiveInvoiceTotal = options.IncludeArchive ? source.ArchiveInvoices.Sum(x => x.TotalAmount) : 0,
            PurchaseInvoices = source.Purchases.Count,
            PurchaseInvoiceLines = source.PurchaseLines.Count,
            PurchaseInvoiceTotal = source.Purchases.Sum(x => x.TotalAmount),
            TargetCustomersBefore = existingCustomers.Count,
            TargetInvoicesBefore = await db.SalesInvoices.IgnoreQueryFilters().CountAsync(x => x.CompanyId == options.TargetCompanyId && !x.IsDeleted, cancellationToken),
            TargetPurchasesBefore = await db.PurchaseInvoices.IgnoreQueryFilters().CountAsync(x => x.CompanyId == options.TargetCompanyId && !x.IsDeleted, cancellationToken),
            PlannedMatchedCustomers = matchedCustomers,
            PlannedNewCustomers = source.Customers.Count - matchedCustomers,
            ExistingImportedInvoices = existingLegacyIds.Count,
            PlannedNewInvoices = importedCurrent.Count(x => !existingLegacyIds.Contains(CurrentInvoiceLegacyId(x.Number))) +
                                 (options.IncludeArchive ? source.ArchiveInvoices.Count(x => !existingLegacyIds.Contains(ArchiveInvoiceLegacyId(x.Id))) : 0),
            PlannedNewPurchases = source.Purchases.Count(x => !existingPurchaseLegacyIds.Contains(PurchaseLegacyId(x.Number))),
            NumberCollisions = collisionCount
        };

        var duplicatePhones = sourcePhoneCounts.Count(x => x.Value > 1);
        if (duplicatePhones > 0) report.Warnings.Add($"Có {duplicatePhones} nhóm số điện thoại trùng trong nguồn; các hồ sơ này được giữ riêng theo ID cũ.");
        var invalidCustomerLinks = importedCurrent.Count(x => x.CustomerId.HasValue && !customerIds.Contains(x.CustomerId.Value));
        if (invalidCustomerLinks > 0) report.Warnings.Add($"Có {invalidCustomerLinks} hóa đơn trỏ tới khách hàng đã bị xóa; hóa đơn vẫn được giữ bằng thông tin snapshot.");
        var invoicesWithoutLines = importedCurrent.Count(x => currentLines.All(line => !string.Equals(line.InvoiceNumber, x.Number, StringComparison.OrdinalIgnoreCase)));
        if (invoicesWithoutLines > 0) report.Warnings.Add($"Có {invoicesWithoutLines} hóa đơn không có dòng chi tiết; công cụ sẽ tạo một dòng đối soát theo tổng hóa đơn.");
        if (archiveOrphans > 0) report.Warnings.Add($"Có {archiveOrphans} dòng chi tiết lưu trữ không còn hóa đơn cha; các dòng mồ côi không được nhập.");
        if (quotes.Count > 0) report.Warnings.Add($"Loại trừ {quotes.Count} báo giá khỏi doanh thu và nguồn tiền; tổng giá trị báo giá {quotes.Sum(x => x.TotalAmount):N0} đ.");
        var purchaseNumbers = source.Purchases.Select(x => x.Number).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var purchaseOrphans = source.PurchaseLines.Count(x => !purchaseNumbers.Contains(x.InvoiceNumber));
        if (purchaseOrphans > 0) report.Warnings.Add($"Có {purchaseOrphans} dòng nhập hàng không còn phiếu cha; các dòng này không được nhập.");
        if (source.Purchases.Any(x => !x.SupplierId.HasValue)) report.Warnings.Add("Phiếu nhập không có nhà cung cấp sẽ được gắn vào nhà cung cấp 'Không xác định (dữ liệu cũ)'.");
        if (source.Purchases.Count > 0) report.Warnings.Add("Phiếu nhập lịch sử chỉ phục vụ tra cứu và công nợ; không cộng lại tồn kho hiện tại.");
        return report;
    }

    private async Task<Dictionary<int, Guid>> ImportCustomersAsync(AppDbContext db, MigrationReport report, CancellationToken cancellationToken)
    {
        var existing = await db.Customers.IgnoreQueryFilters()
            .Where(x => x.CompanyId == options.TargetCompanyId && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        var byLegacy = existing.Where(x => x.LegacySource == LegacySource && x.LegacyId != null)
            .ToDictionary(x => x.LegacyId!, StringComparer.OrdinalIgnoreCase);
        var sourcePhoneCounts = source.Customers.GroupBy(x => NormalizePhone(x.Phone))
            .Where(x => !string.IsNullOrEmpty(x.Key))
            .ToDictionary(x => x.Key!, x => x.Count());
        var targetByPhone = existing.Where(x => !string.IsNullOrWhiteSpace(x.Phone))
            .GroupBy(x => x.Phone!)
            .ToDictionary(x => x.Key, x => x.ToList());
        var claimedTargets = new HashSet<Guid>();
        var map = new Dictionary<int, Guid>();
        var nextCode = NextCustomerNumber(existing.Select(x => x.Code));

        foreach (var sourceCustomer in source.Customers)
        {
            var legacyId = CustomerLegacyId(sourceCustomer.Id);
            if (byLegacy.TryGetValue(legacyId, out var known))
            {
                map[sourceCustomer.Id] = known.Id;
                claimedTargets.Add(known.Id);
                report.MatchedCustomers++;
                continue;
            }

            var phone = NormalizePhone(sourceCustomer.Phone);
            Customer? customer = null;
            if (phone is { Length: > 0 } && sourcePhoneCounts[phone] == 1 &&
                targetByPhone.TryGetValue(phone, out var matches) && matches.Count == 1 &&
                !claimedTargets.Contains(matches[0].Id) && string.IsNullOrWhiteSpace(matches[0].LegacyId))
            {
                customer = matches[0];
                customer.LegacySource = LegacySource;
                customer.LegacyId = legacyId;
                customer.CitizenId ??= Clean(sourceCustomer.CitizenId);
                customer.Address ??= Clean(sourceCustomer.Address);
                customer.TaxCode ??= Clean(sourceCustomer.TaxCode);
                customer.Email ??= Clean(sourceCustomer.Email);
                customer.Notes ??= Clean(sourceCustomer.Notes);
                report.MatchedCustomers++;
            }
            else
            {
                customer = new Customer
                {
                    CompanyId = options.TargetCompanyId,
                    Code = $"KH{nextCode++:000000}",
                    FullName = Clean(sourceCustomer.FullName) ?? $"Khách hàng cũ #{sourceCustomer.Id}",
                    Phone = phone,
                    Email = Clean(sourceCustomer.Email),
                    Address = Clean(sourceCustomer.Address),
                    CitizenId = Clean(sourceCustomer.CitizenId),
                    TaxCode = Clean(sourceCustomer.TaxCode),
                    Notes = Clean(sourceCustomer.Notes),
                    LegacySource = LegacySource,
                    LegacyId = legacyId
                };
                db.Customers.Add(customer);
                existing.Add(customer);
                if (phone is { Length: > 0 })
                {
                    if (!targetByPhone.TryGetValue(phone, out var list)) targetByPhone[phone] = list = [];
                    list.Add(customer);
                }
                report.InsertedCustomers++;
            }

            map[sourceCustomer.Id] = customer.Id;
            claimedTargets.Add(customer.Id);
        }

        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return map;
    }

    private async Task<Dictionary<int, Guid>> EnsureServiceTypesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.ServiceTypes.IgnoreQueryFilters()
            .Where(x => x.CompanyId == options.TargetCompanyId && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        var byName = existing.GroupBy(x => NormalizeText(x.Name)).ToDictionary(x => x.Key, x => x.First());
        var usedCodes = existing.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var map = new Dictionary<int, Guid>();
        var displayOrder = existing.Count == 0 ? 10 : existing.Max(x => x.DisplayOrder) + 10;

        foreach (var tag in source.Tags)
        {
            var normalizedName = NormalizeText(MapLegacyTagName(tag.Name));
            if (byName.TryGetValue(normalizedName, out var serviceType))
            {
                map[tag.Id] = serviceType.Id;
                continue;
            }

            var name = MapLegacyTagName(tag.Name);
            var code = Slug(name);
            if (usedCodes.Contains(code)) code = $"{code}-{tag.Id}";
            serviceType = new ServiceType
            {
                CompanyId = options.TargetCompanyId,
                Name = name,
                Code = code,
                IsActive = true,
                DisplayOrder = displayOrder
            };
            displayOrder += 10;
            db.ServiceTypes.Add(serviceType);
            existing.Add(serviceType);
            byName[normalizedName] = serviceType;
            usedCodes.Add(code);
            map[tag.Id] = serviceType.Id;
        }

        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return map;
    }

    private async Task ImportCurrentInvoicesAsync(
        AppDbContext db,
        IReadOnlyDictionary<int, Guid> customerMap,
        IReadOnlyDictionary<int, Guid> serviceTypeMap,
        MigrationReport report,
        CancellationToken cancellationToken)
    {
        var existingLegacyIds = await db.SalesInvoices.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.CompanyId == options.TargetCompanyId && x.LegacySource == LegacySource && x.LegacyId != null)
            .Select(x => x.LegacyId!)
            .ToHashSetAsync(cancellationToken);
        var linesByInvoice = source.InvoiceLines.GroupBy(x => x.InvoiceNumber).ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        var tagsByInvoice = source.InvoiceTags.GroupBy(x => x.InvoiceNumber).ToDictionary(x => x.Key, x => x.Select(tag => tag.TagId).Distinct().ToList(), StringComparer.OrdinalIgnoreCase);
        var pending = 0;

        foreach (var legacy in source.Invoices.Where(x => x.DocumentTypeId != 4))
        {
            var legacyId = CurrentInvoiceLegacyId(legacy.Number);
            if (existingLegacyIds.Contains(legacyId)) continue;
            linesByInvoice.TryGetValue(legacy.Number, out var sourceLines);
            sourceLines ??= [];
            var invoice = CreateCurrentInvoice(legacy, sourceLines, customerMap);
            if (tagsByInvoice.TryGetValue(legacy.Number, out var tagIds))
            {
                foreach (var serviceTypeId in tagIds.Where(serviceTypeMap.ContainsKey).Select(id => serviceTypeMap[id]).Distinct())
                {
                    invoice.ServiceTypes.Add(new SalesInvoiceServiceType
                    {
                        CompanyId = options.TargetCompanyId,
                        ServiceTypeId = serviceTypeId
                    });
                    report.InsertedServiceTypeLinks++;
                }
            }

            db.SalesInvoices.Add(invoice);
            report.InsertedInvoices++;
            report.InsertedLines += invoice.Lines.Count;
            report.InsertedCashPayments += invoice.Payments.Count;
            pending++;
            if (pending >= 100)
            {
                await db.SaveChangesAsync(cancellationToken);
                db.ChangeTracker.Clear();
                pending = 0;
            }
        }

        if (pending > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }
    }

    private SalesInvoice CreateCurrentInvoice(
        LegacyInvoice legacy,
        IReadOnlyList<LegacyInvoiceLine> sourceLines,
        IReadOnlyDictionary<int, Guid> customerMap)
    {
        var type = legacy.DocumentTypeId == 3 ? SalesInvoiceType.Repair : SalesInvoiceType.Sale;
        var processingStatus = legacy.IsProcessed ? RepairStatus.Returned : RepairStatus.InProgress;
        var customerId = legacy.CustomerId.HasValue && customerMap.TryGetValue(legacy.CustomerId.Value, out var mappedId) ? mappedId : (Guid?)null;
        var invoice = new SalesInvoice
        {
            CompanyId = options.TargetCompanyId,
            Number = legacy.Number,
            Type = type,
            CustomerId = customerId,
            CustomerName = Clean(legacy.CustomerName) ?? (legacy.CustomerId.HasValue ? $"Khách hàng cũ #{legacy.CustomerId}" : "Khách hàng cũ"),
            CustomerPhone = NormalizePhone(legacy.CustomerPhone),
            CustomerEmail = Clean(legacy.CustomerEmail),
            CustomerAddress = Clean(legacy.CustomerAddress),
            InvoiceDateUtc = ToUtc(legacy.InvoiceDate),
            ExpectedReturnAtUtc = type == SalesInvoiceType.Repair && legacy.ReturnDate.HasValue ? ToUtc(legacy.ReturnDate.Value) : null,
            Status = InvoiceStatus.Paid,
            RepairStatus = processingStatus,
            Subtotal = sourceLines.Count > 0 ? sourceLines.Sum(x => x.LineTotal) : legacy.Subtotal,
            DiscountAmount = legacy.DiscountAmount,
            TaxAmount = legacy.TaxAmount,
            TotalAmount = legacy.TotalAmount,
            PaidAmount = legacy.TotalAmount,
            Notes = CombineNotes(legacy.Notes, legacy.InternalNotes),
            LegacySource = LegacySource,
            LegacyId = CurrentInvoiceLegacyId(legacy.Number)
        };

        foreach (var line in sourceLines)
        {
            invoice.Lines.Add(new SalesInvoiceLine
            {
                CompanyId = options.TargetCompanyId,
                Sku = Clean(line.Sku) ?? $"LEGACY-{line.Id}",
                ProductName = line.ProductName,
                Quantity = line.Quantity == 0 ? 1 : line.Quantity,
                UnitName = type == SalesInvoiceType.Repair ? "Dịch vụ" : "Cái",
                WarrantyPeriod = Clean(line.Warranty) ?? "Không xác định",
                UnitPrice = line.UnitPrice,
                DiscountAmount = line.DiscountAmount,
                LineTotal = line.LineTotal,
                Notes = Clean(line.Notes)
            });
        }
        if (invoice.Lines.Count == 0)
        {
            invoice.Lines.Add(new SalesInvoiceLine
            {
                CompanyId = options.TargetCompanyId,
                Sku = $"LEGACY-{legacy.Number}",
                ProductName = Clean(legacy.Notes) ?? "Nội dung hóa đơn cũ",
                Quantity = 1,
                UnitName = type == SalesInvoiceType.Repair ? "Dịch vụ" : "Cái",
                WarrantyPeriod = "Không xác định",
                UnitPrice = legacy.TotalAmount,
                LineTotal = legacy.TotalAmount,
                Notes = "Dòng đối soát được tạo vì hóa đơn nguồn không còn chi tiết."
            });
        }
        if (legacy.TotalAmount != 0)
        {
            invoice.Payments.Add(new SalesPayment
            {
                CompanyId = options.TargetCompanyId,
                Method = PaymentMethod.Cash,
                Amount = legacy.TotalAmount,
                PaidAtUtc = invoice.InvoiceDateUtc,
                IsScheduled = false,
                Notes = legacy.IsProcessed ? "Tiền mặt từ APP.MTL" : "Nguồn tiền mặt dự kiến từ APP.MTL; chỉ ghi nhận khi phiếu được xử lý"
            });
        }
        if (type == SalesInvoiceType.Repair)
        {
            invoice.Repairs.Add(new RepairOrder
            {
                CompanyId = options.TargetCompanyId,
                DeviceModel = Clean(legacy.Model) ?? sourceLines.FirstOrDefault()?.ProductName ?? "Thiết bị sửa chữa",
                SerialNumber = Clean(legacy.Serial),
                DeviceCondition = string.Empty,
                CustomerRequest = Clean(legacy.Notes) ?? string.Empty,
                EstimatedCost = legacy.TotalAmount,
                ExpectedReturnAtUtc = invoice.ExpectedReturnAtUtc,
                Status = processingStatus,
                Notes = "Dữ liệu chuyển từ APP.MTL"
            });
        }
        return invoice;
    }

    private async Task ImportArchiveInvoicesAsync(
        AppDbContext db,
        MigrationReport report,
        CancellationToken cancellationToken)
    {
        var existingLegacyIds = await db.SalesInvoices.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.CompanyId == options.TargetCompanyId && x.LegacySource == LegacySource && x.LegacyId != null)
            .Select(x => x.LegacyId!)
            .ToHashSetAsync(cancellationToken);
        var linesByInvoice = source.ArchiveInvoiceLines.GroupBy(x => x.InvoiceId).ToDictionary(x => x.Key, x => x.ToList());
        var targetCustomers = await db.Customers.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.CompanyId == options.TargetCompanyId && !x.IsDeleted)
            .Select(x => new { x.Id, x.FullName, x.Phone })
            .ToListAsync(cancellationToken);
        var uniqueCustomerNames = targetCustomers.GroupBy(x => NormalizeText(x.FullName))
            .Where(x => x.Count() == 1)
            .ToDictionary(x => x.Key, x => x.Single());
        var pending = 0;

        foreach (var legacy in source.ArchiveInvoices)
        {
            var legacyId = ArchiveInvoiceLegacyId(legacy.Id);
            if (existingLegacyIds.Contains(legacyId)) continue;
            linesByInvoice.TryGetValue(legacy.Id, out var sourceLines);
            sourceLines ??= [];
            uniqueCustomerNames.TryGetValue(NormalizeText(legacy.CustomerName), out var customer);
            var processingStatus = legacy.IsProcessed ? RepairStatus.Returned : RepairStatus.InProgress;
            var subtotal = sourceLines.Count > 0 ? sourceLines.Sum(x => x.LineTotal) : legacy.TotalAmount;
            var invoice = new SalesInvoice
            {
                CompanyId = options.TargetCompanyId,
                Number = ArchiveNumber(legacy.Id),
                Type = SalesInvoiceType.Sale,
                CustomerId = customer?.Id,
                CustomerName = legacy.CustomerName,
                CustomerPhone = customer?.Phone,
                InvoiceDateUtc = ToUtc(legacy.InvoiceDate),
                Status = InvoiceStatus.Paid,
                RepairStatus = processingStatus,
                Subtotal = subtotal,
                DiscountAmount = Math.Max(0, subtotal - legacy.TotalAmount),
                TotalAmount = legacy.TotalAmount,
                PaidAmount = legacy.TotalAmount,
                Notes = Clean(legacy.Notes),
                LegacySource = LegacySource,
                LegacyId = legacyId
            };
            foreach (var line in sourceLines)
            {
                invoice.Lines.Add(new SalesInvoiceLine
                {
                    CompanyId = options.TargetCompanyId,
                    Sku = $"ARCHIVE-{line.Id}",
                    ProductName = line.ProductName,
                    Quantity = line.Quantity == 0 ? 1 : line.Quantity,
                    UnitName = Clean(line.UnitName) ?? "Cái",
                    WarrantyPeriod = Clean(line.Warranty) ?? "Không xác định",
                    UnitPrice = line.UnitPrice,
                    LineTotal = line.LineTotal
                });
            }
            if (invoice.Lines.Count == 0)
            {
                invoice.Lines.Add(new SalesInvoiceLine
                {
                    CompanyId = options.TargetCompanyId,
                    Sku = $"ARCHIVE-{legacy.Id}",
                    ProductName = Clean(legacy.Notes) ?? "Nội dung hóa đơn lưu trữ",
                    Quantity = 1,
                    UnitName = "Cái",
                    WarrantyPeriod = "Không xác định",
                    UnitPrice = legacy.TotalAmount,
                    LineTotal = legacy.TotalAmount
                });
            }
            if (legacy.TotalAmount != 0)
            {
                invoice.Payments.Add(new SalesPayment
                {
                    CompanyId = options.TargetCompanyId,
                    Method = PaymentMethod.Cash,
                    Amount = legacy.TotalAmount,
                    PaidAtUtc = invoice.InvoiceDateUtc,
                    IsScheduled = false,
                    Notes = legacy.IsProcessed ? "Tiền mặt từ kho hóa đơn lưu trữ APP.MTL" : "Nguồn tiền mặt dự kiến từ APP.MTL; chỉ ghi nhận khi phiếu được xử lý"
                });
            }

            db.SalesInvoices.Add(invoice);
            report.InsertedInvoices++;
            report.InsertedLines += invoice.Lines.Count;
            report.InsertedCashPayments += invoice.Payments.Count;
            pending++;
            if (pending >= 100)
            {
                await db.SaveChangesAsync(cancellationToken);
                db.ChangeTracker.Clear();
                pending = 0;
            }
        }

        if (pending > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }
    }

    private async Task ReconcileLegacyCashPaymentsAsync(
        AppDbContext db,
        MigrationReport report,
        CancellationToken cancellationToken)
    {
        var invoices = await db.SalesInvoices.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.CompanyId == options.TargetCompanyId &&
                        !x.IsDeleted &&
                        x.LegacySource == LegacySource)
            .Select(x => new
            {
                x.Id,
                x.CompanyId,
                x.InvoiceDateUtc,
                x.TotalAmount,
                PaymentTotal = x.Payments
                    .Where(payment => !payment.IsDeleted)
                    .Sum(payment => (decimal?)payment.Amount) ?? 0
            })
            .ToListAsync(cancellationToken);

        foreach (var invoice in invoices)
        {
            var difference = invoice.TotalAmount - invoice.PaymentTotal;
            if (difference == 0) continue;

            db.SalesPayments.Add(new SalesPayment
            {
                CompanyId = invoice.CompanyId,
                SalesInvoiceId = invoice.Id,
                Method = PaymentMethod.Cash,
                Amount = difference,
                PaidAtUtc = invoice.InvoiceDateUtc,
                IsScheduled = false,
                Notes = "Điều chỉnh nguồn tiền mặt để đối soát tổng hóa đơn APP.MTL"
            });
            report.InsertedCashPayments++;
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }
    }

    private async Task ImportPurchasesAsync(AppDbContext db, MigrationReport report, CancellationToken cancellationToken)
    {
        if (source.Purchases.Count == 0) return;

        var suppliers = await db.Suppliers.IgnoreQueryFilters()
            .Where(x => x.CompanyId == options.TargetCompanyId && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        var suppliersByLegacy = suppliers.Where(x => x.LegacySource == LegacySource && x.LegacyId != null)
            .ToDictionary(x => x.LegacyId!, StringComparer.OrdinalIgnoreCase);
        var nextSupplierCode = NextSupplierNumber(suppliers.Select(x => x.Code));

        var unknownLegacyId = SupplierLegacyId(null);
        if (!suppliersByLegacy.TryGetValue(unknownLegacyId, out var unknownSupplier))
        {
            unknownSupplier = new Supplier
            {
                CompanyId = options.TargetCompanyId,
                Code = $"NCC{nextSupplierCode++:000000}",
                Name = "Không xác định (dữ liệu cũ)",
                Notes = "Nhà cung cấp đối soát cho phiếu nhập APP.MTL không còn thông tin nhà cung cấp.",
                IsActive = true,
                LegacySource = LegacySource,
                LegacyId = unknownLegacyId
            };
            db.Suppliers.Add(unknownSupplier);
            suppliersByLegacy[unknownLegacyId] = unknownSupplier;
            report.InsertedSuppliers++;
        }

        foreach (var legacy in source.Purchases.Where(x => x.SupplierId.HasValue).GroupBy(x => x.SupplierId!.Value).Select(x => x.First()))
        {
            var legacyId = SupplierLegacyId(legacy.SupplierId);
            if (suppliersByLegacy.ContainsKey(legacyId)) continue;
            var supplier = new Supplier
            {
                CompanyId = options.TargetCompanyId,
                Code = $"NCC{nextSupplierCode++:000000}",
                Name = Clean(legacy.SupplierName) ?? $"Nhà cung cấp cũ #{legacy.SupplierId}",
                Phone = NormalizePhone(legacy.SupplierPhone),
                Email = Clean(legacy.SupplierEmail),
                Address = Clean(legacy.SupplierAddress),
                TaxCode = Clean(legacy.SupplierTaxCode),
                Notes = "Dữ liệu chuyển từ APP.MTL",
                IsActive = true,
                LegacySource = LegacySource,
                LegacyId = legacyId
            };
            db.Suppliers.Add(supplier);
            suppliersByLegacy[legacyId] = supplier;
            report.InsertedSuppliers++;
        }
        await db.SaveChangesAsync(cancellationToken);

        var existingPurchaseLegacyIds = await db.PurchaseInvoices.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.CompanyId == options.TargetCompanyId && x.LegacySource == LegacySource && x.LegacyId != null)
            .Select(x => x.LegacyId!).ToHashSetAsync(cancellationToken);
        var sourceLines = source.PurchaseLines.GroupBy(x => x.InvoiceNumber)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        var targetProducts = await db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.CompanyId == options.TargetCompanyId && !x.IsDeleted)
            .Select(x => new { x.Id, x.Sku }).ToListAsync(cancellationToken);
        var productsBySku = targetProducts.Where(x => !string.IsNullOrWhiteSpace(x.Sku))
            .GroupBy(x => x.Sku, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() == 1)
            .ToDictionary(x => x.Key, x => x.Single().Id, StringComparer.OrdinalIgnoreCase);

        foreach (var legacy in source.Purchases)
        {
            var legacyId = PurchaseLegacyId(legacy.Number);
            if (existingPurchaseLegacyIds.Contains(legacyId)) continue;
            sourceLines.TryGetValue(legacy.Number, out var lines);
            lines ??= [];
            var supplier = suppliersByLegacy[SupplierLegacyId(legacy.SupplierId)];
            var paid = Math.Clamp(legacy.PaidAmount, 0, legacy.TotalAmount);
            var invoice = new PurchaseInvoice
            {
                CompanyId = options.TargetCompanyId,
                Number = legacy.Number,
                SupplierId = supplier.Id,
                Supplier = supplier,
                InvoiceDateUtc = ToUtc(legacy.InvoiceDate),
                ReceivedAtUtc = ToUtc(legacy.InvoiceDate),
                Status = PurchaseInvoiceStatus.Processed,
                Subtotal = legacy.TotalAmount + Math.Max(0, legacy.DiscountAmount),
                DiscountAmount = Math.Max(0, legacy.DiscountAmount),
                TotalAmount = legacy.TotalAmount,
                PaidAmount = paid,
                SupplierInvoiceNumber = Clean(legacy.SupplierInvoiceNumber),
                WarehouseReceiptNumber = legacy.Number,
                Notes = CombineNotes(legacy.Notes, legacy.InternalNotes),
                InventoryPosted = false,
                LegacySource = LegacySource,
                LegacyId = legacyId
            };
            foreach (var line in lines)
            {
                Guid? productId = null;
                if (Clean(line.Sku) is { } sku && productsBySku.TryGetValue(sku, out var mappedProductId)) productId = mappedProductId;
                invoice.Lines.Add(new PurchaseInvoiceLine
                {
                    CompanyId = options.TargetCompanyId,
                    ProductId = productId,
                    Sku = Clean(line.Sku) ?? $"LEGACY-PURCHASE-{line.Id}",
                    ProductName = line.ProductName,
                    Quantity = line.Quantity == 0 ? 1 : line.Quantity,
                    ReceivedQuantity = line.Quantity == 0 ? 1 : line.Quantity,
                    UnitName = Clean(line.UnitName) ?? "Cái",
                    UnitCost = line.UnitCost,
                    DiscountAmount = line.DiscountAmount,
                    TaxAmount = 0,
                    LineTotal = line.LineTotal,
                    SerialNumbersJson = JsonSerializer.Serialize(Array.Empty<string>()),
                    Notes = Clean(line.Notes)
                });
            }
            if (invoice.Lines.Count == 0)
            {
                invoice.Lines.Add(new PurchaseInvoiceLine
                {
                    CompanyId = options.TargetCompanyId,
                    Sku = $"LEGACY-PURCHASE-{legacy.Id}",
                    ProductName = Clean(legacy.Notes) ?? "Nội dung phiếu nhập cũ",
                    Quantity = 1,
                    ReceivedQuantity = 1,
                    UnitName = "Cái",
                    UnitCost = legacy.TotalAmount,
                    LineTotal = legacy.TotalAmount,
                    SerialNumbersJson = JsonSerializer.Serialize(Array.Empty<string>()),
                    Notes = "Dòng đối soát được tạo vì phiếu nhập nguồn không còn chi tiết."
                });
            }
            if (paid > 0)
            {
                invoice.Payments.Add(new PurchasePayment
                {
                    CompanyId = options.TargetCompanyId,
                    Method = PaymentMethod.Cash,
                    Amount = paid,
                    PaidAtUtc = invoice.InvoiceDateUtc,
                    Notes = "Tiền mặt từ phiếu nhập APP.MTL"
                });
            }
            db.PurchaseInvoices.Add(invoice);
            report.InsertedPurchases++;
            report.InsertedPurchaseLines += invoice.Lines.Count;
            report.InsertedPurchasePayments += invoice.Payments.Count;
        }
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    private async Task RecalculateCustomerStatisticsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Customers" AS c
            SET "TotalSpent" = COALESCE((
                    SELECT SUM(i."TotalAmount")
                    FROM "SalesInvoices" AS i
                    WHERE i."CompanyId" = c."CompanyId"
                      AND i."CustomerId" = c."Id"
                      AND i."RepairStatus" = 'Returned'
                      AND i."IsDeleted" = false
                ), 0),
                "LastPurchaseAtUtc" = (
                    SELECT MAX(i."InvoiceDateUtc")
                    FROM "SalesInvoices" AS i
                    WHERE i."CompanyId" = c."CompanyId"
                      AND i."CustomerId" = c."Id"
                      AND i."RepairStatus" = 'Returned'
                      AND i."IsDeleted" = false
                )
            WHERE c."CompanyId" = {options.TargetCompanyId} AND c."IsDeleted" = false;
            """, cancellationToken);
    }

    private DateTimeOffset ToUtc(DateTime localDateTime)
    {
        var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, _businessTimeZone.GetUtcOffset(unspecified)).ToUniversalTime();
    }

    private static string CustomerLegacyId(int id) => $"customer:{id}";
    private static string SupplierLegacyId(int? id) => id.HasValue ? $"supplier:{id.Value}" : "supplier:unknown";
    private static string CurrentInvoiceLegacyId(string number) => $"invoice:{number}";
    private static string PurchaseLegacyId(string number) => $"purchase:{number}";
    private static string ArchiveInvoiceLegacyId(int id) => $"archive:{id}";
    private static string ArchiveNumber(int id) => $"OLD-{id:000000}";
    private static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    private static int NextCustomerNumber(IEnumerable<string> codes) => codes
        .Select(code => code.StartsWith("KH", StringComparison.OrdinalIgnoreCase) && int.TryParse(code.AsSpan(2), out var number) ? number : 0)
        .DefaultIfEmpty(0)
        .Max() + 1;

    private static int NextSupplierNumber(IEnumerable<string> codes) => codes
        .Select(code => code.StartsWith("NCC", StringComparison.OrdinalIgnoreCase) && int.TryParse(code.AsSpan(3), out var number) ? number : 0)
        .DefaultIfEmpty(0)
        .Max() + 1;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? CombineNotes(string? visible, string? internalNotes)
    {
        var first = Clean(visible);
        var second = Clean(internalNotes);
        if (second is null) return first;
        return first is null ? $"Ghi chú nội bộ hệ thống cũ: {second}" : $"{first}\nGhi chú nội bộ hệ thống cũ: {second}";
    }

    private static string MapLegacyTagName(string value) => NormalizeText(value) switch
    {
        "KEYBOARD" => "Phím",
        "WEBCAME" => "Webcam",
        _ => value.Trim()
    };

    private static string Slug(string value)
    {
        var normalized = RemoveDiacritics(value).ToUpperInvariant().Replace('Đ', 'D');
        var builder = new StringBuilder();
        var previousDash = false;
        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousDash = false;
            }
            else if (!previousDash && builder.Length > 0)
            {
                builder.Append('-');
                previousDash = true;
            }
        }
        return builder.ToString().Trim('-') is { Length: > 0 } code ? code[..Math.Min(code.Length, 70)] : "DICH-VU-CU";
    }

    private static string NormalizeText(string value) => RemoveDiacritics(value).Replace('Đ', 'D').Replace('đ', 'd').Trim().ToUpperInvariant();
    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        return new string(normalized.Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC);
    }

    private sealed class MigrationCurrentUser(Guid companyId) : ICurrentUser
    {
        public Guid? UserId => null;
        public Guid? CompanyId => companyId;
        public string? Email => "migration@app.mtl";
        public bool IsAuthenticated => true;
    }
}
