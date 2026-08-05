using Microsoft.EntityFrameworkCore;
using PosFlow.Application.Categories;
using PosFlow.Application.Common;
using PosFlow.Domain.Entities;
using PosFlow.Infrastructure.Persistence;

namespace PosFlow.Infrastructure.Categories;

public sealed class CategoryService(
    PosFlowDbContext dbContext,
    ICurrentUser currentUser)
    : ICategoryService
{
    private readonly PosFlowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<IReadOnlyList<CategoryResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var categories = await _dbContext.ProductCategories
            .AsNoTracking()
            .Where(x => x.TenantId == _currentUser.TenantId)
            .OrderBy(x => x.NameAr)
            .ToListAsync(cancellationToken);

        return categories
            .Select(MapResponse)
            .ToList();
    }

    public async Task<CategoryResponse> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = new ProductCategory
        {
            TenantId = _currentUser.TenantId,
            NameAr = request.NameAr,
            NameEn = request.NameEn
        };

        _dbContext.ProductCategories.Add(category);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapResponse(category);
    }

    public async Task<CategoryResponse> UpdateAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.ProductCategories
            .SingleOrDefaultAsync(
                x =>
                    x.Id == id &&
                    x.TenantId == _currentUser.TenantId,
                cancellationToken);

        if (category is null)
        {
            throw new KeyNotFoundException(
                "التصنيف غير موجود.");
        }

        category.NameAr = request.NameAr;
        category.NameEn = request.NameEn;
        category.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapResponse(category);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.ProductCategories
            .SingleOrDefaultAsync(
                x =>
                    x.Id == id &&
                    x.TenantId == _currentUser.TenantId,
                cancellationToken);

        if (category is null)
        {
            throw new KeyNotFoundException(
                "التصنيف غير موجود.");
        }

        var hasProducts = await _dbContext.Products
            .AsNoTracking()
            .AnyAsync(
                x => x.CategoryId == id,
                cancellationToken);

        if (hasProducts)
        {
            throw new InvalidOperationException(
                "لا يمكن حذف تصنيف مرتبط بمنتجات، انقل المنتجات لتصنيف آخر أولاً.");
        }

        _dbContext.ProductCategories.Remove(category);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static CategoryResponse MapResponse(
        ProductCategory category)
    {
        return new CategoryResponse(
            Id: category.Id,
            NameAr: category.NameAr,
            NameEn: category.NameEn);
    }
}
