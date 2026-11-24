using System.Text.Encodings.Web;
using System.Text.Json;
using MTL.LegacyMigration;

var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    var arguments = ParseArguments(args);
    var apply = arguments.ContainsKey("apply");
    var includeArchive = !arguments.ContainsKey("no-archive");
    var sourceCompanyId = int.TryParse(Get(arguments, "source-company-id", "MTL_SOURCE_COMPANY_ID") ?? "1", out var parsedSourceCompanyId)
        ? parsedSourceCompanyId
        : throw new ArgumentException("source-company-id không hợp lệ.");
    var targetCompanyText = Get(arguments, "target-company-id", "MTL_TARGET_COMPANY_ID")
        ?? throw new ArgumentException("Thiếu --target-company-id hoặc biến MTL_TARGET_COMPANY_ID.");
    if (!Guid.TryParse(targetCompanyText, out var targetCompanyId))
        throw new ArgumentException("target-company-id không hợp lệ.");

    var sourceConnection = Environment.GetEnvironmentVariable("MTL_LEGACY_CONNECTION_STRING");
    if (string.IsNullOrWhiteSpace(sourceConnection))
    {
        var sourceConfig = Get(arguments, "source-config", "MTL_LEGACY_CONFIG")
            ?? throw new ArgumentException("Thiếu MTL_LEGACY_CONNECTION_STRING hoặc --source-config.");
        sourceConnection = ReadLegacyConnectionString(sourceConfig);
    }
    var targetConnection = Environment.GetEnvironmentVariable("MTL_TARGET_CONNECTION_STRING")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__Database")
        ?? throw new ArgumentException("Thiếu biến MTL_TARGET_CONNECTION_STRING.");

    var defaultReport = Path.Combine(
        Directory.GetCurrentDirectory(),
        "artifacts",
        "migration",
        $"app-mtl-{(apply ? "apply" : "dry-run")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
    var reportPath = Path.GetFullPath(Get(arguments, "report", "MTL_MIGRATION_REPORT") ?? defaultReport);
    var options = new MigrationOptions(
        apply,
        includeArchive,
        sourceCompanyId,
        targetCompanyId,
        sourceConnection,
        targetConnection,
        reportPath);

    Console.WriteLine($"Đang đọc APP.MTL (company {sourceCompanyId}) ở chế độ chỉ đọc...");
    var dataset = await new LegacyReader(sourceConnection, sourceCompanyId).ReadAsync(includeArchive, cancellation.Token);
    Console.WriteLine($"Đã đọc {dataset.Customers.Count:N0} khách hàng, {dataset.Invoices.Count:N0} chứng từ bán hàng, {dataset.Purchases.Count:N0} phiếu nhập và {dataset.ArchiveInvoices.Count:N0} hóa đơn lưu trữ.");
    var report = await new MigrationRunner(options, dataset).RunAsync(cancellation.Token);

    Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
    var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    });
    await File.WriteAllTextAsync(reportPath, json, cancellation.Token);

    Console.WriteLine(apply ? "Migration đã hoàn tất." : "Dry-run hoàn tất; chưa ghi dữ liệu.");
    Console.WriteLine($"Khách hàng nguồn: {report.SourceCustomers:N0} ({report.SourceCustomersWithCitizenId:N0} có CCCD)");
    Console.WriteLine($"Hóa đơn dự kiến nhập: {report.PlannedNewInvoices:N0}; tổng hiện hành: {report.CurrentInvoiceTotal:N0} đ; tổng lưu trữ: {report.ArchiveInvoiceTotal:N0} đ");
    Console.WriteLine($"Báo giá loại trừ: {report.ExcludedQuotes:N0}; trùng mã đích: {report.NumberCollisions:N0}");
    Console.WriteLine($"Phiếu nhập dự kiến nhập: {report.PlannedNewPurchases:N0}; tổng nguồn: {report.PurchaseInvoiceTotal:N0} đ");
    if (apply)
        Console.WriteLine($"Đã thêm {report.InsertedCustomers:N0} khách hàng, {report.InsertedInvoices:N0} hóa đơn bán, {report.InsertedLines:N0} dòng bán; {report.InsertedSuppliers:N0} nhà cung cấp, {report.InsertedPurchases:N0} phiếu nhập, {report.InsertedPurchaseLines:N0} dòng nhập.");
    Console.WriteLine($"Báo cáo: {reportPath}");
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Đã hủy migration.");
    Environment.ExitCode = 2;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Migration thất bại: {exception.Message}");
    Environment.ExitCode = 1;
}

static Dictionary<string, string?> ParseArguments(string[] args)
{
    var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    for (var index = 0; index < args.Length; index++)
    {
        var token = args[index];
        if (!token.StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"Tham số không hợp lệ: {token}");
        var key = token[2..];
        if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            result[key] = args[++index];
        else
            result[key] = null;
    }
    return result;
}

static string? Get(IReadOnlyDictionary<string, string?> arguments, string argumentName, string environmentName) =>
    arguments.TryGetValue(argumentName, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value
        : Environment.GetEnvironmentVariable(environmentName);

static string ReadLegacyConnectionString(string configPath)
{
    using var document = JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(configPath)));
    if (!document.RootElement.TryGetProperty("ConnectionStrings", out var connections) ||
        !connections.TryGetProperty("MTLDB", out var connection) ||
        string.IsNullOrWhiteSpace(connection.GetString()))
        throw new InvalidDataException("Không tìm thấy ConnectionStrings:MTLDB trong cấu hình APP.MTL.");
    return connection.GetString()!;
}
