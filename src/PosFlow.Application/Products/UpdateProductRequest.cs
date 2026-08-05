namespace PosFlow.Application.Products;

public sealed record UpdateProductRequest(
    string NameAr,
    string? NameEn,
    string? Barcode,
    decimal Price,
    bool IsActive,
    Guid? CategoryId,
    bool TrackStock,
    decimal StockQuantity,
    string RowVersion
);
