using PosFlow.Application.Orders;
using PosFlow.Application.Orders.Validators;
using PosFlow.Domain.Entities;
using Xunit;

namespace PosFlow.Application.Tests.Validators;

public sealed class CreateOrderRequestValidatorTests
{
    private readonly CreateOrderRequestValidator _validator = new();

    [Fact]
    public void Validate_WithNoLines_IsInvalid()
    {
        var request = new CreateOrderRequest(
            Lines: [],
            Payments: [new PaymentRequest(PaymentMethod.Cash, 10, null)]);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WithNoPayments_IsInvalid()
    {
        var request = new CreateOrderRequest(
            Lines: [new OrderLineRequest(Guid.NewGuid(), 1, 0)],
            Payments: []);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WithZeroQuantity_IsInvalid()
    {
        var request = new CreateOrderRequest(
            Lines: [new OrderLineRequest(Guid.NewGuid(), 0, 0)],
            Payments: [new PaymentRequest(PaymentMethod.Cash, 10, null)]);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WithNegativePaymentAmount_IsInvalid()
    {
        var request = new CreateOrderRequest(
            Lines: [new OrderLineRequest(Guid.NewGuid(), 1, 0)],
            Payments: [new PaymentRequest(PaymentMethod.Cash, -5, null)]);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WithValidRequest_IsValid()
    {
        var request = new CreateOrderRequest(
            Lines: [new OrderLineRequest(Guid.NewGuid(), 2, 0)],
            Payments: [new PaymentRequest(PaymentMethod.Cash, 20, null)]);

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }
}
