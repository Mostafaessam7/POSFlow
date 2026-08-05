using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PosFlow.Domain.Entities;

namespace PosFlow.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        PosFlowDbContext dbContext,
        IPasswordHasher<AppUser> passwordHasher)
    {
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync();
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync();

        if (tenant is null)
        {
            tenant = new Tenant
            {
                Name = "PosFlow Demo"
            };

            dbContext.Tenants.Add(tenant);
        }

        var branch = await dbContext.Branches
            .FirstOrDefaultAsync();

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

            admin.PasswordHash =
                passwordHasher.HashPassword(
                    admin,
                    "Admin@123");

            dbContext.Users.Add(admin);
        }

        var hasProducts = await dbContext.Products.AnyAsync();

        if (!hasProducts)
        {
            dbContext.Products.AddRange(
                new Product
                {
                    TenantId = tenant.Id,
                    NameAr = "قهوة",
                    NameEn = "Coffee",
                    Barcode = "1001",
                    Price = 40
                },
                new Product
                {
                    TenantId = tenant.Id,
                    NameAr = "شاي",
                    NameEn = "Tea",
                    Barcode = "1002",
                    Price = 25
                },
                new Product
                {
                    TenantId = tenant.Id,
                    NameAr = "عصير برتقال",
                    NameEn = "Orange Juice",
                    Barcode = "1003",
                    Price = 50
                },
                new Product
                {
                    TenantId = tenant.Id,
                    NameAr = "ساندوتش",
                    NameEn = "Sandwich",
                    Barcode = "1004",
                    Price = 75
                },
                new Product
                {
                    TenantId = tenant.Id,
                    NameAr = "مياه",
                    NameEn = "Water",
                    Barcode = "1005",
                    Price = 15
                });
        }

        await dbContext.SaveChangesAsync();
    }
}