namespace PosFlow.Domain.Entities;

public sealed class Tenant : BaseEntity
{
    public required string Name { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>ISO 4217 currency code used for display only (e.g. "EGP", "SAR", "USD") - no multi-currency conversion, just which symbol/code the frontend shows.</summary>
    public string CurrencyCode { get; set; } = "EGP";

    /// <summary>Flat VAT/sales tax percentage applied to every order at checkout (e.g. 14 for Egypt's 14% VAT). 0 = no tax, matching the previous hardcoded behaviour.</summary>
    public decimal TaxRatePercent { get; set; }

    public ICollection<Branch> Branches { get; set; } = [];
}