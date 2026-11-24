using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MTL.Domain.Catalog;
using MTL.Domain.Identity;
using MTL.Infrastructure.Identity;
using MTL.Infrastructure.Integrations.WooCommerce;
using MTL.Infrastructure.Persistence;

namespace MTL.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(
    AppDbContext db,
    ICurrentUser currentUser,
    IWooProductSyncService wooSync)
    : ControllerBase
{
    [Authorize(Policy = Permissions.ProductsRead)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductListItem>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] string stock = "all",
        [FromQuery] ProductStatus? status = null,
        [FromQuery] ProductSyncState? syncState = null,
        [FromQuery] long? categoryId = null,
        [FromQuery] string? category = null,
        [FromQuery] string? brand = null,
        [FromQuery] string warning = "all",
        [FromQuery] string sort = "newest",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.Name, $"%{term}%") ||
                EF.Functions.ILike(x.Sku, $"%{term}%") ||
                (x.Brand != null && EF.Functions.ILike(x.Brand, $"%{term}%")) ||
                (x.CategoryName != null && EF.Functions.ILike(x.CategoryName, $"%{term}%")));
        }

        query = stock.ToLowerInvariant() switch
        {
            "in_stock" => query.Where(x => x.Availability == ProductAvailability.InStock),
            "out_of_stock" => query.Where(x => x.Availability == ProductAvailability.OutOfStock),
            "on_backorder" => query.Where(x => x.Availability == ProductAvailability.OnBackorder),
            _ => query
        };

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (syncState.HasValue)
        {
            query = query.Where(x => x.SyncState == syncState.Value);
        }

        if (categoryId.HasValue)
        {
            var hierarchy = await db.WooCategories.AsNoTracking()
                .Select(x => new CategoryHierarchyRow(x.WooCategoryId, x.ParentWooCategoryId))
                .ToListAsync(cancellationToken);
            var categoryIds = GetCategoryAndDescendantIds(categoryId.Value, hierarchy);
            query = query.Where(x =>
                x.CategoryAssignments.Any(assignment => categoryIds.Contains(assignment.WooCategoryId)) ||
                (x.WooCategoryId.HasValue && categoryIds.Contains(x.WooCategoryId.Value)));
        }
        else if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(x => x.CategoryName == category);
        }

        if (!string.IsNullOrWhiteSpace(brand))
        {
            query = query.Where(x => x.Brand == brand);
        }

        query = warning.ToLowerInvariant() switch
        {
            "missing_image" => query.Where(x => x.PrimaryImageUrl == null || x.PrimaryImageUrl == ""),
            "missing_price" => query.Where(x => x.SalePrice <= 0),
            "missing_any" => query.Where(x => x.SalePrice <= 0 || x.PrimaryImageUrl == null || x.PrimaryImageUrl == ""),
            _ => query
        };

        var total = await query.CountAsync(cancellationToken);
        query = sort.ToLowerInvariant() switch
        {
            "oldest" => query.OrderBy(x => x.WooLink != null && x.WooLink.WooCreatedAtUtc.HasValue ? x.WooLink.WooCreatedAtUtc.Value : x.CreatedAtUtc).ThenBy(x => x.Name),
            "price_asc" => query.OrderBy(x => x.SalePrice).ThenBy(x => x.Name),
            "price_desc" => query.OrderByDescending(x => x.SalePrice).ThenBy(x => x.Name),
            "name_asc" => query.OrderBy(x => x.Name),
            "stock_desc" => query.OrderByDescending(x => x.StockQuantity).ThenBy(x => x.Name),
            _ => query.OrderByDescending(x => x.WooLink != null && x.WooLink.WooCreatedAtUtc.HasValue ? x.WooLink.WooCreatedAtUtc.Value : x.CreatedAtUtc).ThenBy(x => x.Name)
        };

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ProductListItem(
                x.Id,
                x.Sku,
                x.Name,
                x.SalePrice,
                x.RegularPrice,
                x.CostPrice,
                x.StockQuantity,
                x.TrackInventory,
                x.Availability,
                x.Status,
                x.SyncState,
                x.PrimaryImageUrl,
                x.Brand,
                x.CategoryName,
                x.WooCategoryId,
                x.WooLink != null ? x.WooLink.WooProductId : null,
                x.WooLink != null ? x.WooLink.WooPermalink : null,
                x.WooLink != null && x.WooLink.WooCreatedAtUtc.HasValue ? x.WooLink.WooCreatedAtUtc.Value : x.CreatedAtUtc,
                x.UpdatedAtUtc,
                x.ConcurrencyToken))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<ProductListItem>(items, total, page, pageSize));
    }

    [Authorize(Policy = Permissions.ProductsRead)]
    [HttpGet("filter-options")]
    public async Task<ActionResult<ProductFilterOptions>> GetFilterOptions(CancellationToken cancellationToken)
    {
        var wooCategories = await db.WooCategories.AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        List<ProductCategoryOption> categories;
        if (wooCategories.Count > 0)
        {
            categories = BuildCategoryTree(wooCategories);
        }
        else
        {
            var categoryRows = await db.Products.AsNoTracking()
                .Where(x => x.CategoryName != null && x.CategoryName != "")
                .Select(x => new { Name = x.CategoryName!, x.WooCategoryId })
                .ToListAsync(cancellationToken);
            categories = categoryRows
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => new ProductCategoryOption(
                    x.Key,
                    x.Select(item => item.WooCategoryId).FirstOrDefault(id => id.HasValue),
                    null,
                    0,
                    x.Key,
                    x.Count()))
                .OrderBy(x => x.Name)
                .ToList();
        }

        var brandRows = await db.Products.AsNoTracking()
            .Where(x => x.Brand != null && x.Brand != "")
            .Select(x => x.Brand!)
            .ToListAsync(cancellationToken);
        var brands = brandRows
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(x => new ProductBrandOption(x.Key, x.Count()))
            .OrderBy(x => x.Name)
            .ToList();

        return Ok(new ProductFilterOptions(categories, brands));
    }

    [Authorize(Policy = Permissions.ProductsRead)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDetails>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking()
            .Include(x => x.WooLink)
            .Include(x => x.CategoryAssignments)
            .Include(x => x.Images)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return product is null ? NotFound() : Ok(ToDetails(product));
    }

    [Authorize(Policy = Permissions.ProductsManage)]
    [HttpPost]
    public async Task<ActionResult<ProductDetails>> Create(
        SaveProductRequest request,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequest(request) is { } validationMessage)
        {
            return BadRequest(new { message = validationMessage });
        }

        var companyId = currentUser.CompanyId ?? throw new UnauthorizedAccessException();
        var productId = request.ClientRequestId is { } requestedId && requestedId != Guid.Empty
            ? requestedId
            : Guid.NewGuid();
        var existingRequest = await db.Products.AsNoTracking()
            .Include(x => x.WooLink)
            .Include(x => x.CategoryAssignments)
            .Include(x => x.Images)
            .SingleOrDefaultAsync(x => x.Id == productId, cancellationToken);
        if (existingRequest is not null)
        {
            return Ok(ToDetails(existingRequest));
        }

        var normalizedSku = string.IsNullOrWhiteSpace(request.Sku)
            ? $"MTL-AUTO-{productId:N}"
            : request.Sku.Trim();
        if (!normalizedSku.StartsWith("MTL-AUTO-", StringComparison.OrdinalIgnoreCase) &&
            await db.Products.AnyAsync(x => x.Sku == normalizedSku, cancellationToken))
        {
            return Conflict(new { message = "SKU đã tồn tại." });
        }

        var product = new Product
        {
            Id = productId,
            CompanyId = companyId,
            Sku = normalizedSku,
            Name = request.Name.Trim(),
            ShortDescription = request.ShortDescription,
            Description = request.Description,
            Brand = request.Brand,
            CategoryName = request.CategoryName,
            WooCategoryId = request.WooCategoryId,
            SalePrice = request.SalePrice,
            RegularPrice = request.RegularPrice,
            CostPrice = request.CostPrice,
            StockQuantity = request.StockQuantity,
            Unit = request.Unit,
            TrackInventory = request.TrackInventory,
            Availability = request.Availability,
            Status = request.Status,
            PrimaryImageUrl = request.PrimaryImageUrl,
            SyncState = ProductSyncState.PendingPush
        };

        db.Products.Add(product);
        await ApplyProductCategoriesAsync(product, request.WooCategoryIds, request.WooCategoryId, cancellationToken);
        ApplyProductImages(product, request.Images, request.PrimaryImageUrl);
        await db.SaveChangesAsync(cancellationToken);

        var syncResult = await PushOrMarkFailedAsync(product, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, ToDetails(syncResult.Product, syncResult.Warning));
    }

    [Authorize(Policy = Permissions.ProductsManage)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductDetails>> Update(
        Guid id,
        SaveProductRequest request,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequest(request) is { } validationMessage)
        {
            return BadRequest(new { message = validationMessage });
        }

        var product = await db.Products
            .Include(x => x.WooLink)
            .Include(x => x.CategoryAssignments)
            .Include(x => x.Images)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        if (product.ConcurrencyToken != request.ConcurrencyToken)
        {
            return Conflict(new { message = "Sản phẩm đã được người khác cập nhật. Hãy tải lại dữ liệu." });
        }

        var normalizedSku = string.IsNullOrWhiteSpace(request.Sku) ? product.Sku : request.Sku.Trim();
        if (normalizedSku != product.Sku &&
            await db.Products.AnyAsync(x => x.Id != id && x.Sku == normalizedSku, cancellationToken))
        {
            return Conflict(new { message = "SKU đã tồn tại." });
        }

        product.Sku = normalizedSku;
        product.Name = request.Name.Trim();
        product.ShortDescription = request.ShortDescription;
        product.Description = request.Description;
        product.Brand = request.Brand;
        product.CategoryName = request.CategoryName;
        product.WooCategoryId = request.WooCategoryId;
        product.SalePrice = request.SalePrice;
        product.RegularPrice = request.RegularPrice;
        product.CostPrice = request.CostPrice;
        product.StockQuantity = request.StockQuantity;
        product.Unit = request.Unit;
        product.TrackInventory = request.TrackInventory;
        product.Availability = request.Availability;
        product.Status = request.Status;
        product.PrimaryImageUrl = request.PrimaryImageUrl;
        product.SyncState = ProductSyncState.PendingPush;
        product.ConcurrencyToken = Guid.NewGuid();
        await ApplyProductCategoriesAsync(product, request.WooCategoryIds, request.WooCategoryId, cancellationToken);
        ApplyProductImages(product, request.Images, request.PrimaryImageUrl);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Sản phẩm đã được người khác cập nhật. Hãy tải lại dữ liệu." });
        }

        var syncResult = await PushOrMarkFailedAsync(product, cancellationToken);
        return Ok(ToDetails(syncResult.Product, syncResult.Warning));
    }

    [Authorize(Policy = Permissions.ProductsManage)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await wooSync.DeleteAsync(id, force: false, cancellationToken: cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return NoContent();
        }
        catch (WooCommerceRequestException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }
    }

    [Authorize(Policy = Permissions.WooCommerceManage)]
    [HttpPost("{id:guid}/push-to-woocommerce")]
    public async Task<IActionResult> PushToWoo(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await wooSync.PushAsync(id, cancellationToken);
            return Accepted(new { message = "Đã cập nhật sản phẩm lên WooCommerce." });
        }
        catch (WooCommerceRequestException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }
    }

    [Authorize(Policy = Permissions.ProductsManage)]
    [HttpPost("bulk")]
    public async Task<ActionResult<BulkProductResult>> Bulk(
        BulkProductRequest request,
        CancellationToken cancellationToken)
    {
        var ids = request.ProductIds.Distinct().ToList();
        if (ids.Count is < 1 or > 100)
        {
            return BadRequest(new { message = "Mỗi lần chỉ xử lý từ 1 đến 100 sản phẩm." });
        }

        var action = request.Action.Trim().ToLowerInvariant();
        if (action is not ("push" or "publish" or "draft" or "trash"))
        {
            return BadRequest(new { message = "Thao tác hàng loạt không hợp lệ." });
        }

        var processed = 0;
        var errors = new List<string>();
        foreach (var id in ids)
        {
            try
            {
                if (action == "trash")
                {
                    await wooSync.DeleteAsync(id, force: false, cancellationToken: cancellationToken);
                }
                else
                {
                    if (action is "publish" or "draft")
                    {
                        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
                            ?? throw new InvalidOperationException("Không tìm thấy sản phẩm.");
                        product.Status = action == "publish" ? ProductStatus.Active : ProductStatus.Draft;
                        product.SyncState = ProductSyncState.PendingPush;
                        await db.SaveChangesAsync(cancellationToken);
                    }

                    await wooSync.PushAsync(id, cancellationToken);
                }

                processed++;
            }
            catch (Exception ex)
            {
                errors.Add($"{id}: {ex.Message}");
            }
        }

        return Ok(new BulkProductResult(processed, errors.Count, errors.Take(20).ToList()));
    }

    private async Task ApplyProductCategoriesAsync(
        Product product,
        IReadOnlyList<long>? requestedIds,
        long? legacyCategoryId,
        CancellationToken cancellationToken)
    {
        var ids = (requestedIds ?? (legacyCategoryId.HasValue ? [legacyCategoryId.Value] : []))
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        var categories = ids.Count == 0
            ? []
            : await db.WooCategories.Where(category => ids.Contains(category.WooCategoryId)).ToListAsync(cancellationToken);
        var categoryById = categories.ToDictionary(category => category.WooCategoryId);
        var validIds = categoryById.Keys.ToHashSet();

        foreach (var stale in product.CategoryAssignments
                     .Where(assignment => !validIds.Contains(assignment.WooCategoryId))
                     .ToList())
        {
            db.ProductCategoryAssignments.Remove(stale);
            product.CategoryAssignments.Remove(stale);
        }

        var existingIds = product.CategoryAssignments.Select(assignment => assignment.WooCategoryId).ToHashSet();
        foreach (var categoryId in ids.Where(categoryById.ContainsKey).Where(categoryId => !existingIds.Contains(categoryId)))
        {
            var category = categoryById[categoryId];
            var assignment = new ProductCategoryAssignment
            {
                CompanyId = product.CompanyId,
                Product = product,
                WooCategoryId = category.WooCategoryId,
                Name = category.Name
            };
            product.CategoryAssignments.Add(assignment);
            db.Entry(assignment).State = EntityState.Added;
        }

        var primary = ids.Select(id => categoryById.GetValueOrDefault(id)).FirstOrDefault(category => category is not null);
        product.WooCategoryId = primary?.WooCategoryId;
        product.CategoryName = primary?.Name;
    }

    private void ApplyProductImages(
        Product product,
        IReadOnlyList<SaveProductImageRequest>? requestedImages,
        string? legacyPrimaryImageUrl)
    {
        var incoming = requestedImages is null
            ? string.IsNullOrWhiteSpace(legacyPrimaryImageUrl)
                ? []
                : [new SaveProductImageRequest(null, legacyPrimaryImageUrl.Trim(), null, null, null, 0)]
            : requestedImages
                .Where(image => !string.IsNullOrWhiteSpace(image.Url))
                .GroupBy(image => image.WooMediaId.HasValue ? $"id:{image.WooMediaId}" : $"url:{image.Url.Trim()}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(30)
                .ToList();

        var remaining = product.Images.ToList();
        for (var position = 0; position < incoming.Count; position++)
        {
            var source = incoming[position];
            var url = source.Url.Trim();
            var existing = remaining.FirstOrDefault(image =>
                (source.WooMediaId.HasValue && image.WooMediaId == source.WooMediaId) ||
                image.Url.Equals(url, StringComparison.OrdinalIgnoreCase));

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

            existing.WooMediaId = source.WooMediaId;
            existing.Url = url;
            existing.ThumbnailUrl = source.ThumbnailUrl;
            existing.Name = source.Name;
            existing.AltText = source.AltText;
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

    private static List<long> GetCategoryAndDescendantIds(long rootId, IReadOnlyList<CategoryHierarchyRow> hierarchy)
    {
        var children = hierarchy
            .Where(item => item.ParentId.HasValue)
            .GroupBy(item => item.ParentId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Id).ToList());
        var result = new List<long>();
        var visited = new HashSet<long>();
        var pending = new Stack<long>();
        pending.Push(rootId);

        while (pending.TryPop(out var current) && visited.Add(current))
        {
            result.Add(current);
            if (!children.TryGetValue(current, out var childIds)) continue;
            foreach (var childId in childIds) pending.Push(childId);
        }

        return result;
    }

    private static List<ProductCategoryOption> BuildCategoryTree(IReadOnlyList<WooCategory> categories)
    {
        var ids = categories.Select(category => category.WooCategoryId).ToHashSet();
        var children = categories
            .GroupBy(category => category.ParentWooCategoryId is > 0 && ids.Contains(category.ParentWooCategoryId.Value)
                ? category.ParentWooCategoryId.Value
                : 0L)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(category => category.Name, StringComparer.OrdinalIgnoreCase).ToList());
        var result = new List<ProductCategoryOption>();
        var visited = new HashSet<long>();

        void Append(long parentId, int depth, string? parentPath)
        {
            if (!children.TryGetValue(parentId, out var nodes)) return;
            foreach (var category in nodes)
            {
                if (!visited.Add(category.WooCategoryId)) continue;
                var path = string.IsNullOrWhiteSpace(parentPath) ? category.Name : $"{parentPath} › {category.Name}";
                result.Add(new ProductCategoryOption(
                    category.Name,
                    category.WooCategoryId,
                    category.ParentWooCategoryId,
                    depth,
                    path,
                    category.ProductCount));
                Append(category.WooCategoryId, depth + 1, path);
            }
        }

        Append(0, 0, null);
        foreach (var category in categories.OrderBy(category => category.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (visited.Contains(category.WooCategoryId)) continue;
            var path = category.Name;
            result.Add(new ProductCategoryOption(
                category.Name,
                category.WooCategoryId,
                category.ParentWooCategoryId,
                0,
                path,
                category.ProductCount));
            Append(category.WooCategoryId, 1, path);
        }

        return result;
    }

    private async Task<(Product Product, string? Warning)> PushOrMarkFailedAsync(
        Product product,
        CancellationToken cancellationToken)
    {
        try
        {
            await wooSync.PushAsync(product, cancellationToken);
            return (product, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var reason = exception.Message;
            db.ChangeTracker.Clear();

            var persistedProduct = await db.Products
                .Include(x => x.WooLink)
                .Include(x => x.CategoryAssignments)
                .Include(x => x.Images)
                .SingleAsync(x => x.Id == product.Id, cancellationToken);
            persistedProduct.SyncState = ProductSyncState.Failed;

            var connection = await db.WooCommerceConnections.SingleOrDefaultAsync(cancellationToken);
            if (connection is not null)
            {
                connection.LastError = reason;
            }

            await db.SaveChangesAsync(cancellationToken);
            return (
                persistedProduct,
                $"Chưa đồng bộ WooCommerce: {reason}");
        }
    }

    private static string? ValidateRequest(SaveProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "Tên sản phẩm không được để trống.";
        if (string.IsNullOrWhiteSpace(request.Unit)) return "Đơn vị tính không được để trống.";
        if (request.SalePrice < 0 || request.CostPrice < 0 || request.RegularPrice < 0)
            return "Giá sản phẩm không được nhỏ hơn 0.";
        if (request.RegularPrice.HasValue && request.SalePrice > request.RegularPrice.Value)
            return "Giá khuyến mãi không được lớn hơn giá thông thường.";
        if (request.StockQuantity < 0) return "Số lượng tồn không được nhỏ hơn 0.";
        return null;
    }

    private static ProductDetails ToDetails(Product x, string? syncWarning = null) => new(
        x.Id,
        x.Sku,
        x.Name,
        x.ShortDescription,
        x.Description,
        x.Brand,
        x.CategoryName,
        x.WooCategoryId,
        x.CategoryAssignments.Select(category => category.WooCategoryId).ToList(),
        x.SalePrice,
        x.RegularPrice,
        x.CostPrice,
        x.StockQuantity,
        x.Unit,
        x.TrackInventory,
        x.Availability,
        x.Status,
        x.SyncState,
        x.PrimaryImageUrl,
        x.Images.OrderBy(image => image.Position).Select(image => new ProductImageDetails(
            image.WooMediaId,
            image.Url,
            image.ThumbnailUrl,
            image.Name,
            image.AltText,
            image.Position)).ToList(),
        x.WooLink?.WooProductId,
        x.WooLink?.WooPermalink,
        x.ConcurrencyToken,
        syncWarning);
}

public sealed record SaveProductRequest(
    string? Sku,
    string Name,
    string? ShortDescription,
    string? Description,
    string? Brand,
    string? CategoryName,
    long? WooCategoryId,
    IReadOnlyList<long>? WooCategoryIds,
    decimal SalePrice,
    decimal? RegularPrice,
    decimal CostPrice,
    decimal StockQuantity,
    string Unit,
    bool TrackInventory,
    ProductAvailability Availability,
    ProductStatus Status,
    string? PrimaryImageUrl,
    IReadOnlyList<SaveProductImageRequest>? Images,
    Guid? ConcurrencyToken,
    Guid? ClientRequestId);

public sealed record SaveProductImageRequest(
    long? WooMediaId,
    string Url,
    string? ThumbnailUrl,
    string? Name,
    string? AltText,
    int Position);

public sealed record ProductListItem(
    Guid Id,
    string Sku,
    string Name,
    decimal SalePrice,
    decimal? RegularPrice,
    decimal CostPrice,
    decimal StockQuantity,
    bool TrackInventory,
    ProductAvailability Availability,
    ProductStatus Status,
    ProductSyncState SyncState,
    string? PrimaryImageUrl,
    string? Brand,
    string? CategoryName,
    long? WooCategoryId,
    long? WooProductId,
    string? WooPermalink,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    Guid ConcurrencyToken);

public sealed record ProductDetails(
    Guid Id,
    string Sku,
    string Name,
    string? ShortDescription,
    string? Description,
    string? Brand,
    string? CategoryName,
    long? WooCategoryId,
    IReadOnlyList<long> WooCategoryIds,
    decimal SalePrice,
    decimal? RegularPrice,
    decimal CostPrice,
    decimal StockQuantity,
    string Unit,
    bool TrackInventory,
    ProductAvailability Availability,
    ProductStatus Status,
    ProductSyncState SyncState,
    string? PrimaryImageUrl,
    IReadOnlyList<ProductImageDetails> Images,
    long? WooProductId,
    string? WooPermalink,
    Guid ConcurrencyToken,
    string? SyncWarning = null);

public sealed record ProductImageDetails(
    long? WooMediaId,
    string Url,
    string? ThumbnailUrl,
    string? Name,
    string? AltText,
    int Position);

public sealed record ProductCategoryOption(
    string Name,
    long? WooCategoryId,
    long? ParentWooCategoryId,
    int Depth,
    string Path,
    int Count);
public sealed record ProductBrandOption(string Name, int Count);
public sealed record ProductFilterOptions(
    IReadOnlyList<ProductCategoryOption> Categories,
    IReadOnlyList<ProductBrandOption> Brands);
public sealed record BulkProductRequest(IReadOnlyList<Guid> ProductIds, string Action);
public sealed record BulkProductResult(int Processed, int Failed, IReadOnlyList<string> Errors);
public sealed record CategoryHierarchyRow(long Id, long? ParentId);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

