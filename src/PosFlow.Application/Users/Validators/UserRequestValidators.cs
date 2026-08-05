using FluentValidation;

namespace PosFlow.Application.Users.Validators;

public sealed class CreateUserRequestValidator
    : AbstractValidator<CreateUserRequest>
{
    private static readonly string[] AllowedRoles =
        ["Admin", "Manager", "Cashier"];

    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("اسم المستخدم مطلوب.")
            .MaximumLength(100)
            .WithMessage("اسم المستخدم لا يتجاوز 100 حرف.");

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .WithMessage("الاسم مطلوب.")
            .MaximumLength(200)
            .WithMessage("الاسم لا يتجاوز 200 حرف.");

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("صيغة الإيميل غير صحيحة.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("كلمة المرور مطلوبة.")
            .MinimumLength(6)
            .WithMessage("كلمة المرور يجب ألا تقل عن 6 أحرف.");

        RuleFor(x => x.Role)
            .Must(role => AllowedRoles.Contains(role))
            .WithMessage("الدور يجب أن يكون Admin أو Manager أو Cashier.");
    }
}

public sealed class UpdateUserRequestValidator
    : AbstractValidator<UpdateUserRequest>
{
    private static readonly string[] AllowedRoles =
        ["Admin", "Manager", "Cashier"];

    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .WithMessage("الاسم مطلوب.")
            .MaximumLength(200)
            .WithMessage("الاسم لا يتجاوز 200 حرف.");

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("صيغة الإيميل غير صحيحة.");

        RuleFor(x => x.Role)
            .Must(role => AllowedRoles.Contains(role))
            .WithMessage("الدور يجب أن يكون Admin أو Manager أو Cashier.");
    }
}

public sealed class ResetPasswordRequestValidator
    : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("كلمة المرور الجديدة مطلوبة.")
            .MinimumLength(6)
            .WithMessage("كلمة المرور يجب ألا تقل عن 6 أحرف.");
    }
}
