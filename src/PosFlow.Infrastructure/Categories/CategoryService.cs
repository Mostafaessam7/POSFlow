using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using PosFlow.Application.Categories;
using PosFlow.Application.Common;
using PosFlow.Domain.Entities;
using PosFlow.Infrastructure.Persistence;

namespace PosFlow.Infrastructure.Categories;

/// <summary>
/// Categories change rarely but get read on nearly every POS/product
/// screen load, so they're a good, low-risk caching candidate (unlike
/// e.g. product stock, which changes on every sale and would risk
/// showing stale availability).
/// </summary>
/// <remarks>
/// This used <c>IMemoryCache</c>, which is per-process. Correct for a single instance, wrong the
/// moment the app scales: a category edit on one instance left every other instance serving its own
/// stale copy until the entry expired, so the same request could return different answers depending
/// on which instance handled it. Nothing would look broken - it would just intermittently show the
/// wrong categories, which is the kind of bug nobody manages to reproduce.
///
/// It now uses <see cref="IDistributedCache"/>. The backing store is Redis when
/// <c>ConnectionStrings:Redis</c> is configured and an in-memory implementation of the same
/// interface otherwise (see Program.cs), so local development, CI and the test suite do not need a
/// Redis server to run. The cache key was already tenant-scoped, which matters more now that the
/// store can be shared between instances.
/// </remarks>
public sealed class CategoryService(
    PosFlowDbContext dbContext,
    ICurrentUser currentUser,
    IDistributedCache cache)
    : ICategoryService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly PosFlowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDistributedCache _cache = cache;

    private string CacheKey => $"categories:{_currentUser.TenantId}";

    public async Task<IReadOnlyList<CategoryResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetStringAsync(CacheKey, cancellationToken);

        if (cached is not null)
        {
            var deserialized = JsonSerializer.Deserialize<List<CategoryResponse>>(cached);

            if (deserialized is not null)
            {
                return deserialized;
            }

            // A payload that will not deserialize - an older response shape left behind in a shared
            // Redis, say - is treated as a miss and overwritten below, rather than throwing on
            // every read until someone flushes the cache by hand.
        }

        var categories = await _dbContext.ProductCategories
            .AsNoTracking()
            .Where(x => x.TenantId == _currentUser.TenantId)
            .OrderBy(x => x.NameAr)
            .ToListAsync(cancellationToken);

        var result = categories
            .Select(MapResponse)
            .ToList();

        await _cache.SetStringAsync(
            CacheKey,
            JsonSerializer.Serialize(result),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheDuration },
            cancellationToken);

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
        await _cache.RemoveAsync(CacheKey, cancellationToken);

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
        await _cache.RemoveAsync(CacheKey, cancellationToken);

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
        await _cache.RemoveAsync(CacheKey, cancellationToken);
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
