using FluentValidation;

namespace PosFlow.Application.Auth.Validators;

public sealed class ForgotPasswordRequestValidator
    : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("اسم المستخدم مطلوب.");
    }
}

public sealed class ResetPasswordWithTokenRequestValidator
    : AbstractValidator<ResetPasswordWithTokenRequest>
{
    public ResetPasswordWithTokenRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .WithMessage("رابط إعادة التعيين غير صالح.");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("كلمة المرور الجديدة مطلوبة.")
            .MinimumLength(6)
            .WithMessage("كلمة المرور يجب ألا تقل عن 6 أحرف.");
    }
}
