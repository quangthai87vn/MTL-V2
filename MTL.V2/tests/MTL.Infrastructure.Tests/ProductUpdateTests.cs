using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MTL.Api.Controllers;
using MTL.Domain.Catalog;
using MTL.Infrastructure.Identity;
using MTL.Infrastructure.Integrations.WooCommerce;
using MTL.Infrastructure.Persistence;
using Xunit;

namespace MTL.Infrastructure.Tests;

public sealed class ProductUpdateTests
{
    [Fact]
    public async Task Update_SavesEveryEditableField_WhenWooSynchronizationFails()
    {
        var fixture = await TestFixture.CreateAsync();
        if (fixture is null) return;

        await using var scope = fixture;
        var oldCategoryId = scope.NextWooId();
        var newCategoryId = scope.NextWooId();
        scope.Db.WooCategories.AddRange(
            scope.NewCategory(oldCategoryId, "Danh mục cũ"),
            scope.NewCategory(newCategoryId, "Danh mục mới"));

        var originalToken = Guid.NewGuid();
        var remoteProductId = scope.NextWooId();
        var product = new Product
        {
            CompanyId = scope.CompanyId,
            Sku = $"REGRESSION-{Guid.NewGuid():N}",
            Name = "Tên cũ",
            ShortDescription = "Mô tả ngắn cũ",
            Description = "Mô tả cũ",
            Brand = "Thương hiệu cũ",
            CategoryName = "Danh mục cũ",
            WooCategoryId = oldCategoryId,
            SalePrice = 100,
            RegularPrice = 120,
            CostPrice = 80,
            StockQuantity = 3,
            Unit = "Cái",
            TrackInventory = true,
            Availability = ProductAvailability.OutOfStock,
            Status = ProductStatus.Draft,
            PrimaryImageUrl = "https://example.test/old.jpg",
            SyncState = ProductSyncState.Synced,
            ConcurrencyToken = originalToken
        };
        product.CategoryAssignments.Add(new ProductCategoryAssignment
        {
            CompanyId = scope.CompanyId,
            Product = product,
            WooCategoryId = oldCategoryId,
            Name = "Danh mục cũ"
        });
        product.Images.Add(new ProductImage
        {
            CompanyId = scope.CompanyId,
            Product = product,
            Url = "https://example.test/old.jpg",
            Position = 0
        });
        product.WooLink = new WooProductLink
        {
            CompanyId = scope.CompanyId,
            Product = product,
            ProductId = product.Id,
            WooProductId = remoteProductId,
            WooPermalink = $"https://example.test/product/{remoteProductId}"
        };
        scope.Db.Products.Add(product);
        await scope.Db.SaveChangesAsync();

        var controller = new ProductsController(
            scope.Db,
            scope.CurrentUser,
            new FailingWooSyncService());
        var request = new SaveProductRequest(
            Sku: null,
            Name: "Tên mới",
            ShortDescription: "Mô tả ngắn mới",
            Description: "Mô tả chi tiết mới",
            Brand: "Thương hiệu mới",
            CategoryName: "Giá trị này phải được thay bằng danh mục đã chọn",
            WooCategoryId: newCategoryId,
            WooCategoryIds: [newCategoryId],
            SalePrice: 200,
            RegularPrice: 250,
            CostPrice: 150,
            StockQuantity: 0,
            Unit: "Bộ",
            TrackInventory: false,
            Availability: ProductAvailability.InStock,
            Status: ProductStatus.Active,
            PrimaryImageUrl: "https://example.test/new-primary.jpg",
            Images:
            [
                new SaveProductImageRequest(101, "https://example.test/new-primary.jpg", "https://example.test/thumb.jpg", "Ảnh chính", "Máy tính mới", 0),
                new SaveProductImageRequest(102, "https://example.test/new-gallery.jpg", null, "Ảnh phụ", null, 1)
            ],
            ConcurrencyToken: originalToken,
            ClientRequestId: null);

        var result = await controller.Update(product.Id, request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ProductDetails>(ok.Value);
        Assert.NotNull(response.SyncWarning);
        Assert.Contains("chưa đồng bộ WooCommerce", response.SyncWarning, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ProductSyncState.Failed, response.SyncState);

        scope.Db.ChangeTracker.Clear();
        var saved = await scope.Db.Products
            .Include(item => item.CategoryAssignments)
            .Include(item => item.Images)
            .Include(item => item.WooLink)
            .SingleAsync(item => item.Id == product.Id);

        Assert.Equal(product.Sku, saved.Sku);
        Assert.Equal("Tên mới", saved.Name);
        Assert.Equal("Mô tả ngắn mới", saved.ShortDescription);
        Assert.Equal("Mô tả chi tiết mới", saved.Description);
        Assert.Equal("Thương hiệu mới", saved.Brand);
        Assert.Equal("Danh mục mới", saved.CategoryName);
        Assert.Equal(newCategoryId, saved.WooCategoryId);
        Assert.Equal([newCategoryId], saved.CategoryAssignments.Select(item => item.WooCategoryId));
        Assert.Equal(200, saved.SalePrice);
        Assert.Equal(250, saved.RegularPrice);
        Assert.Equal(150, saved.CostPrice);
        Assert.Equal(0, saved.StockQuantity);
        Assert.Equal("Bộ", saved.Unit);
        Assert.False(saved.TrackInventory);
        Assert.Equal(ProductAvailability.InStock, saved.Availability);
        Assert.Equal(ProductStatus.Active, saved.Status);
        Assert.Equal(ProductSyncState.Failed, saved.SyncState);
        Assert.Equal("https://example.test/new-primary.jpg", saved.PrimaryImageUrl);
        Assert.Equal(
            ["https://example.test/new-primary.jpg", "https://example.test/new-gallery.jpg"],
            saved.Images.OrderBy(item => item.Position).Select(item => item.Url));
        Assert.Equal(remoteProductId, saved.WooLink?.WooProductId);
        Assert.NotEqual(originalToken, saved.ConcurrencyToken);
    }

    private sealed class FailingWooSyncService : IWooProductSyncService
    {
        public Task<WooSyncResult> PullAllAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task PushAsync(Guid productId, CancellationToken cancellationToken = default) =>
            throw Failure();

        public Task PushAsync(Product product, CancellationToken cancellationToken = default) =>
            throw Failure();

        public Task DeleteAsync(Guid productId, bool force = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        private static WooCommerceRequestException Failure() => new(
            "Cập nhật sản phẩm Woo #16453 không thành công [woocommerce_rest_invalid_product]: Trạng thái kho không hợp lệ.",
            HttpStatusCode.BadRequest,
            "woocommerce_rest_invalid_product");
    }

    private sealed class TestCurrentUser(Guid companyId) : ICurrentUser
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = companyId;
        public string? Email => "test@localhost";
        public bool IsAuthenticated => true;
    }

    private sealed class TestFixture(
        AppDbContext db,
        TestCurrentUser currentUser,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
        : IAsyncDisposable
    {
        private long nextWooId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 100_000;

        public AppDbContext Db { get; } = db;
        public TestCurrentUser CurrentUser { get; } = currentUser;
        public Guid CompanyId => CurrentUser.CompanyId!.Value;

        public static async Task<TestFixture?> CreateAsync()
        {
            var connectionString = Environment.GetEnvironmentVariable("MTL_TEST_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(connectionString)) return null;

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var discovery = new AppDbContext(options, new TestCurrentUser(Guid.Empty));
            var companyId = await discovery.Companies.Select(company => company.Id).FirstAsync();
            var currentUser = new TestCurrentUser(companyId);
            var db = new AppDbContext(options, currentUser);
            var transaction = await db.Database.BeginTransactionAsync();
            return new TestFixture(db, currentUser, transaction);
        }

        public long NextWooId() => Interlocked.Increment(ref nextWooId);

        public WooCategory NewCategory(long wooCategoryId, string name) => new()
        {
            CompanyId = CompanyId,
            WooCategoryId = wooCategoryId,
            Name = name,
            Slug = $"regression-{wooCategoryId}",
            LastSyncedAtUtc = DateTimeOffset.UtcNow
        };

        public async ValueTask DisposeAsync()
        {
            await transaction.RollbackAsync();
            await transaction.DisposeAsync();
            await Db.DisposeAsync();
        }
    }
}
