using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MTL.Domain.Catalog;
using MTL.Infrastructure.Identity;
using MTL.Infrastructure.Integrations.WooCommerce;
using MTL.Infrastructure.Persistence;
using MTL.Infrastructure.Security;
using Xunit;

namespace MTL.Infrastructure.Tests;

public sealed class WooProductCreationTests
{
    private const long RemoteProductId = 9_876_543_210;

    [Fact]
    public async Task PushNewTrackedProduct_SavesProductAndWooLinkInOneDatabaseSave()
    {
        var fixture = await TestFixture.CreateAsync();
        if (fixture is null) return;

        await using var scope = fixture;
        var product = scope.NewProduct("MTL regression create");
        scope.Db.Products.Add(product);
        var requests = new List<HttpMethod>();
        var handler = new StubHandler(request =>
        {
            requests.Add(request.Method);
            return request.Method == HttpMethod.Get
                ? Json(HttpStatusCode.OK, Array.Empty<object>())
                : Json(HttpStatusCode.Created, WooProduct(product));
        });

        var service = scope.CreateService(handler);
        await service.PushAsync(product);

        Assert.Equal([HttpMethod.Get, HttpMethod.Post], requests);
        Assert.Equal(ProductSyncState.Synced, product.SyncState);
        Assert.Equal(RemoteProductId, product.WooLink?.WooProductId);
        Assert.True(await scope.Db.Products.AnyAsync(item => item.Id == product.Id));
        Assert.True(await scope.Db.WooProductLinks.AnyAsync(item => item.ProductId == product.Id));
    }

    [Fact]
    public async Task PushNewTrackedProduct_RetryRecoversWooProductInsteadOfCreatingDuplicate()
    {
        var fixture = await TestFixture.CreateAsync();
        if (fixture is null) return;

        await using var scope = fixture;
        var product = scope.NewProduct("MTL regression retry");
        scope.Db.Products.Add(product);
        var requests = new List<HttpMethod>();
        var remoteProduct = WooProduct(product);
        var handler = new StubHandler(request =>
        {
            requests.Add(request.Method);
            return Json(HttpStatusCode.OK, request.Method == HttpMethod.Get
                ? new[] { remoteProduct }
                : remoteProduct);
        });

        var service = scope.CreateService(handler);
        await service.PushAsync(product);

        Assert.Equal([HttpMethod.Get, HttpMethod.Put], requests);
        Assert.DoesNotContain(HttpMethod.Post, requests);
        Assert.Equal(RemoteProductId, product.WooLink?.WooProductId);
    }

    [Fact]
    public async Task PushPersistedProduct_AddsNewWooLinkInsteadOfUpdatingMissingRow()
    {
        var fixture = await TestFixture.CreateAsync();
        if (fixture is null) return;

        await using var scope = fixture;
        var product = scope.NewProduct("MTL regression persisted create");
        scope.Db.Products.Add(product);
        await scope.Db.SaveChangesAsync();
        var originalToken = product.ConcurrencyToken;

        var requests = new List<HttpMethod>();
        var handler = new StubHandler(request =>
        {
            requests.Add(request.Method);
            return request.Method == HttpMethod.Get
                ? Json(HttpStatusCode.OK, Array.Empty<object>())
                : Json(HttpStatusCode.Created, WooProduct(product));
        });

        await scope.CreateService(handler).PushAsync(product);

        Assert.Equal([HttpMethod.Get, HttpMethod.Post], requests);
        scope.Db.ChangeTracker.Clear();
        var savedLink = await scope.Db.WooProductLinks.SingleAsync(link => link.ProductId == product.Id);
        Assert.Equal(RemoteProductId, savedLink.WooProductId);
        Assert.Equal(originalToken, await scope.Db.Products
            .Where(item => item.Id == product.Id)
            .Select(item => item.ConcurrencyToken)
            .SingleAsync());
    }

    [Fact]
    public async Task PushPersistedProduct_ConcurrencyAfterWooCreate_ReloadsWithoutPostingDuplicate()
    {
        var fixture = await TestFixture.CreateAsync();
        if (fixture is null) return;

        await using var scope = fixture;
        var product = scope.NewProduct("MTL regression concurrent create");
        product.Status = ProductStatus.Active;
        scope.Db.Products.Add(product);
        await scope.Db.SaveChangesAsync();

        var requests = new List<HttpMethod>();
        var handler = new AsyncStubHandler(async (request, cancellationToken) =>
        {
            requests.Add(request.Method);
            if (request.Method == HttpMethod.Get)
            {
                return Json(HttpStatusCode.OK, Array.Empty<object>());
            }

            await scope.Db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"Products\" SET \"ConcurrencyToken\" = {Guid.NewGuid()} WHERE \"Id\" = {product.Id}",
                cancellationToken);
            return Json(HttpStatusCode.Created, WooProduct(product));
        });

