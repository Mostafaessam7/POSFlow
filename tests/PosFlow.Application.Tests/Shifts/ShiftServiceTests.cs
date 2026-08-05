using PosFlow.Application.Shifts;
using PosFlow.Application.Tests.TestHelpers;
using PosFlow.Domain.Entities;
using PosFlow.Infrastructure.Shifts;
using Xunit;

namespace PosFlow.Application.Tests.Shifts;

public sealed class ShiftServiceTests
{
    [Fact]
    public async Task OpenAsync_WhenNoOpenShift_CreatesOpenShift()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var currentUser = new FakeCurrentUser();
        var sut = new ShiftService(dbContext, currentUser);

        var result = await sut.OpenAsync(new OpenShiftRequest(200m));

        Assert.Equal("Open", result.Status);
        Assert.Equal(200m, result.OpeningCash);
        Assert.Equal(currentUser.UserId, result.UserId);
    }

    [Fact]
    public async Task OpenAsync_WhenShiftAlreadyOpen_ThrowsInvalidOperationException()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var currentUser = new FakeCurrentUser();
        var sut = new ShiftService(dbContext, currentUser);

        await sut.OpenAsync(new OpenShiftRequest(100m));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.OpenAsync(new OpenShiftRequest(50m)));
    }

    [Fact]
    public async Task OpenAsync_WithNegativeOpeningCash_ThrowsArgumentOutOfRangeException()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var currentUser = new FakeCurrentUser();
        var sut = new ShiftService(dbContext, currentUser);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => sut.OpenAsync(new OpenShiftRequest(-1m)));
    }

    [Fact]
    public async Task CloseAsync_ComputesExpectedCashFromCashPaymentsOnly()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var currentUser = new FakeCurrentUser();
        var sut = new ShiftService(dbContext, currentUser);

        var openedShift = await sut.OpenAsync(new OpenShiftRequest(100m));

        var order = new Order
        {
            TenantId = currentUser.TenantId,
            BranchId = currentUser.BranchId,
            ShiftId = openedShift.Id,
            OrderNumber = "TEST-0001",
            Status = OrderStatus.Completed,
            Subtotal = 200m,
            TotalAmount = 200m
        };

        dbContext.Orders.Add(order);

        // 150 cash + 50 card = 200 total. Only the cash portion should
        // feed into the till reconciliation below.
        dbContext.Payments.Add(new Payment
        {
            TenantId = currentUser.TenantId,
            OrderId = order.Id,
            Method = PaymentMethod.Cash,
            Amount = 150m
        });

        dbContext.Payments.Add(new Payment
        {
            TenantId = currentUser.TenantId,
            OrderId = order.Id,
            Method = PaymentMethod.Card,
            Amount = 50m
        });

        await dbContext.SaveChangesAsync();

        var closed = await sut.CloseAsync(
            openedShift.Id,
            new CloseShiftRequest(240m));

        Assert.Equal("Closed", closed.Status);
        Assert.Equal(150m, closed.CashSales);
        Assert.Equal(250m, closed.ExpectedCash); // 100 opening + 150 cash
        Assert.Equal(-10m, closed.CashDifference); // 240 actual - 250 expected
    }

    [Fact]
    public async Task CloseAsync_IgnoresPaymentsFromCancelledOrders()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var currentUser = new FakeCurrentUser();
        var sut = new ShiftService(dbContext, currentUser);

        var openedShift = await sut.OpenAsync(new OpenShiftRequest(0m));

        var voidedOrder = new Order
        {
            TenantId = currentUser.TenantId,
            BranchId = currentUser.BranchId,
            ShiftId = openedShift.Id,
            OrderNumber = "TEST-0002",
            Status = OrderStatus.Cancelled,
            Subtotal = 500m,
            TotalAmount = 500m
        };

        dbContext.Orders.Add(voidedOrder);

        dbContext.Payments.Add(new Payment
        {
            TenantId = currentUser.TenantId,
            OrderId = voidedOrder.Id,
            Method = PaymentMethod.Cash,
            Amount = 500m
        });

        await dbContext.SaveChangesAsync();

        var closed = await sut.CloseAsync(
            openedShift.Id,
            new CloseShiftRequest(0m));

        Assert.Equal(0m, closed.CashSales);
        Assert.Equal(0m, closed.CashDifference);
    }

    [Fact]
    public async Task CloseAsync_WhenAlreadyClosed_ThrowsInvalidOperationException()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var currentUser = new FakeCurrentUser();
        var sut = new ShiftService(dbContext, currentUser);

        var openedShift = await sut.OpenAsync(new OpenShiftRequest(0m));
        await sut.CloseAsync(openedShift.Id, new CloseShiftRequest(0m));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.CloseAsync(openedShift.Id, new CloseShiftRequest(0m)));
    }
}
