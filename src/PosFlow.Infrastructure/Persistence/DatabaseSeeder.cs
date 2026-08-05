using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PosFlow.Domain.Entities;

namespace PosFlow.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    /// <summary>
    /// Applies pending migrations. Call this as an explicit, reviewed
    /// deployment step in production (see ENTERPRISE-READINESS.md /
    /// deploy/README.md) rather than relying on it running implicitly
    /// on every app boot. It stays safe to call on every boot in
    /// dev/staging since a no-op migration set is a no-op call.
    /// </summary>
    public static async Task MigrateAsync(PosFlowDbContext dbContext)
    {
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync();
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync();
        }
    }

    /// <summary>
    /// Seeds a demo tenant/branch/admin/catalog with well-known
    /// credentials. Development/local use only - never call this
    /// against a production database.
    /// </summary>
    public static async Task SeedDemoDataAsync(
        PosFlowDbContext dbContext,
        IPasswordHasher<AppUser> passwordHasher)
    {
        var tenant = await dbContext.Tenants.FirstOrDefaultAsync();

        if (tenant is null)
        {
            tenant = new Tenant { Name = "PosFlow Demo" };
            dbContext.Tenants.Add(tenant);
        }

        var branch = await dbContext.Branches.FirstOrDefaultAsync();

        if (branch is null)
        {
            branch = new Branch
            {
                TenantId = tenant.Id,
                Name = "الفرع الرئيسي",
                Code = "MAIN"
            };

            dbContext.Branches.Add(branch);
        }

        var adminExists = await dbContext.Users
            .AnyAsync(x => x.NormalizedUsername == "ADMIN");

        if (!adminExists)
        {
            var admin = new AppUser
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                Username = "admin",
                NormalizedUsername = "ADMIN",
                DisplayName = "مدير النظام",
                Role = UserRole.Admin,
                IsActive = true
            };

            admin.PasswordHash = passwordHasher.HashPassword(admin, "Admin@123");

            dbContext.Users.Add(admin);
        }

        var hasCategories = await dbContext.ProductCategories.AnyAsync();
        ProductCategory? drinksCategory = null;
        ProductCategory? foodCategory = null;

        if (!hasCategories)
        {
            drinksCategory = new ProductCategory { TenantId = tenant.Id, NameAr = "مشروبات", NameEn = "Drinks" };
            foodCategory = new ProductCategory { TenantId = tenant.Id, NameAr = "مأكولات", NameEn = "Food" };

            dbContext.ProductCategories.AddRange(drinksCategory, foodCategory);
        }

        var hasProducts = await dbContext.Products.AnyAsync();
        List<Product>? seededProducts = null;

        if (!hasProducts)
        {
            seededProducts =
            [
                new Product { TenantId = tenant.Id, CategoryId = drinksCategory?.Id, NameAr = "قهوة", NameEn = "Coffee", Barcode = "1001", Price = 40, TrackStock = true, StockQuantity = 100 },
                new Product { TenantId = tenant.Id, CategoryId = drinksCategory?.Id, NameAr = "شاي", NameEn = "Tea", Barcode = "1002", Price = 25, TrackStock = true, StockQuantity = 100 },
                new Product { TenantId = tenant.Id, CategoryId = drinksCategory?.Id, NameAr = "عصير برتقال", NameEn = "Orange Juice", Barcode = "1003", Price = 50, TrackStock = true, StockQuantity = 60 },
                new Product { TenantId = tenant.Id, CategoryId = foodCategory?.Id, NameAr = "ساندوتش", NameEn = "Sandwich", Barcode = "1004", Price = 75, TrackStock = true, StockQuantity = 40 },
                new Product { TenantId = tenant.Id, CategoryId = drinksCategory?.Id, NameAr = "مياه", NameEn = "Water", Barcode = "1005", Price = 15, TrackStock = true, StockQuantity = 200 },
                new Product { TenantId = tenant.Id, CategoryId = foodCategory?.Id, NameAr = "كرواسون", NameEn = "Croissant", Barcode = "1006", Price = 35, TrackStock = true, StockQuantity = 50 },
                new Product { TenantId = tenant.Id, CategoryId = foodCategory?.Id, NameAr = "بيتزا صغيرة", NameEn = "Mini Pizza", Barcode = "1007", Price = 90, TrackStock = true, StockQuantity = 25 },
            ];

            dbContext.Products.AddRange(seededProducts);
        }

        var cashierExists = await dbContext.Users
            .AnyAsync(x => x.NormalizedUsername == "CASHIER");
        AppUser? cashier = null;

        if (!cashierExists)
        {
            cashier = new AppUser
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                Username = "cashier",
                NormalizedUsername = "CASHIER",
                DisplayName = "كاشير تجريبي",
                Role = UserRole.Cashier,
                IsActive = true
            };

            cashier.PasswordHash = passwordHasher.HashPassword(cashier, "Cashier@123");

            dbContext.Users.Add(cashier);
        }

        var hasCustomers = await dbContext.Customers.AnyAsync();
        List<Customer>? seededCustomers = null;

        if (!hasCustomers)
        {
            seededCustomers =
            [
                new Customer { TenantId = tenant.Id, Name = "أحمد محمود", Phone = "01001234567", Email = "ahmed@example.com", LoyaltyPoints = 120 },
                new Customer { TenantId = tenant.Id, Name = "سارة علي", Phone = "01109876543", Email = "sara@example.com", LoyaltyPoints = 45 },
                new Customer { TenantId = tenant.Id, Name = "محمد عبد الله", Phone = "01223344556" },
            ];

            dbContext.Customers.AddRange(seededCustomers);
        }

        await dbContext.SaveChangesAsync();

        var hasShifts = await dbContext.Shifts.AnyAsync();

        if (!hasShifts)
        {
            // Use whatever products/customers exist for this tenant, whether
            // they were just seeded above or already existed from a prior run.
            var products = seededProducts ?? await dbContext.Products
                .Where(x => x.TenantId == tenant.Id)
                .OrderBy(x => x.Barcode)
                .ToListAsync();
            var customers = seededCustomers ?? await dbContext.Customers
                .Where(x => x.TenantId == tenant.Id)
                .ToListAsync();

            if (products.Count < 4)
            {
                await dbContext.SaveChangesAsync();
                return;
            }

            seededProducts = products;
            seededCustomers = customers;

            var shiftUser = cashier ?? await dbContext.Users.FirstAsync(x => x.NormalizedUsername == "ADMIN");
            var openedAt = DateTime.UtcNow.AddDays(-1);

            var closedShift = new Shift
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                UserId = shiftUser.Id,
                OpeningCash = 500,
                ClosingCash = 950,
                CashSales = 450,
                ExpectedCash = 950,
                CashDifference = 0,
                OpenedAtUtc = openedAt,
                ClosedAtUtc = openedAt.AddHours(8),
                Status = ShiftStatus.Closed
            };

            dbContext.Shifts.Add(closedShift);
            await dbContext.SaveChangesAsync();

            var coffee = seededProducts[0];
            var sandwich = seededProducts[3];
            var water = seededProducts[4];

            var order1 = BuildDemoOrder(
                tenant.Id,
                branch.Id,
                closedShift.Id,
                seededCustomers?.ElementAtOrDefault(0)?.Id,
                "ORD-0001",
                openedAt.AddHours(1),
                (coffee, 2m),
                (water, 1m));

            var order2 = BuildDemoOrder(
                tenant.Id,
                branch.Id,
                closedShift.Id,
                seededCustomers?.ElementAtOrDefault(1)?.Id,
                "ORD-0002",
                openedAt.AddHours(3),
                (sandwich, 1m),
                (coffee, 1m));

            dbContext.Orders.AddRange(order1, order2);
            dbContext.Payments.AddRange(
                new Payment { TenantId = tenant.Id, OrderId = order1.Id, Method = PaymentMethod.Cash, Amount = order1.TotalAmount },
                new Payment { TenantId = tenant.Id, OrderId = order2.Id, Method = PaymentMethod.Card, Amount = order2.TotalAmount, ReferenceNumber = "REF-1002" });

            await dbContext.SaveChangesAsync();
        }
    }

    private static Order BuildDemoOrder(
        Guid tenantId,
        Guid branchId,
        Guid shiftId,
        Guid? customerId,
        string orderNumber,
        DateTime createdAtUtc,
        params (Product Product, decimal Quantity)[] items)
    {
        var order = new Order
        {
            TenantId = tenantId,
            BranchId = branchId,
            ShiftId = shiftId,
            CustomerId = customerId,
            OrderNumber = orderNumber,
            Status = OrderStatus.Completed,
            CreatedAtUtc = createdAtUtc
        };

        decimal subtotal = 0;

        foreach (var (product, quantity) in items)
        {
            var lineTotal = product.Price * quantity;
            subtotal += lineTotal;

            order.Lines.Add(new OrderLine
            {
                TenantId = tenantId,
                OrderId = order.Id,
                ProductId = product.Id,
                ProductName = product.NameAr,
                Quantity = quantity,
                UnitPrice = product.Price,
                LineTotal = lineTotal
            });
        }

        order.Subtotal = subtotal;
        order.TotalAmount = subtotal;

        return order;
    }

    /// <summary>
    /// Production-safe first-run bootstrap: if the database has no
    /// tenant at all, creates exactly one tenant/branch/admin with a
    /// freshly generated random password, logged ONCE at Warning level.
    /// The operator must capture it from the deploy logs and rotate it
    /// immediately via the change-password flow. This intentionally
    /// never writes a fixed/well-known credential to a real database.
    /// Does nothing if a tenant already exists (i.e. runs at most once
    /// per environment, ever).
    /// </summary>
    public static async Task BootstrapProductionAdminIfEmptyAsync(
        PosFlowDbContext dbContext,
        IPasswordHasher<AppUser> passwordHasher,
        ILogger logger)
    {
        var hasAnyTenant = await dbContext.Tenants.AnyAsync();

        if (hasAnyTenant)
        {
            return;
        }

        var tenant = new Tenant { Name = "Default" };
        dbContext.Tenants.Add(tenant);

        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = "Main",
            Code = "MAIN"
        };

        dbContext.Branches.Add(branch);

        var generatedPassword = GenerateStrongPassword();

        var admin = new AppUser
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Username = "admin",
            NormalizedUsername = "ADMIN",
            DisplayName = "System Administrator",
            Role = UserRole.Admin,
            IsActive = true
        };

        admin.PasswordHash = passwordHasher.HashPassword(admin, generatedPassword);
        dbContext.Users.Add(admin);

        await dbContext.SaveChangesAsync();

        logger.LogWarning(
            "First-run bootstrap: created tenant {TenantId} and admin " +
            "user 'admin' with a ONE-TIME generated password: {Password} " +
            "- capture it now and change it immediately. It will not be " +
            "shown again.",
            tenant.Id,
            generatedPassword);
    }

    private static string GenerateStrongPassword()
    {
        const string chars =
            "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%";

        Span<byte> buffer = stackalloc byte[24];
        RandomNumberGenerator.Fill(buffer);

        var password = new char[24];

        for (var i = 0; i < buffer.Length; i++)
        {
            password[i] = chars[buffer[i] % chars.Length];
        }

        return new string(password);
    }
}
