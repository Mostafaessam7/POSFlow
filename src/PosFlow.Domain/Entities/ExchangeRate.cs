namespace PosFlow.Domain.Entities;

/// <summary>
/// Admin-maintained conversion rate from the tenant's base currency
/// (Tenant.CurrencyCode) to another currency, for DISPLAY purposes only
/// (e.g. showing a total in USD next to the EGP total on a receipt).
/// Rates are entered manually by the tenant admin - there is no live
/// market-rate feed, so this never goes stale silently; whoever
/// maintains it decides when to update it.
/// </summary>
public sealed class ExchangeRate : BaseEntity
{
    public Guid TenantId { get; set; }

    /// <summary>ISO 4217 code being converted TO (e.g. "USD"). The FROM side is implicitly Tenant.CurrencyCode.</summary>
    public required string CurrencyCode { get; set; }

    /// <summary>How many units of CurrencyCode one unit of the tenant's base currency is worth.</summary>
    public decimal RatePerBaseUnit { get; set; }
}
