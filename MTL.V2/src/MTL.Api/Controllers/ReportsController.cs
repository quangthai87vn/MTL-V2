using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MTL.Domain.Identity;
using MTL.Domain.Sales;
using MTL.Infrastructure.Persistence;

namespace MTL.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = Permissions.ReportsRead)]
public sealed class ReportsController(AppDbContext db) : ControllerBase
{
    [HttpGet("filter-options")]
    public async Task<ActionResult<RevenueReportFilterOptions>> GetFilterOptions(CancellationToken cancellationToken)
    {
        var creatorIds = await db.SalesInvoices.AsNoTracking()
            .Where(invoice => invoice.CreatedByUserId.HasValue)
            .Select(invoice => invoice.CreatedByUserId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        var creators = await db.Users.AsNoTracking()
            .Where(user => creatorIds.Contains(user.Id))
            .OrderBy(user => user.FullName)
            .Select(user => new RevenueReportCreatorOption(user.Id, user.FullName))
            .ToListAsync(cancellationToken);
        var accounts = await db.PaymentAccounts.AsNoTracking()
            .OrderByDescending(account => account.IsActive)
            .ThenBy(account => account.DisplayOrder)
            .ThenBy(account => account.BankName)
            .ThenBy(account => account.Name)
            .Select(account => new RevenueReportPaymentAccountOption(
                account.Id,
                account.Name,
                account.BankName,
                account.AccountNumber,
                account.AccountHolder,
                account.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(new RevenueReportFilterOptions(creators, accounts));
    }

    [HttpGet("revenue")]
    public async Task<ActionResult<RevenueReportResponse>> GetRevenue(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? groupBy,
        [FromQuery] SalesInvoiceType? type,
        [FromQuery] string? paymentSource,
        [FromQuery] Guid? paymentAccountId,
        [FromQuery] Guid? createdByUserId,
        CancellationToken cancellationToken)
    {
        var timeZone = GetBusinessTimeZone();
        var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
        var today = DateOnly.FromDateTime(localNow.DateTime);
        var defaultFrom = new DateOnly(today.Year, today.Month, 1);

        if (!TryParseDate(from, defaultFrom, out var fromDate) || !TryParseDate(to, today, out var toDate))
            return BadRequest(new { message = "Ngày báo cáo phải theo định dạng yyyy-MM-dd." });
        if (toDate < fromDate)
            return BadRequest(new { message = "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu." });

        var grouping = ParseGrouping(groupBy);
        if (grouping is null)
            return BadRequest(new { message = "Cấp thời gian chỉ nhận day, month hoặc year." });

        var source = ParsePaymentSource(paymentSource);
        if (source is null)
            return BadRequest(new { message = "Nguồn tiền chỉ nhận all, cash, bank hoặc mixed." });

        var periods = BuildPeriods(fromDate, toDate, grouping.Value);
        if (periods.Count > 400)
            return BadRequest(new { message = "Khoảng báo cáo có quá nhiều điểm. Hãy chuyển sang nhóm theo tháng hoặc năm." });

        var fromUtc = ToUtcStart(fromDate, timeZone);
        var toUtcExclusive = ToUtcStart(toDate.AddDays(1), timeZone);
        var dayCount = toDate.DayNumber - fromDate.DayNumber + 1;
        var previousToDate = fromDate.AddDays(-1);
        var previousFromDate = previousToDate.AddDays(1 - dayCount);
        var previousFromUtc = ToUtcStart(previousFromDate, timeZone);
        var previousToUtcExclusive = ToUtcStart(previousToDate.AddDays(1), timeZone);

        var invoiceBase = ApplyInvoiceFilters(
            db.SalesInvoices.AsNoTracking().Where(invoice => invoice.RepairStatus == RepairStatus.Returned),
            type,
            source.Value,
            paymentAccountId,
            createdByUserId);

        var currentInvoices = await invoiceBase
            .Where(invoice => invoice.InvoiceDateUtc >= fromUtc && invoice.InvoiceDateUtc < toUtcExclusive)
            .OrderByDescending(invoice => invoice.InvoiceDateUtc)
            .Select(invoice => new ReportInvoiceRow(
                invoice.Id,
                invoice.Number,
                invoice.Type,
                invoice.CustomerName,
                invoice.InvoiceDateUtc,
                invoice.TotalAmount,
                invoice.DiscountAmount,
                invoice.CreatedByUserId,
                invoice.Payments.Where(payment => !payment.IsScheduled && payment.Amount > 0 && payment.Method == PaymentMethod.Cash)
                    .Sum(payment => (decimal?)payment.Amount) ?? 0,
                invoice.Payments.Where(payment => !payment.IsScheduled && payment.Amount > 0 && payment.Method == PaymentMethod.BankTransfer)
                    .Sum(payment => (decimal?)payment.Amount) ?? 0))
            .ToListAsync(cancellationToken);

        var previousInvoiceRevenue = await invoiceBase
            .Where(invoice => invoice.InvoiceDateUtc >= previousFromUtc && invoice.InvoiceDateUtc < previousToUtcExclusive)
            .SumAsync(invoice => (decimal?)invoice.TotalAmount, cancellationToken) ?? 0;

        var paymentBase = ApplyPaymentFilters(
            db.SalesPayments.AsNoTracking().Where(payment =>
                !payment.IsScheduled &&
                payment.Amount > 0 &&
                payment.SalesInvoice.RepairStatus == RepairStatus.Returned),
            type,
            source.Value,
            paymentAccountId,
            createdByUserId);
        var scheduledBase = ApplyPaymentFilters(
            db.SalesPayments.AsNoTracking().Where(payment =>
                payment.IsScheduled &&
                payment.Amount > 0 &&
                payment.SalesInvoice.RepairStatus == RepairStatus.Returned),
            type,
            source.Value,
            paymentAccountId,
            createdByUserId);

        var currentPayments = await paymentBase
            .Where(payment => payment.PaidAtUtc >= fromUtc && payment.PaidAtUtc < toUtcExclusive)
            .Select(payment => new ReportPaymentRow(
                payment.SalesInvoiceId,
                payment.SalesInvoice.Number,
                payment.Method,
                payment.PaymentAccountId,
                payment.PaymentAccount == null ? null : payment.PaymentAccount.Name,
                payment.PaymentAccount == null ? null : payment.PaymentAccount.BankName,
                payment.PaymentAccount == null ? null : payment.PaymentAccount.AccountNumber,
                payment.PaymentAccount == null ? null : payment.PaymentAccount.AccountHolder,
                payment.Amount,
                payment.PaidAtUtc,
                false))
            .ToListAsync(cancellationToken);
        var scheduledPayments = source == RevenuePaymentSource.Cash
            ? []
            : await scheduledBase
                .Where(payment => payment.PaidAtUtc >= fromUtc && payment.PaidAtUtc < toUtcExclusive)
                .Select(payment => new ReportPaymentRow(
                    payment.SalesInvoiceId,
                    payment.SalesInvoice.Number,
                    payment.Method,
                    payment.PaymentAccountId,
                    payment.PaymentAccount == null ? null : payment.PaymentAccount.Name,
                    payment.PaymentAccount == null ? null : payment.PaymentAccount.BankName,
                    payment.PaymentAccount == null ? null : payment.PaymentAccount.AccountNumber,
                    payment.PaymentAccount == null ? null : payment.PaymentAccount.AccountHolder,
                    payment.Amount,
                    payment.PaidAtUtc,
                    true))
                .ToListAsync(cancellationToken);

        var previousCollected = await paymentBase
            .Where(payment => payment.PaidAtUtc >= previousFromUtc && payment.PaidAtUtc < previousToUtcExclusive)
            .SumAsync(payment => (decimal?)payment.Amount, cancellationToken) ?? 0;

        var creatorIds = currentInvoices
            .Where(invoice => invoice.CreatedByUserId.HasValue)
            .Select(invoice => invoice.CreatedByUserId!.Value)
            .Distinct()
            .ToList();
        var creatorNames = creatorIds.Count == 0
            ? []
            : await db.Users.AsNoTracking()
                .Where(user => creatorIds.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);

        var bucketMap = periods.ToDictionary(
            period => period.BucketStart,
            period => new RevenueBucketAccumulator(period.Start, period.End, period.Label));
        foreach (var invoice in currentInvoices)
        {
            var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(invoice.InvoiceDateUtc, timeZone).DateTime);
            bucketMap[GetPeriodStart(localDate, grouping.Value)].InvoiceRevenue += invoice.TotalAmount;
            bucketMap[GetPeriodStart(localDate, grouping.Value)].InvoiceCount++;
        }
        foreach (var payment in currentPayments)
        {
            var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(payment.PaidAtUtc, timeZone).DateTime);
            var bucket = bucketMap[GetPeriodStart(localDate, grouping.Value)];
            bucket.CollectedAmount += payment.Amount;
            if (payment.Method == PaymentMethod.Cash) bucket.CashAmount += payment.Amount;
            if (payment.Method == PaymentMethod.BankTransfer) bucket.BankAmount += payment.Amount;
        }
        foreach (var payment in scheduledPayments)
        {
            var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(payment.PaidAtUtc, timeZone).DateTime);
            bucketMap[GetPeriodStart(localDate, grouping.Value)].ScheduledAmount += payment.Amount;
        }

        var timeline = periods.Select(period => bucketMap[period.Start].ToItem()).ToList();
        var invoiceRevenue = currentInvoices.Sum(invoice => invoice.TotalAmount);
        var collectedAmount = currentPayments.Sum(payment => payment.Amount);
        var scheduledAmount = scheduledPayments.Sum(payment => payment.Amount);
        var outstandingAmount = currentInvoices.Sum(invoice => Math.Max(0, invoice.TotalAmount - invoice.CashReceived - invoice.BankReceived));
        var invoiceCount = currentInvoices.Count;
        var summary = new RevenueReportSummary(
            invoiceRevenue,
            collectedAmount,
            scheduledAmount,
            outstandingAmount,
            invoiceCount,
            invoiceCount == 0 ? 0 : invoiceRevenue / invoiceCount,
            previousInvoiceRevenue,
            previousCollected,
            PercentChange(invoiceRevenue, previousInvoiceRevenue),
            PercentChange(collectedAmount, previousCollected));

        var actualTotal = currentPayments.Sum(payment => payment.Amount);
        var paymentSources = new[]
        {
            BuildSource("cash", "Tiền mặt", currentPayments.Where(payment => payment.Method == PaymentMethod.Cash), actualTotal),
            BuildSource("bank", "Chuyển khoản", currentPayments.Where(payment => payment.Method == PaymentMethod.BankTransfer), actualTotal)
        };

        var banking = BuildBanking(currentPayments, scheduledPayments);
        var invoices = currentInvoices.Take(20).Select(invoice => new RevenueReportInvoiceItem(
            invoice.Id,
            invoice.Number,
            invoice.Type,
            invoice.CustomerName,
            invoice.InvoiceDateUtc,
            invoice.TotalAmount,
            invoice.CashReceived + invoice.BankReceived,
            Math.Max(0, invoice.TotalAmount - invoice.CashReceived - invoice.BankReceived),
            PaymentLabel(invoice.CashReceived, invoice.BankReceived),
            invoice.CreatedByUserId.HasValue && creatorNames.TryGetValue(invoice.CreatedByUserId.Value, out var creatorName)
                ? creatorName
                : "Dữ liệu cũ"))
            .ToList();

        return Ok(new RevenueReportResponse(
            fromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            toDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            grouping.Value.ToString(),
            summary,
            timeline,
            paymentSources,
            banking,
            invoices,
            DateTimeOffset.UtcNow));
    }

