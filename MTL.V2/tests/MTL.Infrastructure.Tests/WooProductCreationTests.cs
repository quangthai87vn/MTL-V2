using System.Net;
using System.Net.Http.Json;
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

    private static object WooProduct(Product product) => new
    {
        id = RemoteProductId,
        name = product.Name,
        status = "draft",
        date_created_gmt = DateTimeOffset.UtcNow,
        permalink = $"https://example.test/product/{RemoteProductId}",
        manage_stock = false,
        stock_status = "instock",
        categories = Array.Empty<object>(),
        images = Array.Empty<object>(),
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

    private sealed class PlaintextProtector : ISecretProtector
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string ciphertext) => ciphertext;
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

        public WooProductSyncService CreateService(HttpMessageHandler handler) => new(
            new HttpClient(handler),
            Db,
            new PlaintextProtector(),
            currentUser);

        public async ValueTask DisposeAsync()
        {
            await transaction.RollbackAsync();
            await transaction.DisposeAsync();
            await Db.DisposeAsync();
        }
    }
}
