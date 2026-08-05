using Microsoft.EntityFrameworkCore;
using PosFlow.Application.Common;
using PosFlow.Domain.Entities;

namespace PosFlow.Infrastructure.Persistence;

/// <summary>
/// Tenant isolation is enforced in two independent layers on purpose:
/// (1) every service still filters explicitly by TenantId (defence in
/// depth, and required for the composite unique indexes below), and
/// (2) the global query filter here, driven by
/// <see cref="ICurrentTenantProvider"/>, which protects any query -
/// current or future - that forgets step 1. Losing either layer must
/// not cause cross-tenant data to leak.
/// </summary>
public sealed class PosFlowDbContext(
    DbContextOptions<PosFlowDbContext> options,
    ICurrentTenantProvider currentTenant)
    : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("pos");

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.RowVersion)
                .IsRowVersion();
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Code })
                .IsUnique();

            entity.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Code)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.RowVersion)
                .IsRowVersion();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Barcode });

            entity.Property(x => x.NameAr)
                .HasMaxLength(250)
                .IsRequired();

            entity.Property(x => x.NameEn)
                .HasMaxLength(250);

            entity.Property(x => x.Barcode)
                .HasMaxLength(100);

            entity.Property(x => x.Price)
                .HasPrecision(19, 4);

            entity.Property(x => x.StockQuantity)
                .HasPrecision(19, 4);

            entity.Property(x => x.RowVersion)
                .IsRowVersion();

            entity.HasOne<ProductCategory>()
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.NameAr });

            entity.Property(x => x.NameAr)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.NameEn)
                .HasMaxLength(150);

            entity.Property(x => x.RowVersion)
                .IsRowVersion();
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.OrderNumber })
                .IsUnique();

            // Covers GenerateOrderNumberAsync's daily count and
            // ReportService's daily summary - both filter by
            // tenant(+branch) and range on CreatedAtUtc.
            entity.HasIndex(x => new
            {
                x.TenantId,
                x.BranchId,
                x.CreatedAtUtc
            });

            entity.Property(x => x.Subtotal).HasPrecision(19, 4);
            entity.Property(x => x.DiscountAmount).HasPrecision(19, 4);
            entity.Property(x => x.TaxAmount).HasPrecision(19, 4);
            entity.Property(x => x.TotalAmount).HasPrecision(19, 4);

            entity.Property(x => x.VoidReason)
                .HasMaxLength(500);

            entity.Property(x => x.RowVersion)
                .IsRowVersion();
        });

        modelBuilder.Entity<OrderLine>(entity =>
        {
            entity.Property(x => x.Quantity).HasPrecision(19, 4);
            entity.Property(x => x.UnitPrice).HasPrecision(19, 4);
            entity.Property(x => x.DiscountAmount).HasPrecision(19, 4);
            entity.Property(x => x.TaxAmount).HasPrecision(19, 4);
            entity.Property(x => x.LineTotal).HasPrecision(19, 4);

            entity.Property(x => x.RowVersion)
                .IsRowVersion();
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.Property(x => x.Amount)
                .HasPrecision(19, 4);

            entity.Property(x => x.ReferenceNumber)
                .HasMaxLength(200);

            entity.Property(x => x.RowVersion)
                .IsRowVersion();
        });

        modelBuilder.Entity<Shift>(entity =>
        {
            entity.ToTable("Shifts", "pos");

            entity.Property(x => x.OpeningCash)
                .HasPrecision(19, 4);

            entity.Property(x => x.ClosingCash)
                .HasPrecision(19, 4);

            entity.Property(x => x.CashSales)
                .HasPrecision(19, 4);

            entity.Property(x => x.ExpectedCash)
                .HasPrecision(19, 4);

            entity.Property(x => x.CashDifference)
                .HasPrecision(19, 4);

            entity.Property(x => x.RowVersion)
                .IsRowVersion();

            entity.HasIndex(x => new
            {
                x.TenantId,
                x.BranchId,
                x.UserId
            })
                .IsUnique()
                .HasFilter("[Status] = 1");

            entity.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("Users", "auth");

            entity.HasIndex(x => x.NormalizedUsername)
                .IsUnique();

            entity.Property(x => x.Username)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.NormalizedUsername)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Email)
                .HasMaxLength(256);

            entity.Property(x => x.DisplayName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.PasswordHash)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(x => x.Role)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(x => x.RowVersion)
                .IsRowVersion();

            entity.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens", "auth");

            entity.HasIndex(x => x.TokenHash)
                .IsUnique();

            entity.HasIndex(x => x.UserId);

            entity.Property(x => x.TokenHash)
                .HasMaxLength(200)
                .IsRequired();
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("PasswordResetTokens", "auth");

            entity.HasIndex(x => x.TokenHash)
                .IsUnique();

            entity.HasIndex(x => x.UserId);

            entity.Property(x => x.TokenHash)
                .HasMaxLength(200)
                .IsRequired();
        });

        modelBuilder.Entity<Branch>()
            .HasQueryFilter(x =>
                !currentTenant.TenantId.HasValue ||
                x.TenantId == currentTenant.TenantId);

        modelBuilder.Entity<Product>()
            .HasQueryFilter(x =>
                !currentTenant.TenantId.HasValue ||
                x.TenantId == currentTenant.TenantId);

        modelBuilder.Entity<ProductCategory>()
            .HasQueryFilter(x =>
                !currentTenant.TenantId.HasValue ||
                x.TenantId == currentTenant.TenantId);

        modelBuilder.Entity<Order>()
            .HasQueryFilter(x =>
                !currentTenant.TenantId.HasValue ||
                x.TenantId == currentTenant.TenantId);

        modelBuilder.Entity<OrderLine>()
            .HasQueryFilter(x =>
                !currentTenant.TenantId.HasValue ||
                x.TenantId == currentTenant.TenantId);

        modelBuilder.Entity<Payment>()
            .HasQueryFilter(x =>
                !currentTenant.TenantId.HasValue ||
                x.TenantId == currentTenant.TenantId);

        modelBuilder.Entity<Shift>()
            .HasQueryFilter(x =>
                !currentTenant.TenantId.HasValue ||
                x.TenantId == currentTenant.TenantId);

        modelBuilder.Entity<AppUser>()
            .HasQueryFilter(x =>
                !currentTenant.TenantId.HasValue ||
                x.TenantId == currentTenant.TenantId);

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        BumpRowVersionsOnModifiedEntities();

        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        BumpRowVersionsOnModifiedEntities();

        return base.SaveChanges();
    }

    /// <summary>
    /// SQL Server's rowversion columns auto-increment on their own
    /// (EF just reads the DB-computed value back afterwards, so this
    /// assignment is harmless there - it gets overwritten). The EF
    /// Core InMemory provider has no equivalent auto-increment for
    /// .IsRowVersion() properties though, so without this, optimistic
    /// concurrency checks against InMemory (used by unit tests) never
    /// actually see the token change and never detect a real
    /// conflict. Doing it centrally here, instead of per-service,
    /// guarantees every entity with a RowVersion gets this for free.
    /// </summary>
    private void BumpRowVersionsOnModifiedEntities()
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
            }
        }
    }
}