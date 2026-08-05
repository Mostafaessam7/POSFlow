using FluentValidation;

namespace PosFlow.Application.Auth.Validators;

public sealed class VerifyTwoFactorRequestValidator
    : AbstractValidator<VerifyTwoFactorRequest>
{
    public VerifyTwoFactorRequestValidator()
    {
        RuleFor(x => x.ChallengeToken).NotEmpty();

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("كود التحقق مطلوب.")
            .Length(6).WithMessage("كود التحقق مكون من 6 أرقام.");
    }
}

public sealed class EnableTwoFactorRequestValidator
    : AbstractValidator<EnableTwoFactorRequest>
{
    public EnableTwoFactorRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("كود التحقق مطلوب.")
            .Length(6).WithMessage("كود التحقق مكون من 6 أرقام.");
    }
}

public sealed class DisableTwoFactorRequestValidator
    : AbstractValidator<DisableTwoFactorRequest>
{
    public DisableTwoFactorRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("كود التحقق مطلوب.")
            .Length(6).WithMessage("كود التحقق مكون من 6 أرقام.");
    }
}
