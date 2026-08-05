using FluentValidation;

namespace PosFlow.Application.Shifts.Validators;

public sealed class OpenShiftRequestValidator
    : AbstractValidator<OpenShiftRequest>
{
    public OpenShiftRequestValidator()
    {
        RuleFor(x => x.OpeningCash)
            .GreaterThanOrEqualTo(0)
            .WithMessage("رصيد بداية الوردية لا يمكن أن يكون أقل من صفر.");
    }
}
