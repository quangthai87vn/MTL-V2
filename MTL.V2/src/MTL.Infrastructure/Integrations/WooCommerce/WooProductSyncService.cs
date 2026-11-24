using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MTL.Domain.Catalog;
using MTL.Domain.Integrations;
using MTL.Infrastructure.Identity;
using MTL.Infrastructure.Persistence;
using MTL.Infrastructure.Security;

namespace MTL.Infrastructure.Integrations.WooCommerce;

public sealed record WooSyncResult(int Processed, int Created, int Updated, IReadOnlyList<string> Errors);

public sealed class WooProductRecoveryRequiredException(string message) : InvalidOperationException(message);

public sealed class WooCommerceRequestException(
    string message,
    HttpStatusCode statusCode,
    string? errorCode = null)
    : InvalidOperationException(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string? ErrorCode { get; } = errorCode;
}

public interface IWooProductSyncService
{
    Task<WooSyncResult> PullAllAsync(CancellationToken cancellationToken = default);
    Task PushAsync(Guid productId, CancellationToken cancellationToken = default);
    Task PushAsync(Product product, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid productId, bool force = false, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}

public sealed class WooProductSyncService(
    HttpClient httpClient,
    AppDbContext db,
    ISecretProtector secretProtector,
    ICurrentUser currentUser)
    : IWooProductSyncService
{
    private const string MtlProductIdMetaKey = "_mtl_v2_product_id";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<WooSyncResult> PullAllAsync(CancellationToken cancellationToken = default)
    {
        var companyId = RequireCompanyId();
        var connection = await GetConnectionAsync(cancellationToken);
        ConfigureClient(connection);
        await PullCategoriesAsync(companyId, cancellationToken);

        var job = new SyncJob
        {
            CompanyId = companyId,
            Type = "WooProductsPull",
            Status = SyncJobStatus.Running,
            StartedAtUtc = DateTimeOffset.UtcNow
        };
        db.SyncJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        var created = 0;
        var updated = 0;
        var processed = 0;
        var errors = new List<string>();

        try
        {
            const int pageSize = 100;
            for (var page = 1; ; page++)
            {
                var response = await httpClient.GetAsync(
                    $"wp-json/wc/v3/products?per_page={pageSize}&page={page}&orderby=id&order=asc",
                    cancellationToken);
                await EnsureWooSuccessAsync(response, "Lấy danh sách sản phẩm", cancellationToken);

                var products = await response.Content.ReadFromJsonAsync<List<WooProductDto>>(JsonOptions, cancellationToken)
                    ?? [];

                foreach (var woo in products)
                {
                    try
                    {
                        var link = await db.WooProductLinks
                            .Include(x => x.Product)
                            .ThenInclude(x => x.CategoryAssignments)
                            .Include(x => x.Product)
                            .ThenInclude(x => x.Images)
                            .SingleOrDefaultAsync(x => x.WooProductId == woo.Id, cancellationToken);

                        var product = link?.Product;
                        if (product is null && !string.IsNullOrWhiteSpace(woo.Sku))
                        {
                            product = await db.Products
                                .Include(x => x.CategoryAssignments)
                                .Include(x => x.Images)
                                .SingleOrDefaultAsync(x => x.Sku == woo.Sku, cancellationToken);
                        }

                        if (product is null)
                        {
                            product = new Product { CompanyId = companyId };
                            db.Products.Add(product);
                            created++;
                        }
                        else
                        {
                            updated++;
                        }

                        MapFromWoo(product, woo);

                        if (link is null)
                        {
                            link = new WooProductLink
                            {
                                CompanyId = companyId,
                                Product = product,
                                WooProductId = woo.Id
                            };
                            db.WooProductLinks.Add(link);
                        }

                        link.WooPermalink = woo.Permalink;
                        link.WooCreatedAtUtc = woo.DateCreatedGmt;
                        link.LastPulledAtUtc = DateTimeOffset.UtcNow;
                        processed++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Woo product {woo.Id}: {ex.Message}");
                    }
                }

                await db.SaveChangesAsync(cancellationToken);
                if (products.Count < pageSize)
                {
                    break;
                }
            }

            job.ProcessedCount = processed;
            job.FailedCount = errors.Count;
            job.Status = errors.Count == 0 ? SyncJobStatus.Succeeded : SyncJobStatus.Failed;
            job.Error = errors.Count == 0 ? null : string.Join(Environment.NewLine, errors.Take(20));
            job.CompletedAtUtc = DateTimeOffset.UtcNow;
            connection.LastSuccessfulSyncAtUtc = errors.Count == 0 ? DateTimeOffset.UtcNow : connection.LastSuccessfulSyncAtUtc;
            connection.LastError = job.Error;
            await db.SaveChangesAsync(cancellationToken);

            return new WooSyncResult(processed, created, updated, errors);
        }
        catch (Exception ex)
        {
            var failedEntries = ex is DbUpdateConcurrencyException concurrencyException
                ? string.Join("; ", concurrencyException.Entries.Select(entry =>
                    $"{entry.Metadata.ClrType.Name}[State={entry.State}; {string.Join(", ", entry.Properties
                        .Where(property => property.Metadata.Name is nameof(ProductCategoryAssignment.Id)
                            or nameof(ProductCategoryAssignment.ProductId)
                            or nameof(ProductCategoryAssignment.WooCategoryId)
                            or nameof(ProductCategoryAssignment.IsDeleted))
                        .Select(property => $"{property.Metadata.Name}: {property.OriginalValue} -> {property.CurrentValue}"))}]"))
                : null;
            var failureMessage = string.IsNullOrWhiteSpace(failedEntries)
                ? ex.Message
                : $"{ex.Message} Thực thể xung đột: {failedEntries}.";

            // A failed SaveChanges leaves the tracked graph in an invalid state. Detach it before
            // recording the failed job so the original exception is not hidden by a second failure.
            db.ChangeTracker.Clear();
            var failedJob = await db.SyncJobs
                .SingleAsync(x => x.Id == job.Id, CancellationToken.None);
            var failedConnection = await db.WooCommerceConnections
                .SingleAsync(x => x.Id == connection.Id, CancellationToken.None);

            failedJob.Status = SyncJobStatus.Failed;
            failedJob.Error = failureMessage;
            failedJob.CompletedAtUtc = DateTimeOffset.UtcNow;
            failedConnection.LastError = failureMessage;
            await db.SaveChangesAsync(CancellationToken.None);
            throw new InvalidOperationException(failureMessage, ex);
        }
    }

