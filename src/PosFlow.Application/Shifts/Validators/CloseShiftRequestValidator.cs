using FluentValidation;

namespace PosFlow.Application.Shifts.Validators;

public sealed class CloseShiftRequestValidator
    : AbstractValidator<CloseShiftRequest>
{
    public CloseShiftRequestValidator()
    {
        RuleFor(x => x.ClosingCash)
            .GreaterThanOrEqualTo(0)
            .WithMessage("النقدية الفعلية لا يمكن أن تكون أقل من صفر.");
    }
}
