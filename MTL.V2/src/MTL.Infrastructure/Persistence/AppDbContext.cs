using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MTL.Domain.Auditing;
using MTL.Domain.Catalog;
using MTL.Domain.Common;
using MTL.Domain.Companies;
using MTL.Domain.Crm;
using MTL.Domain.Integrations;
using MTL.Domain.Inventory;
using MTL.Domain.Purchasing;
using MTL.Domain.Sales;
using MTL.Infrastructure.Identity;

namespace MTL.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentUser currentUser)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<WooCategory> WooCategories => Set<WooCategory>();
    public DbSet<ProductCategoryAssignment> ProductCategoryAssignments => Set<ProductCategoryAssignment>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<WooProductLink> WooProductLinks => Set<WooProductLink>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceLine> SalesInvoiceLines => Set<SalesInvoiceLine>();
    public DbSet<SalesDiscountLine> SalesDiscountLines => Set<SalesDiscountLine>();
    public DbSet<SalesPayment> SalesPayments => Set<SalesPayment>();
    public DbSet<PaymentAccount> PaymentAccounts => Set<PaymentAccount>();
    public DbSet<InstallmentProvider> InstallmentProviders => Set<InstallmentProvider>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<RepairOrder> RepairOrders => Set<RepairOrder>();
    public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();
    public DbSet<SalesInvoiceServiceType> SalesInvoiceServiceTypes => Set<SalesInvoiceServiceType>();
    public DbSet<RepairOrderServiceType> RepairOrderServiceTypes => Set<RepairOrderServiceType>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceLine> PurchaseInvoiceLines => Set<PurchaseInvoiceLine>();
    public DbSet<PurchasePayment> PurchasePayments => Set<PurchasePayment>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<ProductSerial> ProductSerials => Set<ProductSerial>();
    public DbSet<WooCommerceConnection> WooCommerceConnections => Set<WooCommerceConnection>();
    public DbSet<SyncJob> SyncJobs => Set<SyncJob>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    private Guid? CurrentCompanyId => currentUser.CompanyId;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Company>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Code).HasMaxLength(50);
            entity.Property(x => x.Currency).HasMaxLength(3);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.FullName).HasMaxLength(200);
            entity.HasIndex(x => new { x.CompanyId, x.Email });
        });

        builder.Entity<Product>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.HasIndex(x => new { x.CompanyId, x.Sku }).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.HasIndex(x => new { x.CompanyId, x.Name });
            entity.Property(x => x.Sku).HasMaxLength(100);
            entity.Property(x => x.Name).HasMaxLength(500);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.SyncState).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Availability).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.CatalogVisibility).HasConversion<string>().HasMaxLength(30).HasDefaultValue(ProductCatalogVisibility.Visible);
            entity.Property(x => x.TagsJson).HasColumnType("jsonb").HasDefaultValue("[]");
            entity.Property(x => x.SalePrice).HasPrecision(18, 2);
            entity.Property(x => x.RegularPrice).HasPrecision(18, 2);
            entity.Property(x => x.CostPrice).HasPrecision(18, 2);
            entity.Property(x => x.StockQuantity).HasPrecision(18, 3);
            entity.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        });

        builder.Entity<ProductVariant>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.HasIndex(x => new { x.CompanyId, x.Sku }).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.Property(x => x.SalePrice).HasPrecision(18, 2);
            entity.Property(x => x.StockQuantity).HasPrecision(18, 3);
        });

        builder.Entity<WooCategory>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.HasIndex(x => new { x.CompanyId, x.WooCategoryId }).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.HasIndex(x => new { x.CompanyId, x.ParentWooCategoryId });
            entity.Property(x => x.Name).HasMaxLength(300);
            entity.Property(x => x.Slug).HasMaxLength(300);
        });

        builder.Entity<ProductCategoryAssignment>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.HasIndex(x => new { x.CompanyId, x.ProductId, x.WooCategoryId }).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.HasIndex(x => new { x.CompanyId, x.WooCategoryId });
            entity.Property(x => x.Name).HasMaxLength(300);
            entity.HasOne(x => x.Product).WithMany(x => x.CategoryAssignments).HasForeignKey(x => x.ProductId);
        });

        builder.Entity<ProductImage>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.HasIndex(x => new { x.CompanyId, x.ProductId, x.Position });
            entity.HasIndex(x => new { x.CompanyId, x.WooMediaId });
            entity.Property(x => x.Url).HasMaxLength(2000);
            entity.Property(x => x.ThumbnailUrl).HasMaxLength(2000);
            entity.Property(x => x.Name).HasMaxLength(500);
            entity.Property(x => x.AltText).HasMaxLength(1000);
            entity.HasOne(x => x.Product).WithMany(x => x.Images).HasForeignKey(x => x.ProductId);
        });

        builder.Entity<WooProductLink>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.HasIndex(x => new { x.CompanyId, x.WooProductId }).IsUnique();
            entity.HasIndex(x => x.ProductId).IsUnique();
            entity.HasOne(x => x.Product).WithOne(x => x.WooLink).HasForeignKey<WooProductLink>(x => x.ProductId);
        });

        ConfigureCompanyOwned(builder.Entity<Customer>(), x => x.Code, 50);
        ConfigureCompanyOwned(builder.Entity<Supplier>(), x => x.Code, 50);
        ConfigureCompanyOwned(builder.Entity<SalesInvoice>(), x => x.Number, 80);
        ConfigureCompanyOwned(builder.Entity<PurchaseInvoice>(), x => x.Number, 80);

        builder.Entity<SalesInvoice>(entity =>
        {
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.RepairStatus).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.PromotionDiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.VoucherDiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.PrintNotes).HasMaxLength(2000);
            entity.HasOne(x => x.InstallmentPaymentAccount).WithMany().HasForeignKey(x => x.InstallmentPaymentAccountId).OnDelete(DeleteBehavior.SetNull);
            ConfigureMoney(entity);
            entity.Property(x => x.LegacySource).HasMaxLength(50);
            entity.Property(x => x.LegacyId).HasMaxLength(150);
            entity.HasIndex(x => new { x.CompanyId, x.LegacySource, x.LegacyId })
                .IsUnique()
                .HasFilter("\"LegacySource\" IS NOT NULL AND \"LegacyId\" IS NOT NULL AND \"IsDeleted\" = false");
        });

        builder.Entity<Customer>(entity =>
        {
            entity.Property(x => x.CitizenId).HasMaxLength(30);
            entity.Property(x => x.LegacySource).HasMaxLength(50);
            entity.Property(x => x.LegacyId).HasMaxLength(150);
            entity.HasIndex(x => new { x.CompanyId, x.CitizenId })
                .HasFilter("\"CitizenId\" IS NOT NULL AND \"IsDeleted\" = false");
            entity.HasIndex(x => new { x.CompanyId, x.LegacySource, x.LegacyId })
                .IsUnique()
                .HasFilter("\"LegacySource\" IS NOT NULL AND \"LegacyId\" IS NOT NULL AND \"IsDeleted\" = false");
        });

        builder.Entity<Supplier>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(300);
            entity.Property(x => x.TaxCode).HasMaxLength(30);
            entity.Property(x => x.LegacySource).HasMaxLength(50);
            entity.Property(x => x.LegacyId).HasMaxLength(150);
            entity.HasIndex(x => new { x.CompanyId, x.LegacySource, x.LegacyId })
                .IsUnique()
                .HasFilter("\"LegacySource\" IS NOT NULL AND \"LegacyId\" IS NOT NULL AND \"IsDeleted\" = false");
        });

        builder.Entity<SalesInvoiceLine>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);
            entity.Property(x => x.UnitName).HasMaxLength(30);
            entity.Property(x => x.WarrantyPeriod).HasMaxLength(50);
            entity.HasOne(x => x.SalesInvoice).WithMany(x => x.Lines).HasForeignKey(x => x.SalesInvoiceId);
        });

        builder.Entity<SalesDiscountLine>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.HasIndex(x => new { x.CompanyId, x.SalesInvoiceId });
            entity.Property(x => x.Source).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Name).HasMaxLength(300);
            entity.Property(x => x.Code).HasMaxLength(100);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasOne(x => x.SalesInvoice).WithMany(x => x.Discounts).HasForeignKey(x => x.SalesInvoiceId);
        });

        builder.Entity<PaymentAccount>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.HasIndex(x => new { x.CompanyId, x.IsActive, x.DisplayOrder });
            entity.Property(x => x.Name).HasMaxLength(150);
            entity.Property(x => x.BankName).HasMaxLength(150);
            entity.Property(x => x.AccountNumber).HasMaxLength(100);
            entity.Property(x => x.AccountHolder).HasMaxLength(200);
        });

        builder.Entity<InstallmentProvider>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.HasIndex(x => new { x.CompanyId, x.IsActive, x.DisplayOrder });
            entity.Property(x => x.Name).HasMaxLength(150);
            entity.Property(x => x.Code).HasMaxLength(80);
        });

        builder.Entity<SalesPayment>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.HasIndex(x => new { x.CompanyId, x.PaidAtUtc });
            entity.Property(x => x.Method).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasOne(x => x.SalesInvoice).WithMany(x => x.Payments).HasForeignKey(x => x.SalesInvoiceId);
            entity.HasOne(x => x.PaymentAccount).WithMany().HasForeignKey(x => x.PaymentAccountId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Promotion>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.HasIndex(x => new { x.CompanyId, x.IsActive });
            entity.Property(x => x.Name).HasMaxLength(250);
            entity.Property(x => x.DiscountType).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Value).HasPrecision(18, 2);
            entity.Property(x => x.MinimumOrderAmount).HasPrecision(18, 2);
            entity.Property(x => x.MaximumDiscountAmount).HasPrecision(18, 2);
        });

        builder.Entity<Voucher>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.Property(x => x.Code).HasMaxLength(80);
            entity.Property(x => x.Name).HasMaxLength(250);
            entity.Property(x => x.DiscountType).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Value).HasPrecision(18, 2);
            entity.Property(x => x.MinimumOrderAmount).HasPrecision(18, 2);
            entity.Property(x => x.MaximumDiscountAmount).HasPrecision(18, 2);
        });

        builder.Entity<RepairOrder>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.HasIndex(x => x.SalesInvoiceId);
            entity.Property(x => x.DeviceModel).HasMaxLength(300);
            entity.Property(x => x.SerialNumber).HasMaxLength(200);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.EstimatedCost).HasPrecision(18, 2);
            entity.HasOne(x => x.SalesInvoice).WithMany(x => x.Repairs).HasForeignKey(x => x.SalesInvoiceId);
        });

        builder.Entity<ServiceType>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.HasIndex(x => new { x.CompanyId, x.IsActive, x.DisplayOrder });
            entity.Property(x => x.Name).HasMaxLength(150);
            entity.Property(x => x.Code).HasMaxLength(80);
        });

        builder.Entity<SalesInvoiceServiceType>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.HasIndex(x => new { x.CompanyId, x.SalesInvoiceId, x.ServiceTypeId }).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.HasOne(x => x.SalesInvoice).WithMany(x => x.ServiceTypes).HasForeignKey(x => x.SalesInvoiceId);
            entity.HasOne(x => x.ServiceType).WithMany(x => x.SalesInvoices).HasForeignKey(x => x.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RepairOrderServiceType>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.HasIndex(x => new { x.CompanyId, x.RepairOrderId, x.ServiceTypeId }).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasOne(x => x.RepairOrder).WithMany(x => x.ServiceTypes).HasForeignKey(x => x.RepairOrderId);
            entity.HasOne(x => x.ServiceType).WithMany(x => x.RepairOrders).HasForeignKey(x => x.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PurchaseInvoice>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.SupplierInvoiceNumber).HasMaxLength(100);
            entity.Property(x => x.SupplierInvoiceSeries).HasMaxLength(100);
            entity.Property(x => x.WarehouseReceiptNumber).HasMaxLength(100);
            entity.Property(x => x.LegacySource).HasMaxLength(50);
            entity.Property(x => x.LegacyId).HasMaxLength(150);
            entity.HasIndex(x => new { x.CompanyId, x.LegacySource, x.LegacyId })
                .IsUnique()
                .HasFilter("\"LegacySource\" IS NOT NULL AND \"LegacyId\" IS NOT NULL AND \"IsDeleted\" = false");
            entity.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            ConfigureMoney(entity);
        });

        builder.Entity<PurchaseInvoiceLine>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.HasIndex(x => new { x.CompanyId, x.PurchaseInvoiceId });
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.ReceivedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitName).HasMaxLength(30);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.VatRate).HasPrecision(5, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);
            entity.HasOne(x => x.PurchaseInvoice).WithMany(x => x.Lines).HasForeignKey(x => x.PurchaseInvoiceId);
        });

        builder.Entity<PurchasePayment>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.HasIndex(x => new { x.CompanyId, x.PaidAtUtc });
            entity.Property(x => x.Method).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasOne(x => x.PurchaseInvoice).WithMany(x => x.Payments).HasForeignKey(x => x.PurchaseInvoiceId);
            entity.HasOne(x => x.PaymentAccount).WithMany().HasForeignKey(x => x.PaymentAccountId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<StockMovement>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.HasIndex(x => new { x.CompanyId, x.ProductId, x.OccurredAtUtc });
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
        });

        builder.Entity<ProductSerial>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.HasIndex(x => new { x.CompanyId, x.SerialNumber }).IsUnique().HasFilter("\"IsDeleted\" = false");
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.CostPrice).HasPrecision(18, 2);
        });

        builder.Entity<WooCommerceConnection>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.HasIndex(x => x.CompanyId).IsUnique().HasFilter("\"IsDeleted\" = false");
        });

        builder.Entity<SyncJob>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
            entity.HasIndex(x => new { x.CompanyId, x.ExternalId }).IsUnique().HasFilter("\"ExternalId\" IS NOT NULL");
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CompanyId, x.OccurredAtUtc });
            entity.Property(x => x.Action).HasMaxLength(50);
            entity.Property(x => x.EntityType).HasMaxLength(200);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AddAuditEntries();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void AddAuditEntries()
    {
        var now = DateTimeOffset.UtcNow;
        var auditableEntries = ChangeTracker.Entries<AuditableEntity>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in auditableEntries)
        {
            if (entry.State == EntityState.Deleted &&
                entry.Entity is ProductCategoryAssignment assignment &&
                !ProductCategoryAssignments.IgnoreQueryFilters().Any(x => x.Id == assignment.Id))
            {
                // During a Woo batch import, duplicate SKUs can attach and replace a category
                // assignment before that new entity has ever been inserted. It is an in-memory
                // orphan, not a persisted row that should be converted into a soft delete.
                assignment.Product?.CategoryAssignments.Remove(assignment);
                assignment.Product = null!;
                assignment.ProductId = Guid.Empty;
                entry.State = EntityState.Detached;
                continue;
            }

            if (entry.State == EntityState.Deleted &&
                entry.Entity is ProductImage image &&
                !ProductImages.IgnoreQueryFilters().Any(x => x.Id == image.Id))
            {
                image.Product?.Images.Remove(image);
                image.Product = null!;
                image.ProductId = Guid.Empty;
                entry.State = EntityState.Detached;
                continue;
            }

            var action = entry.State.ToString();
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.CreatedByUserId = currentUser.UserId;
            }

            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAtUtc = now;
                action = "SoftDeleted";
            }

            entry.Entity.UpdatedAtUtc = now;
            entry.Entity.UpdatedByUserId = currentUser.UserId;

            var companyId = entry.Entity is CompanyOwnedEntity owned
                ? owned.CompanyId
                : currentUser.CompanyId;

            if (!companyId.HasValue || companyId == Guid.Empty)
            {
                continue;
            }

            var changes = entry.Properties
                .Where(x => x.IsModified || entry.State == EntityState.Added)
                .Where(x => !IsSensitive(x.Metadata.Name))
                .ToDictionary(
                    x => x.Metadata.Name,
                    x => new { Old = x.OriginalValue, New = x.CurrentValue });

            AuditLogs.Add(new AuditLog
            {
                CompanyId = companyId.Value,
                UserId = currentUser.UserId,
                Action = action,
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = entry.Entity.Id.ToString(),
                ChangesJson = JsonSerializer.Serialize(changes),
                OccurredAtUtc = now
            });
        }
    }

    private static bool IsSensitive(string propertyName) =>
        propertyName.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Ciphertext", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("SecurityStamp", StringComparison.OrdinalIgnoreCase);

    private void ConfigureCompanyOwned<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity,
        System.Linq.Expressions.Expression<Func<TEntity, string>> uniqueProperty,
        int maxLength)
        where TEntity : CompanyOwnedEntity
    {
        entity.HasQueryFilter(x => !x.IsDeleted && CurrentCompanyId.HasValue && x.CompanyId == CurrentCompanyId.Value);
        entity.HasIndex(nameof(CompanyOwnedEntity.CompanyId), GetMemberName(uniqueProperty)).IsUnique();
        entity.Property(uniqueProperty).HasMaxLength(maxLength);
    }

    private static string GetMemberName<TEntity>(System.Linq.Expressions.Expression<Func<TEntity, string>> expression) =>
        expression.Body is System.Linq.Expressions.MemberExpression member
            ? member.Member.Name
            : throw new InvalidOperationException("Expected a member expression.");

    private static void ConfigureMoney<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity)
        where TEntity : class
    {
        entity.Property("Subtotal").HasPrecision(18, 2);
        entity.Property("DiscountAmount").HasPrecision(18, 2);
        entity.Property("TaxAmount").HasPrecision(18, 2);
        entity.Property("TotalAmount").HasPrecision(18, 2);
        entity.Property("PaidAmount").HasPrecision(18, 2);
    }
}

