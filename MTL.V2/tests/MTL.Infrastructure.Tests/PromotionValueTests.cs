using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MTL.Api.Controllers;
using MTL.Domain.Sales;
using MTL.Infrastructure.Identity;
using MTL.Infrastructure.Persistence;
using Xunit;

namespace MTL.Infrastructure.Tests;

public sealed class PromotionValueTests
{
    [Fact]
    public async Task CreateAndUpdate_AcceptZeroValue_AndRejectNegativeValue()
    {
        var fixture = await TestFixture.CreateAsync();
        if (fixture is null) return;

        await using var scope = fixture;
        var controller = new SalesController(scope.Db, scope.CurrentUser);
        var request = new SavePromotionRequest(
            Name: $"Khuyến mãi 0 đồng {Guid.NewGuid():N}",
            Description: "Chương trình dùng để kiểm tra giá trị mặc định bằng 0.",
            DiscountType: DiscountType.FixedAmount,
            Value: 0,
            MinimumOrderAmount: 0,
            MaximumDiscountAmount: null,
            StartsAtUtc: null,
            EndsAtUtc: null);

        var createResult = await controller.CreatePromotion(request, CancellationToken.None);

        var createOk = Assert.IsType<OkObjectResult>(createResult.Result);
        var created = Assert.IsType<PromotionItem>(createOk.Value);
        Assert.Equal(0, created.Value);
        Assert.Equal(0, (await scope.Db.Promotions.SingleAsync(item => item.Id == created.Id)).Value);

        var updateResult = await controller.UpdatePromotion(
            created.Id,
            request with { Name = $"Khuyến mãi cập nhật {Guid.NewGuid():N}", Value = 0 },
            CancellationToken.None);
        var updateOk = Assert.IsType<OkObjectResult>(updateResult.Result);
        Assert.Equal(0, Assert.IsType<PromotionItem>(updateOk.Value).Value);

        var invalidResult = await controller.UpdatePromotion(
            created.Id,
            request with { Value = -1 },
            CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(invalidResult.Result);
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
        public TestCurrentUser CurrentUser { get; } = currentUser;

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

        public async ValueTask DisposeAsync()
        {
            await transaction.RollbackAsync();
            await transaction.DisposeAsync();
            await Db.DisposeAsync();
        }
    }
}