    private static IQueryable<SalesInvoice> ApplyInvoiceFilters(
        IQueryable<SalesInvoice> query,
        SalesInvoiceType? type,
        RevenuePaymentSource source,
        Guid? paymentAccountId,
        Guid? createdByUserId)
    {
        if (type.HasValue) query = query.Where(invoice => invoice.Type == type.Value);
        if (createdByUserId.HasValue) query = query.Where(invoice => invoice.CreatedByUserId == createdByUserId.Value);
        if (paymentAccountId.HasValue)
            query = query.Where(invoice => invoice.Payments.Any(payment =>
                !payment.IsScheduled &&
                payment.Amount > 0 &&
                payment.Method == PaymentMethod.BankTransfer &&
                payment.PaymentAccountId == paymentAccountId.Value));
        query = source switch
        {
            RevenuePaymentSource.Cash => query.Where(invoice => invoice.Payments.Any(payment => !payment.IsScheduled && payment.Amount > 0 && payment.Method == PaymentMethod.Cash)),
            RevenuePaymentSource.Bank => query.Where(invoice => invoice.Payments.Any(payment => !payment.IsScheduled && payment.Amount > 0 && payment.Method == PaymentMethod.BankTransfer)),
            RevenuePaymentSource.Mixed => query.Where(invoice =>
                invoice.Payments.Any(payment => !payment.IsScheduled && payment.Amount > 0 && payment.Method == PaymentMethod.Cash) &&
                invoice.Payments.Any(payment => !payment.IsScheduled && payment.Amount > 0 && payment.Method == PaymentMethod.BankTransfer)),
            _ => query
        };
        return query;
    }

