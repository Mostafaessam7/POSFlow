using FluentValidation;

namespace PosFlow.Application.Customers.Validators;

public sealed class CreateCustomerRequestValidator
    : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم العميل مطلوب.")
            .MaximumLength(200).WithMessage("اسم العميل لا يتجاوز 200 حرف.");

        RuleFor(x => x.Phone)
            .MaximumLength(30).WithMessage("رقم الهاتف طويل جدًا.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("البريد الإلكتروني غير صالح.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public sealed class UpdateCustomerRequestValidator
    : AbstractValidator<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم العميل مطلوب.")
            .MaximumLength(200).WithMessage("اسم العميل لا يتجاوز 200 حرف.");

        RuleFor(x => x.Phone)
            .MaximumLength(30).WithMessage("رقم الهاتف طويل جدًا.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("البريد الإلكتروني غير صالح.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}