    public async Task PushAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var product = await db.Products
            .Include(x => x.WooLink)
            .Include(x => x.CategoryAssignments)
            .Include(x => x.Images)
            .SingleOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy sản phẩm.");

        await PushAsync(product, cancellationToken);
    }

    public async Task PushAsync(Product product, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        ConfigureClient(connection);

        var payload = new
        {
            name = product.Name,
            sku = IsGeneratedSku(product.Sku) ? null : product.Sku,
            status = product.Status == ProductStatus.Active ? "publish" : "draft",
            regular_price = product.RegularPrice?.ToString("0.##", CultureInfo.InvariantCulture),
            sale_price = product.SalePrice.ToString("0.##", CultureInfo.InvariantCulture),
            manage_stock = product.TrackInventory,
            stock_quantity = product.TrackInventory ? (int?)product.StockQuantity : null,
            stock_status = product.Availability switch
            {
                ProductAvailability.InStock => "instock",
                ProductAvailability.OnBackorder => "onbackorder",
                _ => "outofstock"
            },
            short_description = product.ShortDescription,
            description = product.Description,
            categories = product.CategoryAssignments.Count > 0
                ? product.CategoryAssignments.Select(category => new { id = category.WooCategoryId }).ToArray()
                : product.WooCategoryId.HasValue ? new[] { new { id = product.WooCategoryId.Value } } : null,
            images = BuildWooImagePayload(product),
            meta_data = new[]
            {
                new { key = MtlProductIdMetaKey, value = product.Id.ToString("D") }
            }
        };

        var recoveredWoo = product.WooLink is null
            ? await FindExistingWooProductAsync(product, cancellationToken)
            : null;
        var wooProductId = product.WooLink?.WooProductId ?? recoveredWoo?.Id;

        using var response = wooProductId.HasValue
            ? await httpClient.PutAsJsonAsync(
                $"wp-json/wc/v3/products/{wooProductId.Value}",
                payload,
                JsonOptions,
                cancellationToken)
            : await httpClient.PostAsJsonAsync(
                "wp-json/wc/v3/products",
                payload,
                JsonOptions,
                cancellationToken);

        var operation = wooProductId.HasValue
            ? $"Cập nhật sản phẩm Woo #{wooProductId.Value}"
            : "Tạo sản phẩm trên WooCommerce";
        await EnsureWooSuccessAsync(response, operation, cancellationToken);
        var woo = await response.Content.ReadFromJsonAsync<WooProductDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("WooCommerce trả về dữ liệu không hợp lệ.");

        if (product.WooLink is null)
        {
            product.WooLink = new WooProductLink
            {
                CompanyId = product.CompanyId,
                ProductId = product.Id,
                WooProductId = woo.Id
            };
        }

        product.WooLink.WooPermalink = woo.Permalink;
        product.WooLink.WooCreatedAtUtc = woo.DateCreatedGmt;
        product.WooLink.LastPushedAtUtc = DateTimeOffset.UtcNow;
        SyncProductCategories(product, woo.Categories);
        SyncProductImages(product, woo.Images);
        product.SyncState = ProductSyncState.Synced;
        product.LastSyncedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<WooProductDto?> FindExistingWooProductAsync(
        Product product,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(product.Name)) return null;

        var search = Uri.EscapeDataString(product.Name.Trim());
        using var response = await httpClient.GetAsync(
            $"wp-json/wc/v3/products?per_page=100&status=any&orderby=id&order=desc&search={search}",
            cancellationToken);
        await EnsureWooSuccessAsync(response, "Tìm sản phẩm trên WooCommerce", cancellationToken);

        var candidates = await response.Content.ReadFromJsonAsync<List<WooProductDto>>(JsonOptions, cancellationToken)
            ?? [];
        var marker = product.Id.ToString("D");
        var recovered = candidates.FirstOrDefault(candidate => candidate.MetaData.Any(meta =>
            meta.Key.Equals(MtlProductIdMetaKey, StringComparison.Ordinal) &&
            MetaValueEquals(meta.Value, marker)));
        if (recovered is not null) return recovered;

        var recentExactMatches = candidates
            .Where(candidate => candidate.Name.Equals(product.Name.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(candidate => candidate.DateCreatedGmt >= DateTimeOffset.UtcNow.AddDays(-1))
            .ToList();
        if (recentExactMatches.Count == 0) return null;

        var candidateIds = recentExactMatches.Select(candidate => candidate.Id).ToArray();
        var linkedIds = await db.WooProductLinks
            .Where(link => candidateIds.Contains(link.WooProductId))
            .Select(link => link.WooProductId)
            .ToListAsync(cancellationToken);
        var unlinked = recentExactMatches
            .Where(candidate => !linkedIds.Contains(candidate.Id))
            .Select(candidate => candidate.Id)
            .ToArray();
        if (unlinked.Length > 0)
        {
            throw new WooProductRecoveryRequiredException(
                $"WooCommerce đã có sản phẩm cùng tên chưa liên kết (ID: {string.Join(", ", unlinked)}). " +
                "Hệ thống đã dừng để tránh tạo thêm bản trùng. Hãy xử lý các bản nháp trùng rồi đồng bộ lại từ WooCommerce.");
        }

        return null;
    }

    private static bool MetaValueEquals(JsonElement value, string expected) =>
        value.ValueKind == JsonValueKind.String
            ? string.Equals(value.GetString(), expected, StringComparison.OrdinalIgnoreCase)
            : string.Equals(value.ToString(), expected, StringComparison.OrdinalIgnoreCase);

    public async Task DeleteAsync(Guid productId, bool force = false, CancellationToken cancellationToken = default)
    {
        var product = await db.Products
            .Include(x => x.WooLink)
            .Include(x => x.Images)
            .SingleOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy sản phẩm.");

        if (product.WooLink is not null)
        {
            var connection = await GetConnectionAsync(cancellationToken);
            ConfigureClient(connection);
            var response = await httpClient.DeleteAsync(
                $"wp-json/wc/v3/products/{product.WooLink.WooProductId}?force={force.ToString().ToLowerInvariant()}",
                cancellationToken);
            await EnsureWooSuccessAsync(response, $"Xóa sản phẩm Woo #{product.WooLink.WooProductId}", cancellationToken);
        }

        db.ProductImages.RemoveRange(product.Images);
        db.Products.Remove(product);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        ConfigureClient(connection);
        var response = await httpClient.GetAsync("wp-json/wc/v3/products?per_page=1", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private async Task<WooCommerceConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = await db.WooCommerceConnections.SingleOrDefaultAsync(cancellationToken);
        if (connection is null || !connection.IsEnabled)
        {
            throw new InvalidOperationException("WooCommerce chưa được cấu hình hoặc chưa bật.");
        }

        return connection;
    }

    private void ConfigureClient(WooCommerceConnection connection)
    {
        if (!Uri.TryCreate(connection.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Host != "localhost"))
        {
            throw new InvalidOperationException("WooCommerce URL phải dùng HTTPS.");
        }

        var key = secretProtector.Unprotect(connection.ConsumerKeyCiphertext);
        var secret = secretProtector.Unprotect(connection.ConsumerSecretCiphertext);
        httpClient.BaseAddress = baseUri;
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{key}:{secret}")));
    }

    private Guid RequireCompanyId() => currentUser.CompanyId
        ?? throw new UnauthorizedAccessException("Không xác định được công ty của người dùng.");

    private async Task PullCategoriesAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var existing = await db.WooCategories.ToDictionaryAsync(x => x.WooCategoryId, cancellationToken);
        const int pageSize = 100;

        for (var page = 1; ; page++)
        {
            var response = await httpClient.GetAsync(
                $"wp-json/wc/v3/products/categories?per_page={pageSize}&page={page}&orderby=id&order=asc",
                cancellationToken);
            await EnsureWooSuccessAsync(response, "Lấy danh mục WooCommerce", cancellationToken);
            var categories = await response.Content.ReadFromJsonAsync<List<WooCategoryDto>>(JsonOptions, cancellationToken)
                ?? [];

            foreach (var woo in categories)
            {
                if (!existing.TryGetValue(woo.Id, out var category))
                {
                    category = new WooCategory
                    {
                        CompanyId = companyId,
                        WooCategoryId = woo.Id
                    };
                    existing.Add(woo.Id, category);
                    db.WooCategories.Add(category);
                }

                category.Name = woo.Name ?? $"Danh mục #{woo.Id}";
                category.Slug = woo.Slug;
                category.ParentWooCategoryId = woo.Parent > 0 ? woo.Parent : null;
                category.ProductCount = woo.Count;
                category.LastSyncedAtUtc = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync(cancellationToken);
            if (categories.Count < pageSize)
            {
                break;
            }
        }
    }

    private void MapFromWoo(Product product, WooProductDto woo)
    {
        product.Sku = string.IsNullOrWhiteSpace(woo.Sku) ? $"WOO-{woo.Id}" : woo.Sku;
        product.Name = woo.Name;
        product.Slug = woo.Slug;
        product.ShortDescription = woo.ShortDescription;
        product.Description = woo.Description;
        product.RegularPrice = ParseMoney(woo.RegularPrice);
        product.SalePrice = ParseMoney(woo.SalePrice) ?? ParseMoney(woo.Price) ?? 0;
        product.StockQuantity = woo.StockQuantity ?? 0;
        product.TrackInventory = woo.ManageStock;
        product.Availability = woo.StockStatus switch
        {
            "instock" => ProductAvailability.InStock,
            "onbackorder" => ProductAvailability.OnBackorder,
            _ => ProductAvailability.OutOfStock
        };
        SyncProductImages(product, woo.Images);
        product.CategoryName = woo.Categories.FirstOrDefault()?.Name;
        product.WooCategoryId = woo.Categories.FirstOrDefault()?.Id;
        SyncProductCategories(product, woo.Categories);
        product.Brand = woo.Brands.FirstOrDefault()?.Name ?? woo.Attributes
            .FirstOrDefault(attribute =>
                attribute.Name.Equals("Brand", StringComparison.OrdinalIgnoreCase) ||
                attribute.Name.Equals("Thương hiệu", StringComparison.OrdinalIgnoreCase) ||
                attribute.Name.Equals("Hãng", StringComparison.OrdinalIgnoreCase))
            ?.Options.FirstOrDefault() ?? product.Brand;
        product.Status = woo.Status == "publish" ? ProductStatus.Active : ProductStatus.Draft;
        product.SyncState = ProductSyncState.Synced;
        product.LastSyncedAtUtc = DateTimeOffset.UtcNow;
        product.WooModifiedAtUtc = woo.DateModifiedGmt;
        product.ConcurrencyToken = Guid.NewGuid();
    }

    private void SyncProductCategories(Product product, IReadOnlyList<WooCategoryDto> categories)
    {
        var incoming = categories
            .Where(category => category.Id > 0)
            .GroupBy(category => category.Id)
            .Select(group => group.First())
            .ToList();
        var incomingIds = incoming.Select(category => category.Id).ToHashSet();

        foreach (var stale in product.CategoryAssignments
                     .Where(category => !incomingIds.Contains(category.WooCategoryId))
                     .ToList())
        {
            var assignmentState = db.Entry(stale).State;
            var wasAddedInCurrentBatch = assignmentState is EntityState.Added or EntityState.Detached;
            product.CategoryAssignments.Remove(stale);

            // A Woo catalogue can contain duplicate SKUs that map more than one remote item
            // to the same local product during a page import. An assignment added and then
            // replaced before the batch is saved has no database row to soft-delete.
            if (wasAddedInCurrentBatch)
            {
                stale.Product = null!;
                stale.ProductId = Guid.Empty;
                db.Entry(stale).State = EntityState.Detached;
            }
            else
            {
                db.ProductCategoryAssignments.Remove(stale);
            }
        }

        var existingIds = product.CategoryAssignments.Select(category => category.WooCategoryId).ToHashSet();
        foreach (var woo in incoming.Where(category => !existingIds.Contains(category.Id)))
        {
            var assignment = new ProductCategoryAssignment
            {
                CompanyId = product.CompanyId,
                Product = product,
                WooCategoryId = woo.Id,
                Name = woo.Name ?? $"Danh mục #{woo.Id}"
            };
            product.CategoryAssignments.Add(assignment);
            db.Entry(assignment).State = EntityState.Added;
        }

        if (incoming.Count > 0)
        {
            product.WooCategoryId = incoming[0].Id;
            product.CategoryName = incoming[0].Name;
        }
        else
        {
            product.WooCategoryId = null;
            product.CategoryName = null;
        }
    }

    private void SyncProductImages(Product product, IReadOnlyList<WooImageDto> images)
    {
        var incoming = images
            .Where(image => image.Id > 0 || !string.IsNullOrWhiteSpace(image.Src))
            .GroupBy(image => image.Id > 0 ? $"id:{image.Id}" : $"url:{image.Src}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        var remaining = product.Images.ToList();

        for (var position = 0; position < incoming.Count; position++)
        {
            var woo = incoming[position];
            var existing = remaining.FirstOrDefault(image =>
                (woo.Id > 0 && image.WooMediaId == woo.Id) ||
                (!string.IsNullOrWhiteSpace(woo.Src) && image.Url.Equals(woo.Src, StringComparison.OrdinalIgnoreCase)));

            if (existing is null)
            {
                existing = new ProductImage
                {
                    CompanyId = product.CompanyId,
                    Product = product
                };
                product.Images.Add(existing);
                db.Entry(existing).State = EntityState.Added;
            }
            else
            {
                remaining.Remove(existing);
            }

            existing.WooMediaId = woo.Id > 0 ? woo.Id : null;
            existing.Url = woo.Src ?? existing.Url;
            existing.ThumbnailUrl = woo.Src ?? existing.ThumbnailUrl;
            existing.Name = woo.Name;
            existing.AltText = woo.Alt;
            existing.Position = position;
        }

        foreach (var stale in remaining)
        {
            var state = db.Entry(stale).State;
            product.Images.Remove(stale);
            if (state is EntityState.Added or EntityState.Detached)
            {
                stale.Product = null!;
                stale.ProductId = Guid.Empty;
                db.Entry(stale).State = EntityState.Detached;
            }
            else
            {
                db.ProductImages.Remove(stale);
            }
        }

        product.PrimaryImageUrl = product.Images
            .OrderBy(image => image.Position)
            .Select(image => image.Url)
            .FirstOrDefault();
    }

    private static WooImageWriteDto[]? BuildWooImagePayload(Product product)
    {
        var images = product.Images
            .OrderBy(image => image.Position)
            .Select(image => new WooImageWriteDto
            {
                Id = image.WooMediaId,
                Src = image.WooMediaId.HasValue ? null : image.Url,
                Name = image.Name,
                Alt = image.AltText
            })
            .ToArray();

        if (images.Length > 0) return images;
        return string.IsNullOrWhiteSpace(product.PrimaryImageUrl)
            ? null
            : [new WooImageWriteDto { Src = product.PrimaryImageUrl }];
    }

    private static bool IsGeneratedSku(string sku) => sku.StartsWith("MTL-AUTO-", StringComparison.OrdinalIgnoreCase);

    private static decimal? ParseMoney(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    private static async Task EnsureWooSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        string? errorCode = null;
        string? remoteMessage = null;
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            try
            {
                using var document = JsonDocument.Parse(responseBody);
                var root = document.RootElement;
                if (root.TryGetProperty("code", out var codeElement) && codeElement.ValueKind == JsonValueKind.String)
                {
                    errorCode = codeElement.GetString();
                }
                if (root.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String)
                {
                    remoteMessage = messageElement.GetString();
                }
            }
            catch (JsonException)
            {
                // WooCommerce or a reverse proxy can return HTML. Use the HTTP status instead of
                // exposing an arbitrary response body to users or logs.
            }
        }

        var status = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}".Trim();
        var code = string.IsNullOrWhiteSpace(errorCode) ? null : $" [{errorCode}]";
        var detail = string.IsNullOrWhiteSpace(remoteMessage) ? status : remoteMessage.Trim();
        throw new WooCommerceRequestException(
            $"{operation} không thành công{code}: {detail}",
            response.StatusCode,
            errorCode);
    }
}

internal sealed class WooProductDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("sku")]
    public string Sku { get; set; } = string.Empty;
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }
    [JsonPropertyName("status")]
    public string? Status { get; set; }
    [JsonPropertyName("permalink")]
    public string? Permalink { get; set; }
    [JsonPropertyName("price")]
    public string? Price { get; set; }
    [JsonPropertyName("regular_price")]
    public string? RegularPrice { get; set; }
    [JsonPropertyName("sale_price")]
    public string? SalePrice { get; set; }
    [JsonPropertyName("short_description")]
    public string? ShortDescription { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("manage_stock")]
    public bool ManageStock { get; set; }
    [JsonPropertyName("stock_quantity")]
    public decimal? StockQuantity { get; set; }
    [JsonPropertyName("stock_status")]
    public string? StockStatus { get; set; }
    [JsonPropertyName("date_modified_gmt")]
    public DateTimeOffset? DateModifiedGmt { get; set; }
    [JsonPropertyName("meta_data")]
    public List<WooMetaDataDto> MetaData { get; set; } = [];
    [JsonPropertyName("date_created_gmt")]
    public DateTimeOffset? DateCreatedGmt { get; set; }
    [JsonPropertyName("images")]
    public List<WooImageDto> Images { get; set; } = [];
    [JsonPropertyName("categories")]
    public List<WooCategoryDto> Categories { get; set; } = [];
    [JsonPropertyName("attributes")]
    public List<WooAttributeDto> Attributes { get; set; } = [];
    [JsonPropertyName("brands")]
    public List<WooBrandDto> Brands { get; set; } = [];
}

internal sealed class WooMetaDataDto
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;
    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }
}

internal sealed class WooImageDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("src")]
    public string? Src { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("alt")]
    public string? Alt { get; set; }
}

internal sealed class WooImageWriteDto
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }
    [JsonPropertyName("src")]
    public string? Src { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("alt")]
    public string? Alt { get; set; }
}

internal sealed class WooCategoryDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }
    [JsonPropertyName("parent")]
    public long Parent { get; set; }
    [JsonPropertyName("count")]
    public int Count { get; set; }
}

internal sealed class WooAttributeDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("options")]
    public List<string> Options { get; set; } = [];
}

internal sealed class WooBrandDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

