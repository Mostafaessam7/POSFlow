using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PosFlow.Api.Tests.TestHelpers;
using PosFlow.Application.Branches;
using PosFlow.Application.Customers;
using PosFlow.Application.Products;
using PosFlow.Domain.Entities;
using PosFlow.Infrastructure.Persistence;
using Xunit;

namespace PosFlow.Api.Tests;

/// <summary>
/// End-to-end proof, over real HTTP, that an authenticated user from
/// one tenant cannot read or modify another tenant's resources by
/// guessing/enumerating their ids. TenantIsolationTests (Application
/// layer) already proves the EF Core global query filter itself works
/// against the DbContext directly; this class proves the SAME
/// guarantee holds through the full stack a real attacker would
/// actually hit - real controllers, real auth, real routing - for
/// every resource type that exposes a by-id GET or PUT.
///
/// The "foreign" tenant's data is seeded directly through the
/// DbContext (there's no self-service tenant-signup endpoint to call
/// instead) - resolving PosFlowDbContext outside of an HTTP request
/// has no HttpContext, so ICurrentTenantProvider.TenantId is null and
/// the query filter is bypassed for seeding only, exactly like
/// DatabaseSeeder does.
/// </summary>
public sealed class CrossTenantAccessTests : IDisposable
{
    private readonly PosFlowApiFactory _factory = new();
    private readonly HttpClient _client;

