using FluentValidation;

namespace PosFlow.Application.Orders.Validators;

public sealed class CreateOrderRequestValidator
    : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.Lines)
            .NotEmpty()
            .WithMessage("الفاتورة لازم تحتوي على صنف واحد على الأقل.");

        RuleForEach(x => x.Lines)
            .SetValidator(new OrderLineRequestValidator());

        RuleFor(x => x.Payments)
            .NotEmpty()
            .WithMessage("لازم تحدد طريقة دفع واحدة على الأقل.");

        RuleForEach(x => x.Payments)
            .SetValidator(new PaymentRequestValidator());
    }
}

public sealed class OrderLineRequestValidator
    : AbstractValidator<OrderLineRequest>
{
    public OrderLineRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty()
            .WithMessage("المنتج مطلوب.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("الكمية يجب أن تكون أكبر من صفر.");

        RuleFor(x => x.DiscountAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("قيمة الخصم لا يمكن أن تكون أقل من صفر.");
    }
}

public sealed class PaymentRequestValidator
    : AbstractValidator<PaymentRequest>
{
    public PaymentRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("قيمة الدفعة يجب أن تكون أكبر من صفر.");

        RuleFor(x => x.Method)
            .IsInEnum()
            .WithMessage("طريقة الدفع غير صحيحة.");
    }
}
