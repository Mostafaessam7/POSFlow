namespace PosFlow.Application.ExchangeRates;

public interface IExchangeRateService
{
    Task<IReadOnlyList<ExchangeRateResponse>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<ExchangeRateResponse> UpsertAsync(
        UpsertExchangeRateRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>Converts an amount in the tenant's base currency (Tenant.CurrencyCode) to a target currency using its stored manual rate. Display only.</summary>
    Task<ConvertAmountResponse> ConvertAsync(
        decimal amount,
        string toCurrencyCode,
        CancellationToken cancellationToken = default);
}