    private static IQueryable<SalesPayment> ApplyPaymentFilters(
        IQueryable<SalesPayment> query,
        SalesInvoiceType? type,
        RevenuePaymentSource source,
        Guid? paymentAccountId,
        Guid? createdByUserId)
    {
        if (type.HasValue) query = query.Where(payment => payment.SalesInvoice.Type == type.Value);
        if (createdByUserId.HasValue) query = query.Where(payment => payment.SalesInvoice.CreatedByUserId == createdByUserId.Value);
        if (paymentAccountId.HasValue) query = query.Where(payment => payment.PaymentAccountId == paymentAccountId.Value);
        query = source switch
        {
            RevenuePaymentSource.Cash => query.Where(payment => payment.Method == PaymentMethod.Cash),
            RevenuePaymentSource.Bank => query.Where(payment => payment.Method == PaymentMethod.BankTransfer),
            RevenuePaymentSource.Mixed => query.Where(payment =>
                payment.SalesInvoice.Payments.Any(item => !item.IsScheduled && item.Amount > 0 && item.Method == PaymentMethod.Cash) &&
                payment.SalesInvoice.Payments.Any(item => !item.IsScheduled && item.Amount > 0 && item.Method == PaymentMethod.BankTransfer)),
            _ => query
        };
        return query;
    }