        await scope.CreateService(handler).PushAsync(product);

        Assert.Equal([HttpMethod.Get, HttpMethod.Post], requests);
        Assert.Single(requests, method => method == HttpMethod.Post);
        scope.Db.ChangeTracker.Clear();
        var saved = await scope.Db.Products
            .Include(item => item.WooLink)
            .SingleAsync(item => item.Id == product.Id);
        Assert.Equal(ProductSyncState.Synced, saved.SyncState);
        Assert.Equal(RemoteProductId, saved.WooLink?.WooProductId);
        Assert.NotNull(saved.WooLink?.LastPushedAtUtc);
    }

    [Fact]
    public async Task PushNewTrackedProduct_UnmarkedRecentWooProductStopsBeforeCreatingDuplicate()
    {
        var fixture = await TestFixture.CreateAsync();
        if (fixture is null) return;

        await using var scope = fixture;
        var product = scope.NewProduct("MTL regression orphan guard");
        scope.Db.Products.Add(product);
        var requests = new List<HttpMethod>();
        var handler = new StubHandler(request =>
        {
            requests.Add(request.Method);
            return Json(HttpStatusCode.OK, new[]
            {
                new
                {
                    id = RemoteProductId,
                    name = product.Name,
                    status = "draft",
                    date_created_gmt = DateTimeOffset.UtcNow,
                    categories = Array.Empty<object>(),
                    images = Array.Empty<object>(),
                    meta_data = Array.Empty<object>()
                }
            });
        });

        var service = scope.CreateService(handler);
        var error = await Assert.ThrowsAsync<WooProductRecoveryRequiredException>(
            () => service.PushAsync(product));

        Assert.Equal([HttpMethod.Get], requests);
        Assert.Contains(RemoteProductId.ToString(), error.Message);
    }

    [Fact]
    public async Task PushLinkedUntrackedProduct_ChangesStockStatusWithoutSendingStockQuantity()
    {
        var fixture = await TestFixture.CreateAsync();
        if (fixture is null) return;

        await using var scope = fixture;
        var product = scope.NewProduct("MTL regression stock status");
        product.TrackInventory = false;
        product.StockQuantity = 0;
        product.Availability = ProductAvailability.InStock;
        product.CatalogVisibility = ProductCatalogVisibility.Search;
        product.TagsJson = ProductTagCollection.Serialize([new ProductTag(42, "Laptop văn phòng")]);
        product.WooLink = new WooProductLink
        {
            CompanyId = product.CompanyId,
            ProductId = product.Id,
            Product = product,
            WooProductId = RemoteProductId
        };
        scope.Db.Products.Add(product);

        string? requestBody = null;
        var handler = new AsyncStubHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return Json(HttpStatusCode.OK, WooProduct(product));
        });

        await scope.CreateService(handler).PushAsync(product);

        Assert.NotNull(requestBody);
        using var payload = JsonDocument.Parse(requestBody);
        Assert.False(payload.RootElement.GetProperty("manage_stock").GetBoolean());
        Assert.Equal("instock", payload.RootElement.GetProperty("stock_status").GetString());
        Assert.False(payload.RootElement.TryGetProperty("stock_quantity", out _));
        Assert.Equal("search", payload.RootElement.GetProperty("catalog_visibility").GetString());
        Assert.Equal("Laptop văn phòng", payload.RootElement.GetProperty("tags")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task PushLinkedProduct_WooErrorIncludesRemoteCodeAndMessage()
    {
        var fixture = await TestFixture.CreateAsync();
        if (fixture is null) return;

        await using var scope = fixture;
        var product = scope.NewProduct("MTL regression Woo error");
        product.WooLink = new WooProductLink
        {
            CompanyId = product.CompanyId,
            ProductId = product.Id,
            Product = product,
            WooProductId = RemoteProductId
        };
        scope.Db.Products.Add(product);
        var handler = new StubHandler(_ => Json(HttpStatusCode.BadRequest, new
        {
            code = "woocommerce_rest_invalid_product",
            message = "Trạng thái kho không hợp lệ."
        }));

        var error = await Assert.ThrowsAsync<WooCommerceRequestException>(
            () => scope.CreateService(handler).PushAsync(product));

        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        Assert.Equal("woocommerce_rest_invalid_product", error.ErrorCode);
        Assert.Contains("woocommerce_rest_invalid_product", error.Message);
        Assert.Contains("Trạng thái kho không hợp lệ.", error.Message);
    }

    [Fact]
    public async Task PushProduct_MissingDataProtectionKeyReturnsActionableConfigurationError()
    {
        var fixture = await TestFixture.CreateAsync();
        if (fixture is null) return;

        await using var scope = fixture;
        var product = scope.NewProduct("MTL missing key regression");
        scope.Db.Products.Add(product);

        var error = await Assert.ThrowsAsync<WooCommerceRequestException>(
            () => scope.CreateService(new StubHandler(_ => throw new InvalidOperationException()), new UnreadableProtector())
                .PushAsync(product));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.Equal("woocommerce_credentials_unreadable", error.ErrorCode);
        Assert.Contains("Data Protection", error.Message);
        Assert.DoesNotContain("key ring", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static object WooProduct(Product product) => new
    {
        id = RemoteProductId,
        name = product.Name,
        status = "draft",
        catalog_visibility = product.CatalogVisibility switch
        {
            ProductCatalogVisibility.Catalog => "catalog",
            ProductCatalogVisibility.Search => "search",
            ProductCatalogVisibility.Hidden => "hidden",
            _ => "visible"
        },
        date_created_gmt = DateTimeOffset.UtcNow,
        permalink = $"https://example.test/product/{RemoteProductId}",
        manage_stock = false,
        stock_status = "instock",
        categories = Array.Empty<object>(),
        images = Array.Empty<object>(),
        tags = ProductTagCollection.Deserialize(product.TagsJson).Select(tag => new
        {
            id = tag.WooTagId ?? 0,
            name = tag.Name,
            slug = tag.Name.ToLowerInvariant().Replace(' ', '-')
        }),
        meta_data = new[]
        {
            new { key = "_mtl_v2_product_id", value = product.Id.ToString("D") }
        }
    };

    private static HttpResponseMessage Json(HttpStatusCode status, object value) => new(status)
    {
        Content = JsonContent.Create(value)
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }

    private sealed class AsyncStubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            responseFactory(request, cancellationToken);
    }

    private sealed class PlaintextProtector : ISecretProtector
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string ciphertext) => ciphertext;
    }

    private sealed class UnreadableProtector : ISecretProtector
    {
        public string Protect(string plaintext) => plaintext;

        public string Unprotect(string ciphertext) => throw new SecretProtectionException(
            "Không thể giải mã thông tin bảo mật đã lưu.",
            new CryptographicException("The key {test-key-id} was not found in the key ring."));
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
        public AppDbContext Db { get; } = db;

        public static async Task<TestFixture?> CreateAsync()
        {
            var connectionString = Environment.GetEnvironmentVariable("MTL_TEST_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(connectionString)) return null;

            var discoveryOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var discovery = new AppDbContext(discoveryOptions, new TestCurrentUser(Guid.Empty));
            var companyId = await discovery.Companies.Select(company => company.Id).FirstAsync();

            var currentUser = new TestCurrentUser(companyId);
            var db = new AppDbContext(discoveryOptions, currentUser);
            var transaction = await db.Database.BeginTransactionAsync();
            return new TestFixture(db, currentUser, transaction);
        }

        public Product NewProduct(string name) => new()
        {
            CompanyId = currentUser.CompanyId!.Value,
            Sku = $"MTL-AUTO-{Guid.NewGuid():N}",
            Name = name,
            Status = ProductStatus.Draft,
            Availability = ProductAvailability.InStock,
            TrackInventory = false,
            SyncState = ProductSyncState.PendingPush
        };

        public WooProductSyncService CreateService(HttpMessageHandler handler, ISecretProtector? secretProtector = null) => new(
            new HttpClient(handler),
            Db,
            secretProtector ?? new PlaintextProtector(),
            currentUser);

        public async ValueTask DisposeAsync()
        {
            await transaction.RollbackAsync();
            await transaction.DisposeAsync();
            await Db.DisposeAsync();
        }
    }
}
