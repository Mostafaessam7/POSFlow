using PosFlow.Application.Common;

namespace PosFlow.Application.Products;

public interface IProductService
{
    Task<PagedResult<ProductResponse>> GetAllAsync(
        bool includeInactive,
        Guid? categoryId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ProductResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Server-side barcode-scanner lookup: exact match on the tenant's
    /// products, so a scanner (barcode + Enter) doesn't need to pull
    /// the whole catalog client-side to filter it.
    /// </summary>
    Task<ProductResponse?> GetByBarcodeAsync(
        string barcode,
        CancellationToken cancellationToken = default);

    Task<ProductResponse> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductResponse> UpdateAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>Stock movement ledger for one product, newest first.</summary>
    Task<PagedResult<StockMovementResponse>> GetStockMovementsAsync(
        Guid productId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