    private static IReadOnlyList<RevenueReportBankingItem> BuildBanking(
        IReadOnlyList<ReportPaymentRow> actualPayments,
        IReadOnlyList<ReportPaymentRow> scheduledPayments)
    {
        var actualBanking = actualPayments.Where(payment => payment.Method == PaymentMethod.BankTransfer).ToList();
        var total = actualBanking.Sum(payment => payment.Amount);
        return actualBanking.Concat(scheduledPayments.Where(payment => payment.Method == PaymentMethod.BankTransfer))
            .GroupBy(payment => payment.PaymentAccountId?.ToString() ?? $"legacy:{payment.BankName}:{payment.AccountNumber}")
            .Select(group =>
            {
                var sample = group.First();
                var actual = group.Where(payment => !payment.IsScheduled).Sum(payment => payment.Amount);
                var scheduled = group.Where(payment => payment.IsScheduled).Sum(payment => payment.Amount);
                return new RevenueReportBankingItem(
                    sample.PaymentAccountId,
                    sample.PaymentAccountName ?? "Tài khoản chưa xác định",
                    sample.BankName ?? "Banking",
                    sample.AccountNumber,
                    sample.AccountHolder,
                    actual,
                    scheduled,
                    group.Count(payment => !payment.IsScheduled),
                    total == 0 ? 0 : actual / total * 100);
            })
            .OrderByDescending(item => item.ActualAmount)
            .ThenByDescending(item => item.ScheduledAmount)
            .ToList();
    }

