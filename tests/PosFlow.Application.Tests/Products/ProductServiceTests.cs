using PosFlow.Application.Products;
using PosFlow.Application.Tests.TestHelpers;
using PosFlow.Domain.Entities;
using PosFlow.Infrastructure.Products;
using Xunit;

namespace PosFlow.Application.Tests.Products;

public sealed class ProductServiceTests
{
    [Fact]
    public async Task CreateAsync_WithDuplicateBarcode_ThrowsInvalidOperationException()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var currentUser = new FakeCurrentUser();
        var sut = new ProductService(dbContext, currentUser);

        await sut.CreateAsync(new CreateProductRequest(
            "منتج ١", null, "12345", 10m, null, false, 0));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.CreateAsync(new CreateProductRequest(
                "منتج ٢", null, "12345", 20m, null, false, 0)));
    }

    [Fact]
    public async Task UpdateAsync_WhenProductMissing_ThrowsKeyNotFoundException()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var currentUser = new FakeCurrentUser();
        var sut = new ProductService(dbContext, currentUser);

        var request = new UpdateProductRequest(
            "اسم", null, null, 10m, true, null, false, 0,
            Convert.ToBase64String(new byte[8]));

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.UpdateAsync(Guid.NewGuid(), request));
    }

    [Fact]
    public async Task UpdateAsync_WithStaleRowVersion_ThrowsInvalidOperationException()
    {
        // Two ProductService instances backed by two separate
        // PosFlowDbContext instances pointing at the SAME in-memory
        // database - simulating two people editing the same product
        // from two different browser tabs/requests.
        var databaseName = Guid.NewGuid().ToString();
        var currentUser = new FakeCurrentUser();

        Guid productId;
        string originalRowVersion;

        await using (var seedContext = TestDbContextFactory.Create(databaseName))
        {
            var product = new Product
            {
                TenantId = currentUser.TenantId,
                NameAr = "قلم",
                Price = 5m,
                IsActive = true
            };

            seedContext.Products.Add(product);
            await seedContext.SaveChangesAsync();

            productId = product.Id;
            originalRowVersion = Convert.ToBase64String(product.RowVersion);
        }

        // First save succeeds and moves the RowVersion forward.
        await using (var firstContext = TestDbContextFactory.Create(databaseName))
        {
            var firstService = new ProductService(firstContext, currentUser);

            await firstService.UpdateAsync(productId, new UpdateProductRequest(
                "قلم أزرق", null, null, 6m, true, null, false, 0,
                originalRowVersion));
        }

        // Second save still holds the ORIGINAL (now stale) RowVersion -
        // must be rejected instead of silently overwriting the first
        // user's change.
        await using (var secondContext = TestDbContextFactory.Create(databaseName))
        {
            var secondService = new ProductService(secondContext, currentUser);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => secondService.UpdateAsync(productId, new UpdateProductRequest(
                    "قلم أحمر", null, null, 7m, true, null, false, 0,
                    originalRowVersion)));
        }
    }

    [Fact]
    public async Task DeactivateAsync_SetsIsActiveFalse()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var currentUser = new FakeCurrentUser();
        var sut = new ProductService(dbContext, currentUser);

        var created = await sut.CreateAsync(new CreateProductRequest(
            "منتج", null, null, 15m, null, false, 0));

        await sut.DeactivateAsync(created.Id);

        var reloaded = await sut.GetByIdAsync(created.Id);

        Assert.NotNull(reloaded);
        Assert.False(reloaded!.IsActive);
    }
}
