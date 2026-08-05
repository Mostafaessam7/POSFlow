using FluentValidation;

namespace PosFlow.Application.Auth.Validators;

public sealed class LoginRequestValidator
    : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("اسم المستخدم مطلوب.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("كلمة المرور مطلوبة.");
    }
}
