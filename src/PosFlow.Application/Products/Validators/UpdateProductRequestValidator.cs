using FluentValidation;

namespace PosFlow.Application.Products.Validators;

public sealed class UpdateProductRequestValidator
    : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.NameAr)
            .NotEmpty()
            .WithMessage("اسم المنتج بالعربي مطلوب.")
            .MaximumLength(250)
            .WithMessage("اسم المنتج بالعربي لا يتجاوز 250 حرف.");

        RuleFor(x => x.NameEn)
            .MaximumLength(250)
            .WithMessage("اسم المنتج بالإنجليزي لا يتجاوز 250 حرف.");

        RuleFor(x => x.Barcode)
            .MaximumLength(100)
            .WithMessage("الباركود لا يتجاوز 100 حرف.");

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .WithMessage("سعر المنتج يجب أن يكون أكبر من صفر.");

        RuleFor(x => x.StockQuantity)
            .GreaterThanOrEqualTo(0)
            .WithMessage("كمية المخزون لا يمكن أن تكون أقل من صفر.");

        RuleFor(x => x.RowVersion)
            .NotEmpty()
            .WithMessage("بيانات النسخة المطلوبة للتحديث ناقصة، أعد تحميل الصفحة.");
    }
}