    private static RevenueReportPaymentSourceItem BuildSource(
        string key,
        string label,
        IEnumerable<ReportPaymentRow> payments,
        decimal total)
    {
        var rows = payments.ToList();
        var amount = rows.Sum(payment => payment.Amount);
        return new RevenueReportPaymentSourceItem(key, label, amount, rows.Count, total == 0 ? 0 : amount / total * 100);
    }

    private static decimal? PercentChange(decimal current, decimal previous) => previous == 0
        ? current == 0 ? 0 : null
        : (current - previous) / previous * 100;

    private static string PaymentLabel(decimal cash, decimal bank) => cash > 0 && bank > 0
        ? "Tiền mặt + chuyển khoản"
        : cash > 0 ? "Tiền mặt" : bank > 0 ? "Chuyển khoản" : "Chưa ghi nhận";

    private static RevenuePaymentSource? ParsePaymentSource(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" or "all" => RevenuePaymentSource.All,
        "cash" => RevenuePaymentSource.Cash,
        "bank" => RevenuePaymentSource.Bank,
        "mixed" => RevenuePaymentSource.Mixed,
        _ => null
    };

    private static RevenueReportGrouping? ParseGrouping(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" or "day" => RevenueReportGrouping.Day,
        "month" => RevenueReportGrouping.Month,
        "year" => RevenueReportGrouping.Year,
        _ => null
    };

    private static bool TryParseDate(string? value, DateOnly fallback, out DateOnly date)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            date = fallback;
            return true;
        }
        return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static DateTimeOffset ToUtcStart(DateOnly date, TimeZoneInfo timeZone)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }

    private static TimeZoneInfo GetBusinessTimeZone() => TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    private static List<ReportPeriod> BuildPeriods(DateOnly from, DateOnly to, RevenueReportGrouping grouping)
    {
        var periods = new List<ReportPeriod>();
        var cursor = GetPeriodStart(from, grouping);
        while (cursor <= to)
        {
            var next = grouping switch
            {
                RevenueReportGrouping.Day => cursor.AddDays(1),
                RevenueReportGrouping.Month => cursor.AddMonths(1),
                _ => cursor.AddYears(1)
            };
            var start = DateOnly.FromDayNumber(Math.Max(cursor.DayNumber, from.DayNumber));
            var end = DateOnly.FromDayNumber(Math.Min(next.AddDays(-1).DayNumber, to.DayNumber));
            periods.Add(new ReportPeriod(cursor, start, end, PeriodLabel(cursor, grouping)));
            cursor = next;
        }
        return periods;
    }

    private static DateOnly GetPeriodStart(DateOnly date, RevenueReportGrouping grouping) => grouping switch
    {
        RevenueReportGrouping.Day => date,
        RevenueReportGrouping.Month => new DateOnly(date.Year, date.Month, 1),
        _ => new DateOnly(date.Year, 1, 1)
    };

    private static string PeriodLabel(DateOnly date, RevenueReportGrouping grouping) => grouping switch
    {
        RevenueReportGrouping.Day => date.ToString("dd/MM", CultureInfo.InvariantCulture),
        RevenueReportGrouping.Month => date.ToString("MM/yyyy", CultureInfo.InvariantCulture),
        _ => date.ToString("yyyy", CultureInfo.InvariantCulture)
    };

    private enum RevenuePaymentSource { All, Cash, Bank, Mixed }
    private enum RevenueReportGrouping { Day, Month, Year }
    private sealed record ReportPeriod(DateOnly BucketStart, DateOnly Start, DateOnly End, string Label);
    private sealed record ReportInvoiceRow(Guid Id, string Number, SalesInvoiceType Type, string CustomerName, DateTimeOffset InvoiceDateUtc, decimal TotalAmount, decimal DiscountAmount, Guid? CreatedByUserId, decimal CashReceived, decimal BankReceived);
    private sealed record ReportPaymentRow(Guid SalesInvoiceId, string InvoiceNumber, PaymentMethod Method, Guid? PaymentAccountId, string? PaymentAccountName, string? BankName, string? AccountNumber, string? AccountHolder, decimal Amount, DateTimeOffset PaidAtUtc, bool IsScheduled);

    private sealed class RevenueBucketAccumulator(DateOnly start, DateOnly end, string label)
    {
        public decimal InvoiceRevenue { get; set; }
        public decimal CollectedAmount { get; set; }
        public decimal CashAmount { get; set; }
        public decimal BankAmount { get; set; }
        public decimal ScheduledAmount { get; set; }
        public int InvoiceCount { get; set; }

        public RevenueReportTimelineItem ToItem() => new(
            label,
            start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            InvoiceRevenue,
            CollectedAmount,
            CashAmount,
            BankAmount,
            ScheduledAmount,
            InvoiceCount);
    }
}

