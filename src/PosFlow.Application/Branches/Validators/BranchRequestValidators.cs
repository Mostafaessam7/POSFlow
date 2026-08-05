using FluentValidation;

namespace PosFlow.Application.Branches.Validators;

public sealed class CreateBranchRequestValidator
    : AbstractValidator<CreateBranchRequest>
{
    public CreateBranchRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("اسم الفرع مطلوب.")
            .MaximumLength(200)
            .WithMessage("اسم الفرع لا يتجاوز 200 حرف.");

        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("كود الفرع مطلوب.")
            .MaximumLength(50)
            .WithMessage("كود الفرع لا يتجاوز 50 حرف.");
    }
}

public sealed class UpdateBranchRequestValidator
    : AbstractValidator<UpdateBranchRequest>
{
    public UpdateBranchRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("اسم الفرع مطلوب.")
            .MaximumLength(200)
            .WithMessage("اسم الفرع لا يتجاوز 200 حرف.");

        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("كود الفرع مطلوب.")
            .MaximumLength(50)
            .WithMessage("كود الفرع لا يتجاوز 50 حرف.");
    }
}
