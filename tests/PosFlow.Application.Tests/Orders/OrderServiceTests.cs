using PosFlow.Application.Orders;
using PosFlow.Application.Tests.TestHelpers;
using PosFlow.Domain.Entities;
using PosFlow.Infrastructure.Orders;
using Xunit;

namespace PosFlow.Application.Tests.Orders;

public sealed class OrderServiceTests
{
    [Fact]
    public async Task CheckoutAsync_WithoutOpenShift_ThrowsInvalidOperationException()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var currentUser = new FakeCurrentUser();
        var sut = new OrderService(dbContext, currentUser);

        var request = new CreateOrderRequest(
            Lines: [new OrderLineRequest(Guid.NewGuid(), 1, 0)],
            Payments: [new PaymentRequest(PaymentMethod.Cash, 10, null)]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.CheckoutAsync(request));
    }

    [Fact]
    public async Task CheckoutAsync_WithInsufficientStock_ThrowsInvalidOperationException()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var currentUser = new FakeCurrentUser();

        var product = SeedProduct(dbContext, currentUser, trackStock: true, stock: 2, price: 10m);
        SeedOpenShift(dbContext, currentUser);
        await dbContext.SaveChangesAsync();

        var sut = new OrderService(dbContext, currentUser);

        var request = new CreateOrderRequest(
            Lines: [new OrderLineRequest(product.Id, 5, 0)],
            Payments: [new PaymentRequest(PaymentMethod.Cash, 50, null)]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.CheckoutAsync(request));
    }

    [Fact]
    public async Task CheckoutAsync_WithInsufficientPayment_ThrowsInvalidOperationException()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var currentUser = new FakeCurrentUser();

        var product = SeedProduct(dbContext, currentUser, trackStock: false, stock: 0, price: 100m);
        SeedOpenShift(dbContext, currentUser);
        await dbContext.SaveChangesAsync();

        var sut = new OrderService(dbContext, currentUser);

        var request = new CreateOrderRequest(
            Lines: [new OrderLineRequest(product.Id, 1, 0)],
            Payments: [new PaymentRequest(PaymentMethod.Cash, 50, null)]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.CheckoutAsync(request));
    }

    [Fact]
    public async Task CheckoutAsync_WithTrackedStock_DecrementsStockOnSuccess()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var currentUser = new FakeCurrentUser();

        var product = SeedProduct(dbContext, currentUser, trackStock: true, stock: 10, price: 20m);
        SeedOpenShift(dbContext, currentUser);
        await dbContext.SaveChangesAsync();

        var sut = new OrderService(dbContext, currentUser);

        var request = new CreateOrderRequest(
            Lines: [new OrderLineRequest(product.Id, 3, 0)],
            Payments: [new PaymentRequest(PaymentMethod.Cash, 60, null)]);

        var result = await sut.CheckoutAsync(request);

        Assert.Equal("Completed", result.Status);
        Assert.Equal(60m, result.TotalAmount);
        Assert.Equal(0m, result.ChangeDue);
        Assert.Equal(7m, product.StockQuantity);
    }

    [Fact]
    public async Task CheckoutAsync_WithOverpayment_ReturnsChangeDue()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var currentUser = new FakeCurrentUser();

        var product = SeedProduct(dbContext, currentUser, trackStock: false, stock: 0, price: 30m);
        SeedOpenShift(dbContext, currentUser);
        await dbContext.SaveChangesAsync();

        var sut = new OrderService(dbContext, currentUser);

        var request = new CreateOrderRequest(
            Lines: [new OrderLineRequest(product.Id, 1, 0)],
            Payments: [new PaymentRequest(PaymentMethod.Cash, 50, null)]);

        var result = await sut.CheckoutAsync(request);

        Assert.Equal(30m, result.TotalAmount);
        Assert.Equal(20m, result.ChangeDue);
    }

    [Fact]
    public async Task VoidAsync_ByShiftOwner_RestoresStockAndMarksCancelled()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var currentUser = new FakeCurrentUser { Role = "Cashier" };

        var product = SeedProduct(dbContext, currentUser, trackStock: true, stock: 10, price: 20m);
        SeedOpenShift(dbContext, currentUser);
        await dbContext.SaveChangesAsync();

        var sut = new OrderService(dbContext, currentUser);

        var order = await sut.CheckoutAsync(new CreateOrderRequest(
            Lines: [new OrderLineRequest(product.Id, 4, 0)],
            Payments: [new PaymentRequest(PaymentMethod.Cash, 80, null)]));

        Assert.Equal(6m, product.StockQuantity);

        var voided = await sut.VoidAsync(
            order.Id,
            new VoidOrderRequest("خطأ في الطلب"));

        Assert.Equal("Cancelled", voided.Status);
        Assert.Equal("خطأ في الطلب", voided.VoidReason);
        Assert.Equal(10m, product.StockQuantity); // fully restored
    }

    [Fact]
    public async Task VoidAsync_ByDifferentCashier_ThrowsInvalidOperationException()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var owner = new FakeCurrentUser { Role = "Cashier" };

        var product = SeedProduct(dbContext, owner, trackStock: false, stock: 0, price: 10m);
        SeedOpenShift(dbContext, owner);
        await dbContext.SaveChangesAsync();

        var ownerService = new OrderService(dbContext, owner);

        var order = await ownerService.CheckoutAsync(new CreateOrderRequest(
            Lines: [new OrderLineRequest(product.Id, 1, 0)],
            Payments: [new PaymentRequest(PaymentMethod.Cash, 10, null)]));

        var otherCashier = new FakeCurrentUser
        {
            TenantId = owner.TenantId,
            BranchId = owner.BranchId,
            Role = "Cashier"
            // Different UserId (default) - not the shift owner.
        };

        var otherService = new OrderService(dbContext, otherCashier);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => otherService.VoidAsync(
                order.Id,
                new VoidOrderRequest("محاولة إلغاء من كاشير آخر")));
    }

    [Fact]
    public async Task VoidAsync_ByManager_SucceedsRegardlessOfShiftOwnership()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var owner = new FakeCurrentUser { Role = "Cashier" };

        var product = SeedProduct(dbContext, owner, trackStock: false, stock: 0, price: 10m);
        SeedOpenShift(dbContext, owner);
        await dbContext.SaveChangesAsync();

        var ownerService = new OrderService(dbContext, owner);

        var order = await ownerService.CheckoutAsync(new CreateOrderRequest(
            Lines: [new OrderLineRequest(product.Id, 1, 0)],
            Payments: [new PaymentRequest(PaymentMethod.Cash, 10, null)]));

        var manager = new FakeCurrentUser
        {
            TenantId = owner.TenantId,
            BranchId = owner.BranchId,
            Role = "Manager"
        };

        var managerService = new OrderService(dbContext, manager);

        var voided = await managerService.VoidAsync(
            order.Id,
            new VoidOrderRequest("مراجعة المدير"));

        Assert.Equal("Cancelled", voided.Status);
    }

    [Fact]
    public async Task VoidAsync_WhenAlreadyVoided_ThrowsInvalidOperationException()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var currentUser = new FakeCurrentUser { Role = "Cashier" };

        var product = SeedProduct(dbContext, currentUser, trackStock: false, stock: 0, price: 10m);
        SeedOpenShift(dbContext, currentUser);
        await dbContext.SaveChangesAsync();

        var sut = new OrderService(dbContext, currentUser);

        var order = await sut.CheckoutAsync(new CreateOrderRequest(
            Lines: [new OrderLineRequest(product.Id, 1, 0)],
            Payments: [new PaymentRequest(PaymentMethod.Cash, 10, null)]));

        await sut.VoidAsync(order.Id, new VoidOrderRequest("سبب أول"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.VoidAsync(order.Id, new VoidOrderRequest("محاولة ثانية")));
    }

    private static Product SeedProduct(
        PosFlow.Infrastructure.Persistence.PosFlowDbContext dbContext,
        FakeCurrentUser currentUser,
        bool trackStock,
        decimal stock,
        decimal price)
    {
        var product = new Product
        {
            TenantId = currentUser.TenantId,
            NameAr = "منتج تجريبي",
            Price = price,
            TrackStock = trackStock,
            StockQuantity = stock,
            IsActive = true
        };

        dbContext.Products.Add(product);
        return product;
    }

    private static Shift SeedOpenShift(
        PosFlow.Infrastructure.Persistence.PosFlowDbContext dbContext,
        FakeCurrentUser currentUser)
    {
        var shift = new Shift
        {
            TenantId = currentUser.TenantId,
            BranchId = currentUser.BranchId,
            UserId = currentUser.UserId,
            OpeningCash = 0,
            Status = ShiftStatus.Open,
            OpenedAtUtc = DateTime.UtcNow
        };

        dbContext.Shifts.Add(shift);
        return shift;
    }
}
