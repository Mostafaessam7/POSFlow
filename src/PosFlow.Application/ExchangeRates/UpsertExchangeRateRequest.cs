namespace PosFlow.Application.ExchangeRates;

/// <summary>Creates the rate for CurrencyCode if it doesn't exist yet for this tenant, otherwise updates it.</summary>
public sealed record UpsertExchangeRateRequest(
    string CurrencyCode,
    decimal RatePerBaseUnit);
