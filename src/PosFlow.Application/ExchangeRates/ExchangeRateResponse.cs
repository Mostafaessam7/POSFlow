namespace PosFlow.Application.ExchangeRates;

public sealed record ExchangeRateResponse(
    Guid Id,
    string CurrencyCode,
    decimal RatePerBaseUnit,
    DateTime? UpdatedAtUtc);
