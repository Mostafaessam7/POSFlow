namespace PosFlow.Application.Products;

public sealed record ProductResponse(
    Guid Id,
    string NameAr,
    string? NameEn,
    string? Barcode,
    decimal Price,
    bool IsActive,
    Guid? CategoryId,
    string? CategoryName,
    bool TrackStock,
    decimal StockQuantity,
    string RowVersion
);
