using FluentValidation;

namespace PosFlow.Application.Categories.Validators;

public sealed class CreateCategoryRequestValidator
    : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.NameAr)
            .NotEmpty()
            .WithMessage("اسم التصنيف مطلوب.")
            .MaximumLength(150)
            .WithMessage("اسم التصنيف لا يتجاوز 150 حرف.");

        RuleFor(x => x.NameEn)
            .MaximumLength(150)
            .WithMessage("الاسم بالإنجليزي لا يتجاوز 150 حرف.");
    }
}

public sealed class UpdateCategoryRequestValidator
    : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.NameAr)
            .NotEmpty()
            .WithMessage("اسم التصنيف مطلوب.")
            .MaximumLength(150)
            .WithMessage("اسم التصنيف لا يتجاوز 150 حرف.");

        RuleFor(x => x.NameEn)
            .MaximumLength(150)
            .WithMessage("الاسم بالإنجليزي لا يتجاوز 150 حرف.");
    }
}
