namespace PosFlow.Application.Products;

public sealed record StockMovementResponse(
    Guid Id,
    Guid ProductId,
    decimal QuantityChange,
    decimal ResultingStockQuantity,
    string Reason,
    Guid? OrderId,
    Guid? UserId,
    string? Note,
    DateTime CreatedAtUtc);
