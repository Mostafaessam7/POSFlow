using FluentValidation;

namespace PosFlow.Application.Auth.Validators;

public sealed class RefreshTokenRequestValidator
    : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage("رمز التجديد مطلوب.");
    }
}
