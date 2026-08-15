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

        var hasSecondBranch = await dbContext.Branches
            .AnyAsync(x => x.TenantId == tenant.Id && x.Code == "MALL");

        if (!hasSecondBranch)
        {
            dbContext.Branches.Add(new Branch
            {
                TenantId = tenant.Id,
                Name = "فرع المول",
                Code = "MALL"
            });
        }

        var hasExchangeRates = await dbContext.ExchangeRates
            .AnyAsync(x => x.TenantId == tenant.Id);

        if (!hasExchangeRates)
        {
            dbContext.ExchangeRates.AddRange(
                new ExchangeRate { TenantId = tenant.Id, CurrencyCode = "USD", RatePerBaseUnit = 0.020m },
                new ExchangeRate { TenantId = tenant.Id, CurrencyCode = "SAR", RatePerBaseUnit = 0.076m },
                new ExchangeRate { TenantId = tenant.Id, CurrencyCode = "EUR", RatePerBaseUnit = 0.019m });
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
        ProductCategory? dessertsCategory = null;

        if (!hasCategories)
        {
            drinksCategory = new ProductCategory { TenantId = tenant.Id, NameAr = "مشروبات", NameEn = "Drinks" };
            foodCategory = new ProductCategory { TenantId = tenant.Id, NameAr = "مأكولات", NameEn = "Food" };
            dessertsCategory = new ProductCategory { TenantId = tenant.Id, NameAr = "حلويات", NameEn = "Desserts" };

            dbContext.ProductCategories.AddRange(drinksCategory, foodCategory, dessertsCategory);
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
                new Product { TenantId = tenant.Id, CategoryId = drinksCategory?.Id, NameAr = "لاتيه", NameEn = "Latte", Barcode = "1008", Price = 55, TrackStock = true, StockQuantity = 80 },
                new Product { TenantId = tenant.Id, CategoryId = drinksCategory?.Id, NameAr = "عصير مانجو", NameEn = "Mango Juice", Barcode = "1009", Price = 45, TrackStock = true, StockQuantity = 60 },
                new Product { TenantId = tenant.Id, CategoryId = foodCategory?.Id, NameAr = "برجر", NameEn = "Burger", Barcode = "1010", Price = 110, TrackStock = true, StockQuantity = 30 },
                new Product { TenantId = tenant.Id, CategoryId = foodCategory?.Id, NameAr = "بطاطس مقلية", NameEn = "French Fries", Barcode = "1011", Price = 35, TrackStock = true, StockQuantity = 70 },
                new Product { TenantId = tenant.Id, CategoryId = foodCategory?.Id, NameAr = "سلطة سيزر", NameEn = "Caesar Salad", Barcode = "1012", Price = 65, TrackStock = true, StockQuantity = 20 },
                new Product { TenantId = tenant.Id, CategoryId = dessertsCategory?.Id, NameAr = "تشيز كيك", NameEn = "Cheesecake", Barcode = "1013", Price = 60, TrackStock = true, StockQuantity = 15 },
                new Product { TenantId = tenant.Id, CategoryId = dessertsCategory?.Id, NameAr = "آيس كريم", NameEn = "Ice Cream", Barcode = "1014", Price = 30, TrackStock = true, StockQuantity = 45 },
                new Product { TenantId = tenant.Id, CategoryId = dessertsCategory?.Id, NameAr = "براونيز", NameEn = "Brownie", Barcode = "1015", Price = 40, TrackStock = true, StockQuantity = 35 },
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
                new Customer { TenantId = tenant.Id, Name = "منى إبراهيم", Phone = "01512345678", Email = "mona@example.com", LoyaltyPoints = 210 },
                new Customer { TenantId = tenant.Id, Name = "خالد يوسف", Phone = "01098765432", Email = "khaled@example.com", LoyaltyPoints = 30 },
                new Customer { TenantId = tenant.Id, Name = "ياسمين حسن", Phone = "01234567890", Email = "yasmin@example.com", LoyaltyPoints = 85 },
                new Customer { TenantId = tenant.Id, Name = "عمر شريف", Phone = "01187654321" },
                new Customer { TenantId = tenant.Id, Name = "نور الدين فتحي", Phone = "01055566677", Email = "nour@example.com", LoyaltyPoints = 15 },
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

            var coffee = seededProducts[0];
            var tea = seededProducts[1];
            var juice = seededProducts[2];
            var sandwich = seededProducts[3];
            var water = seededProducts[4];
            var croissant = seededProducts[5];
            var pizza = seededProducts[6];
            var burger = seededProducts.Count > 9 ? seededProducts[9] : sandwich;
            var fries = seededProducts.Count > 10 ? seededProducts[10] : water;
            var cheesecake = seededProducts.Count > 12 ? seededProducts[12] : croissant;

            var orders = new List<Order>();
            var payments = new List<Payment>();
            var orderSeq = 1;

            // Three closed shifts on three separate past days (kept
            // deliberately out of "today" so reports/tests that filter
            // on the current day stay predictable) - covers cash,
            // card, and split-payment checkouts, an order with no
            // linked customer, and one voided order.
            for (var dayOffset = 3; dayOffset >= 1; dayOffset--)
            {
                var openedAt = DateTime.UtcNow.AddDays(-dayOffset).Date.AddHours(9);

                var shift = new Shift
                {
                    TenantId = tenant.Id,
                    BranchId = branch.Id,
                    UserId = shiftUser.Id,
                    OpeningCash = 500,
                    ClosingCash = 500 + (dayOffset * 150),
                    CashSales = dayOffset * 150,
                    ExpectedCash = 500 + (dayOffset * 150),
                    CashDifference = 0,
                    OpenedAtUtc = openedAt,
                    ClosedAtUtc = openedAt.AddHours(8),
                    Status = ShiftStatus.Closed
                };

                dbContext.Shifts.Add(shift);
                await dbContext.SaveChangesAsync();

                var order1 = BuildDemoOrder(
                    tenant.Id,
                    branch.Id,
                    shift.Id,
                    seededCustomers?.ElementAtOrDefault(orderSeq % seededCustomers.Count)?.Id,
                    $"ORD-{orderSeq:D4}",
                    openedAt.AddHours(1),
                    (coffee, 2m),
                    (water, 1m));
                payments.Add(new Payment { TenantId = tenant.Id, OrderId = order1.Id, Method = PaymentMethod.Cash, Amount = order1.TotalAmount });
                orders.Add(order1);
                orderSeq++;

                var order2 = BuildDemoOrder(
                    tenant.Id,
                    branch.Id,
                    shift.Id,
                    seededCustomers?.ElementAtOrDefault((orderSeq + 2) % seededCustomers.Count)?.Id,
                    $"ORD-{orderSeq:D4}",
                    openedAt.AddHours(3),
                    (sandwich, 1m),
                    (tea, 1m));
                payments.Add(new Payment { TenantId = tenant.Id, OrderId = order2.Id, Method = PaymentMethod.Card, Amount = order2.TotalAmount, ReferenceNumber = $"REF-{1000 + orderSeq}" });
                orders.Add(order2);
                orderSeq++;

                // No linked customer - an anonymous walk-in sale, split
                // across cash + card to exercise the multi-payment path.
                var order3 = BuildDemoOrder(
                    tenant.Id,
                    branch.Id,
                    shift.Id,
                    null,
                    $"ORD-{orderSeq:D4}",
                    openedAt.AddHours(5),
                    (pizza, 1m),
                    (juice, 2m),
                    (croissant, 1m));
                var halfTotal = Math.Round(order3.TotalAmount / 2, 2);
                payments.Add(new Payment { TenantId = tenant.Id, OrderId = order3.Id, Method = PaymentMethod.Cash, Amount = halfTotal });
                payments.Add(new Payment { TenantId = tenant.Id, OrderId = order3.Id, Method = PaymentMethod.Card, Amount = order3.TotalAmount - halfTotal, ReferenceNumber = $"REF-{2000 + orderSeq}" });
                orders.Add(order3);
                orderSeq++;

                if (dayOffset == 2)
                {
                    // One voided order, on the middle day only, so the
                    // dashboard/reports screens have a real example of
                    // a cancelled sale to show.
                    var voidedOrder = BuildDemoOrder(
                        tenant.Id,
                        branch.Id,
                        shift.Id,
                        seededCustomers?.ElementAtOrDefault(orderSeq % seededCustomers.Count)?.Id,
                        $"ORD-{orderSeq:D4}",
                        openedAt.AddHours(6),
                        (burger, 1m),
                        (fries, 1m));

                    voidedOrder.Status = OrderStatus.Cancelled;
                    voidedOrder.VoidReason = "طلب العميل إلغاء الأوردر بعد الدفع بالخطأ";
                    voidedOrder.VoidedAtUtc = openedAt.AddHours(6).AddMinutes(10);

                    payments.Add(new Payment { TenantId = tenant.Id, OrderId = voidedOrder.Id, Method = PaymentMethod.Cash, Amount = voidedOrder.TotalAmount });
                    orders.Add(voidedOrder);
                    orderSeq++;
                }

                if (dayOffset == 1 && cheesecake != croissant)
                {
                    var dessertOrder = BuildDemoOrder(
                        tenant.Id,
                        branch.Id,
                        shift.Id,
                        seededCustomers?.ElementAtOrDefault(orderSeq % seededCustomers.Count)?.Id,
                        $"ORD-{orderSeq:D4}",
                        openedAt.AddHours(7),
                        (cheesecake, 2m));
                    payments.Add(new Payment { TenantId = tenant.Id, OrderId = dessertOrder.Id, Method = PaymentMethod.Card, Amount = dessertOrder.TotalAmount, ReferenceNumber = $"REF-{3000 + orderSeq}" });
                    orders.Add(dessertOrder);
                    orderSeq++;
                }
            }

            dbContext.Orders.AddRange(orders);
            dbContext.Payments.AddRange(payments);

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
