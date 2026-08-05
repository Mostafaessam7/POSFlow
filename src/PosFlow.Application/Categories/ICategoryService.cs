namespace PosFlow.Application.Categories;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryResponse>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<CategoryResponse> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task<CategoryResponse> UpdateAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
