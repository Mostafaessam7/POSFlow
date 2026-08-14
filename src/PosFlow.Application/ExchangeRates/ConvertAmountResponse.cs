namespace PosFlow.Application.ExchangeRates;

public sealed record ConvertAmountResponse(
    decimal Amount,
    string FromCurrencyCode,
    string ToCurrencyCode,
    decimal RatePerBaseUnit,
    decimal ConvertedAmount);
