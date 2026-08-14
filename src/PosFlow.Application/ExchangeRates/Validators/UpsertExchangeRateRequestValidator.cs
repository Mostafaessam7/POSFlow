using FluentValidation;

namespace PosFlow.Application.ExchangeRates.Validators;

public sealed class UpsertExchangeRateRequestValidator
    : AbstractValidator<UpsertExchangeRateRequest>
{
    public UpsertExchangeRateRequestValidator()
    {
        RuleFor(x => x.CurrencyCode)
            .NotEmpty()
            .WithMessage("كود العملة مطلوب.")
            .Length(3)
            .WithMessage("كود العملة لازم يكون 3 حروف (ISO 4217)، مثل USD.");

        RuleFor(x => x.RatePerBaseUnit)
            .GreaterThan(0)
            .WithMessage("سعر الصرف لازم يكون أكبر من صفر.");
    }
}
