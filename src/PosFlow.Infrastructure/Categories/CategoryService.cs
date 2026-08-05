using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PosFlow.Application.Categories;
using PosFlow.Application.Common;
using PosFlow.Domain.Entities;
using PosFlow.Infrastructure.Persistence;

namespace PosFlow.Infrastructure.Categories;

/// <summary>
/// Categories change rarely but get read on nearly every POS/product
/// screen load, so they're a good, low-risk caching candidate (unlike
/// e.g. product stock, which changes on every sale and would risk
/// showing stale availability). IMemoryCache is per-process - fine for
/// a single instance, but a write on one instance won't invalidate
/// another instance's cache in a horizontally-scaled deployment. Swap
/// for IDistributedCache backed by Redis if/when this app runs more
/// than one instance.
/// </summary>
public sealed class CategoryService(
    PosFlowDbContext dbContext,
    ICurrentUser currentUser,
    IMemoryCache cache)
    : ICategoryService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly PosFlowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IMemoryCache _cache = cache;

    private string CacheKey => $"categories:{_currentUser.TenantId}";

    public async Task<IReadOnlyList<CategoryResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(
            CacheKey,
            out IReadOnlyList<CategoryResponse>? cached))
        {
            return cached!;
        }

        var categories = await _dbContext.ProductCategories
            .AsNoTracking()
            .Where(x => x.TenantId == _currentUser.TenantId)
            .OrderBy(x => x.NameAr)
            .ToListAsync(cancellationToken);

        var result = categories
            .Select(MapResponse)
            .ToList();

        _cache.Set(CacheKey, (IReadOnlyList<CategoryResponse>)result, CacheDuration);

        return result;
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
        _cache.Remove(CacheKey);

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
        _cache.Remove(CacheKey);

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
        _cache.Remove(CacheKey);
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
