namespace PosFlow.Application.Products;

public sealed record CreateProductRequest(
    string NameAr,
    string? NameEn,
    string? Barcode,
    decimal Price,
    Guid? CategoryId,
    bool TrackStock,
    decimal StockQuantity
);
