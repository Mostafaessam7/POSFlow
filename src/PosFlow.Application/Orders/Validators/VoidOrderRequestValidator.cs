using FluentValidation;

namespace PosFlow.Application.Orders.Validators;

public sealed class VoidOrderRequestValidator
    : AbstractValidator<VoidOrderRequest>
{
    public VoidOrderRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("سبب الإلغاء مطلوب.")
            .MaximumLength(500)
            .WithMessage("سبب الإلغاء لا يتجاوز 500 حرف.");
    }
}