public sealed record RevenueReportFilterOptions(
    IReadOnlyList<RevenueReportCreatorOption> Creators,
    IReadOnlyList<RevenueReportPaymentAccountOption> PaymentAccounts);
public sealed record RevenueReportCreatorOption(Guid Id, string Name);
public sealed record RevenueReportPaymentAccountOption(Guid Id, string Name, string BankName, string AccountNumber, string AccountHolder, bool IsActive);
public sealed record RevenueReportResponse(
    string From,
    string To,
    string GroupBy,
    RevenueReportSummary Summary,
    IReadOnlyList<RevenueReportTimelineItem> Timeline,
    IReadOnlyList<RevenueReportPaymentSourceItem> PaymentSources,
    IReadOnlyList<RevenueReportBankingItem> Banking,
    IReadOnlyList<RevenueReportInvoiceItem> Invoices,
    DateTimeOffset GeneratedAtUtc);
public sealed record RevenueReportSummary(
    decimal InvoiceRevenue,
    decimal CollectedAmount,
    decimal ScheduledAmount,
    decimal OutstandingAmount,
    int InvoiceCount,
    decimal AverageInvoiceValue,
    decimal PreviousInvoiceRevenue,
    decimal PreviousCollectedAmount,
    decimal? RevenueChangePercent,
    decimal? CollectedChangePercent);
public sealed record RevenueReportTimelineItem(
    string Label,
    string From,
    string To,
    decimal InvoiceRevenue,
    decimal CollectedAmount,
    decimal CashAmount,
    decimal BankAmount,
    decimal ScheduledAmount,
    int InvoiceCount);
public sealed record RevenueReportPaymentSourceItem(string Key, string Label, decimal Amount, int TransactionCount, decimal Share);
public sealed record RevenueReportBankingItem(Guid? PaymentAccountId, string Name, string BankName, string? AccountNumber, string? AccountHolder, decimal ActualAmount, decimal ScheduledAmount, int TransactionCount, decimal Share);
public sealed record RevenueReportInvoiceItem(Guid Id, string Number, SalesInvoiceType Type, string CustomerName, DateTimeOffset InvoiceDateUtc, decimal TotalAmount, decimal CollectedAmount, decimal OutstandingAmount, string PaymentSource, string CreatedByName);
