using System.Text.Json;
using MTL.Domain.Common;

namespace MTL.Domain.Catalog;

public enum ProductStatus
{
    Draft,
    Active,
    Inactive,
    Discontinued
}

public enum ProductSyncState
{
    NotLinked,
    Synced,
    PendingPush,
    PendingPull,
    Conflict,
    Failed
}

public enum ProductAvailability
{
    InStock,
    OutOfStock,
    OnBackorder
}

public enum ProductCatalogVisibility
{
    Visible,
    Catalog,
    Search,
    Hidden
}

public sealed record ProductTag(long? WooTagId, string Name);

public static class ProductTagCollection
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<ProductTag> Normalize(IEnumerable<ProductTag>? tags) =>
        (tags ?? [])
            .Where(tag => !string.IsNullOrWhiteSpace(tag.Name))
            .Select(tag => new ProductTag(tag.WooTagId is > 0 ? tag.WooTagId : null, tag.Name.Trim()))
            .GroupBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(50)
            .ToList();

    public static IReadOnlyList<ProductTag> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return Normalize(JsonSerializer.Deserialize<List<ProductTag>>(json, JsonOptions));
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string Serialize(IEnumerable<ProductTag>? tags) =>
        JsonSerializer.Serialize(Normalize(tags), JsonOptions);
}

public sealed class Product : CompanyOwnedEntity
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    public string? Brand { get; set; }
    public string? CategoryName { get; set; }
    public long? WooCategoryId { get; set; }
    public decimal SalePrice { get; set; }
    public decimal? RegularPrice { get; set; }
    public decimal CostPrice { get; set; }
    public decimal StockQuantity { get; set; }
    public string Unit { get; set; } = "Cái";
    public bool TrackInventory { get; set; } = true;
    public ProductAvailability Availability { get; set; } = ProductAvailability.OutOfStock;
    public ProductCatalogVisibility CatalogVisibility { get; set; } = ProductCatalogVisibility.Visible;
    public ProductStatus Status { get; set; } = ProductStatus.Draft;
    public string TagsJson { get; set; } = "[]";
    public string? PrimaryImageUrl { get; set; }
    public ProductSyncState SyncState { get; set; } = ProductSyncState.NotLinked;
    public DateTimeOffset? LastSyncedAtUtc { get; set; }
    public DateTimeOffset? WooModifiedAtUtc { get; set; }
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public ICollection<ProductVariant> Variants { get; set; } = [];
    public ICollection<ProductCategoryAssignment> CategoryAssignments { get; set; } = [];
    public ICollection<ProductImage> Images { get; set; } = [];
    public WooProductLink? WooLink { get; set; }
}

public sealed class WooCategory : CompanyOwnedEntity
{
    public long WooCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public long? ParentWooCategoryId { get; set; }
    public int ProductCount { get; set; }
    public DateTimeOffset LastSyncedAtUtc { get; set; }
}

public sealed class ProductCategoryAssignment : CompanyOwnedEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public long WooCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class ProductImage : CompanyOwnedEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public long? WooMediaId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? Name { get; set; }
    public string? AltText { get; set; }
    public int Position { get; set; }
}

public sealed class ProductVariant : CompanyOwnedEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AttributesJson { get; set; } = "{}";
    public decimal? SalePrice { get; set; }
    public decimal StockQuantity { get; set; }
    public long? WooVariationId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class WooProductLink : CompanyOwnedEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public long WooProductId { get; set; }
    public string? WooPermalink { get; set; }
    public DateTimeOffset? WooCreatedAtUtc { get; set; }
    public DateTimeOffset? LastPulledAtUtc { get; set; }
    public DateTimeOffset? LastPushedAtUtc { get; set; }
    public string? LastWooHash { get; set; }
    public string? LastLocalHash { get; set; }
}

