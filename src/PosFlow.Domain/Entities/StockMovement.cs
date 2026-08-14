namespace PosFlow.Domain.Entities;

public enum StockMovementReason
{
    /// <summary>Stock decremented by a completed checkout.</summary>
    Sale = 1,

    /// <summary>Stock restored because an order was voided.</summary>
    OrderVoided = 2,

    /// <summary>Cashier/admin changed StockQuantity directly on the product form.</summary>
    ManualAdjustment = 3,

    /// <summary>New stock received from a supplier.</summary>
    StockReceived = 4
}

/// <summary>
/// Append-only ledger row for every change to Product.StockQuantity -
/// answers "who changed the stock of X, by how much, why, and when",
/// which the raw StockQuantity column alone can't (HANDOVER.md /
/// ENTERPRISE-READINESS.md called this out as a known gap: "الكمية رقم
/// بيتكتب فوق بدون تاريخ"). Never updated or deleted, only inserted.
/// </summary>
public sealed class StockMovement : BaseEntity
{
    public Guid TenantId { get; set; }

    public Guid ProductId { get; set; }

    /// <summary>Signed change applied to StockQuantity (negative for a sale, positive for a void/receipt).</summary>
    public decimal QuantityChange { get; set; }

    /// <summary>StockQuantity value right after this movement was applied - lets a reader reconstruct history without replaying every row.</summary>
    public decimal ResultingStockQuantity { get; set; }

    public StockMovementReason Reason { get; set; }

    /// <summary>Order.Id for Sale/OrderVoided movements, null otherwise.</summary>
    public Guid? OrderId { get; set; }

    public Guid? UserId { get; set; }

    public string? Note { get; set; }
}
