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

        var hasProducts = await dbContext.Products.AnyAsync();

        if (!hasProducts)
        {
            dbContext.Products.AddRange(
                new Product { TenantId = tenant.Id, NameAr = "قهوة", NameEn = "Coffee", Barcode = "1001", Price = 40 },
                new Product { TenantId = tenant.Id, NameAr = "شاي", NameEn = "Tea", Barcode = "1002", Price = 25 },
                new Product { TenantId = tenant.Id, NameAr = "عصير برتقال", NameEn = "Orange Juice", Barcode = "1003", Price = 50 },
                new Product { TenantId = tenant.Id, NameAr = "ساندوتش", NameEn = "Sandwich", Barcode = "1004", Price = 75 },
                new Product { TenantId = tenant.Id, NameAr = "مياه", NameEn = "Water", Barcode = "1005", Price = 15 });
        }

        await dbContext.SaveChangesAsync();
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
