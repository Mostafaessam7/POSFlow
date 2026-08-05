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
}