    public CrossTenantAccessTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetProduct_BelongingToAnotherTenant_ReturnsNotFound()
    {
        await AuthenticateAsAdminAsync();
        var foreignProductId = await SeedForeignTenantAsync(seedProduct: true);

        var response = await _client.GetAsync($"/api/products/{foreignProductId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProduct_BelongingToAnotherTenant_ReturnsNotFound_AndLeavesItUnchanged()
    {
        await AuthenticateAsAdminAsync();
        var foreignProductId = await SeedForeignTenantAsync(seedProduct: true);

        // A real RowVersion isn't obtainable through the API (GetById
        // 404s, by design) - any base64 payload is enough here, since
        // the tenant check must reject this before RowVersion is even
        // looked at.
        var response = await _client.PutAsJsonAsync(
            $"/api/products/{foreignProductId}",
            new UpdateProductRequest(
                "تم الاستيلاء عليه",
                null,
                null,
                999m,
                true,
                null,
                false,
                0,
                Convert.ToBase64String(new byte[8])));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<PosFlowDbContext>();

        var untouched = await dbContext.Products.FindAsync(foreignProductId);
        Assert.NotNull(untouched);
        Assert.NotEqual("تم الاستيلاء عليه", untouched!.NameAr);
    }

    [Fact]
    public async Task GetOrder_BelongingToAnotherTenant_ReturnsNotFound()
    {
        await AuthenticateAsAdminAsync();
        var foreignOrderId = await SeedForeignTenantAsync(seedOrder: true);

        var response = await _client.GetAsync($"/api/orders/{foreignOrderId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetUser_BelongingToAnotherTenant_ReturnsNotFound()
    {
        await AuthenticateAsAdminAsync();
        var foreignUserId = await SeedForeignTenantAsync(seedUser: true);

        var response = await _client.GetAsync($"/api/users/{foreignUserId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCustomer_BelongingToAnotherTenant_ReturnsNotFound()
    {
        await AuthenticateAsAdminAsync();
        var foreignCustomerId = await SeedForeignTenantAsync(seedCustomer: true);

        var response = await _client.PutAsJsonAsync(
            $"/api/customers/{foreignCustomerId}",
            new UpdateCustomerRequest("تم الاستيلاء عليه", null, null, true));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBranch_BelongingToAnotherTenant_ReturnsNotFound()
    {
        await AuthenticateAsAdminAsync();
        var foreignBranchId = await SeedForeignTenantAsync(seedBranch: true);

        var response = await _client.PutAsJsonAsync(
            $"/api/branches/{foreignBranchId}",
            new UpdateBranchRequest("تم الاستيلاء عليه", "HACKED", true));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task AuthenticateAsAdminAsync()
    {
        var token = await AuthHelper.LoginAsAdminAsync(_client);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Seeds a second tenant (with its own branch) that the
    /// authenticated caller has no legitimate access to, plus
    /// whichever resource type the test asked for under it. Returns
    /// that resource's id.
    /// </summary>
    private async Task<Guid> SeedForeignTenantAsync(
        bool seedProduct = false,
        bool seedOrder = false,
        bool seedUser = false,
        bool seedCustomer = false,
        bool seedBranch = false)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<PosFlowDbContext>();

        var foreignTenant = new Tenant { Name = "منافس" };
        dbContext.Tenants.Add(foreignTenant);

        var foreignBranch = new Branch
        {
            TenantId = foreignTenant.Id,
            Name = "فرع المنافس",
            Code = "RIVAL"
        };
        dbContext.Branches.Add(foreignBranch);

        Guid resultId = foreignBranch.Id;

        if (seedProduct || seedOrder)
        {
            var foreignProduct = new Product
            {
                TenantId = foreignTenant.Id,
                NameAr = "منتج المنافس",
                Price = 50m,
                IsActive = true
            };
            dbContext.Products.Add(foreignProduct);

            if (seedProduct)
            {
                resultId = foreignProduct.Id;
            }

            if (seedOrder)
            {
                var foreignUser = new AppUser
                {
                    TenantId = foreignTenant.Id,
                    BranchId = foreignBranch.Id,
                    Username = $"rival-{Guid.NewGuid():N}",
                    NormalizedUsername = $"RIVAL-{Guid.NewGuid():N}",
                    DisplayName = "كاشير المنافس",
                    Role = UserRole.Cashier,
                    PasswordHash = "not-a-real-hash",
                    IsActive = true
                };
                dbContext.Users.Add(foreignUser);

                var foreignShift = new Shift
                {
                    TenantId = foreignTenant.Id,
                    BranchId = foreignBranch.Id,
                    UserId = foreignUser.Id,
                    OpeningCash = 0,
                    CashSales = 50,
                    OpenedAtUtc = DateTime.UtcNow,
                    Status = ShiftStatus.Closed,
                    ClosedAtUtc = DateTime.UtcNow
                };
                dbContext.Shifts.Add(foreignShift);

                var foreignOrder = new Order
                {
                    TenantId = foreignTenant.Id,
                    BranchId = foreignBranch.Id,
                    ShiftId = foreignShift.Id,
                    OrderNumber = $"RIVAL-{Guid.NewGuid():N}",
                    Status = OrderStatus.Completed,
                    Subtotal = 50m,
                    TotalAmount = 50m
                };

                foreignOrder.Lines.Add(new OrderLine
                {
                    TenantId = foreignTenant.Id,
                    OrderId = foreignOrder.Id,
                    ProductId = foreignProduct.Id,
                    ProductName = foreignProduct.NameAr,
                    Quantity = 1,
                    UnitPrice = 50m,
                    LineTotal = 50m
                });

                dbContext.Orders.Add(foreignOrder);
                resultId = foreignOrder.Id;
            }
        }

        if (seedUser)
        {
            var foreignUser = new AppUser
            {
                TenantId = foreignTenant.Id,
                BranchId = foreignBranch.Id,
                Username = $"rival-{Guid.NewGuid():N}",
                NormalizedUsername = $"RIVAL-{Guid.NewGuid():N}",
                DisplayName = "موظف المنافس",
                Role = UserRole.Cashier,
                PasswordHash = "not-a-real-hash",
                IsActive = true
            };
            dbContext.Users.Add(foreignUser);
            resultId = foreignUser.Id;
        }

        if (seedCustomer)
        {
            var foreignCustomer = new Customer
            {
                TenantId = foreignTenant.Id,
                Name = "عميل المنافس",
                IsActive = true
            };
            dbContext.Customers.Add(foreignCustomer);
            resultId = foreignCustomer.Id;
        }

        if (seedBranch)
        {
            resultId = foreignBranch.Id;
        }

        await dbContext.SaveChangesAsync();

        return resultId;
    }
}
