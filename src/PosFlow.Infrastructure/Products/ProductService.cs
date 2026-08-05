using Microsoft.EntityFrameworkCore;
using PosFlow.Application.Common;
using PosFlow.Application.Products;
using PosFlow.Domain.Entities;
using PosFlow.Infrastructure.Persistence;

namespace PosFlow.Infrastructure.Products;

public sealed class ProductService(
    PosFlowDbContext dbContext,
    ICurrentUser currentUser)
    : IProductService
{
    private readonly PosFlowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<PagedResult<ProductResponse>> GetAllAsync(
        bool includeInactive,
        Guid? categoryId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (clampedPage, clampedPageSize) =
            Paging.Clamp(page, pageSize);

        var query = _dbContext.Products
            .AsNoTracking()
            .Where(x => x.TenantId == _currentUser.TenantId);

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == categoryId);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var products = await query
            .OrderBy(x => x.NameAr)
            .Skip((clampedPage - 1) * clampedPageSize)
            .Take(clampedPageSize)
            .ToListAsync(cancellationToken);

        var categoryIds = products
            .Where(x => x.CategoryId.HasValue)
            .Select(x => x.CategoryId!.Value)
            .Distinct()
            .ToList();

        var categoryNames = categoryIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _dbContext.ProductCategories
                .AsNoTracking()
                .Where(x => categoryIds.Contains(x.Id))
                .ToDictionaryAsync(
                    x => x.Id,
                    x => x.NameAr,
                    cancellationToken);

        var items = products
            .Select(product => MapResponse(
                product,
                product.CategoryId.HasValue &&
                categoryNames.TryGetValue(
                    product.CategoryId.Value,
                    out var categoryName)
                    ? categoryName
                    : null))
            .ToList();

        return new PagedResult<ProductResponse>(
            Items: items,
            Page: clampedPage,
            PageSize: clampedPageSize,
            TotalCount: totalCount);
    }

    public async Task<ProductResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.Id == id &&
                    x.TenantId == _currentUser.TenantId,
                cancellationToken);

        if (product is null)
        {
            return null;
        }

        var categoryName = await GetCategoryNameAsync(
            product.CategoryId,
            cancellationToken);

        return MapResponse(product, categoryName);
    }

    public async Task<ProductResponse> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request.Barcode))
        {
            var barcodeTaken = await _dbContext.Products
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.TenantId == _currentUser.TenantId &&
                        x.Barcode == request.Barcode,
                    cancellationToken);

            if (barcodeTaken)
            {
                throw new InvalidOperationException(
                    "يوجد منتج آخر بنفس الباركود.");
            }
        }

        await EnsureCategoryBelongsToTenantAsync(
            request.CategoryId,
            cancellationToken);

        var product = new Product
        {
            TenantId = _currentUser.TenantId,
            CategoryId = request.CategoryId,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            Barcode = request.Barcode,
            Price = request.Price,
            TrackStock = request.TrackStock,
            StockQuantity = request.StockQuantity,
            IsActive = true
        };

        _dbContext.Products.Add(product);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var categoryName = await GetCategoryNameAsync(
            product.CategoryId,
            cancellationToken);

        return MapResponse(product, categoryName);
    }

    public async Task<ProductResponse> UpdateAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .SingleOrDefaultAsync(
                x =>
                    x.Id == id &&
                    x.TenantId == _currentUser.TenantId,
                cancellationToken);

        if (product is null)
        {
            throw new KeyNotFoundException(
                "المنتج غير موجود.");
        }

        if (!string.IsNullOrWhiteSpace(request.Barcode))
        {
            var barcodeTaken = await _dbContext.Products
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.TenantId == _currentUser.TenantId &&
                        x.Barcode == request.Barcode &&
                        x.Id != id,
                    cancellationToken);

            if (barcodeTaken)
            {
                throw new InvalidOperationException(
                    "يوجد منتج آخر بنفس الباركود.");
            }
        }

        await EnsureCategoryBelongsToTenantAsync(
            request.CategoryId,
            cancellationToken);

        // Optimistic concurrency: the client sends back the RowVersion
        // it last read. If someone else saved a change in between,
        // EF raises DbUpdateConcurrencyException on SaveChanges.
        byte[] originalRowVersion;

        try
        {
            originalRowVersion = Convert.FromBase64String(request.RowVersion);
        }
        catch (FormatException)
        {
            throw new ArgumentException(
                "بيانات النسخة غير صالحة.",
                nameof(request.RowVersion));
        }

        // Explicit check first, in addition to the provider-level one
        // below: the EF Core InMemory provider (used by our unit
        // tests) does not reliably raise DbUpdateConcurrencyException
        // from an OriginalValue mismatch the way a real relational
        // provider does, so relying on that alone left stale updates
        // silently succeeding under test. This makes the guarantee
        // explicit and provider-agnostic; the OriginalValue set below
        // still protects against a real race against SQL Server that
        // happens between this check and SaveChanges.
        if (!product.RowVersion.SequenceEqual(originalRowVersion))
        {
            throw new InvalidOperationException(
                "تم تعديل هذا المنتج من شخص آخر في نفس الوقت. من فضلك أعد تحميل الصفحة وحاول مرة أخرى.");
        }

        _dbContext.Entry(product)
            .Property(x => x.RowVersion)
            .OriginalValue = originalRowVersion;

        product.NameAr = request.NameAr;
        product.NameEn = request.NameEn;
        product.Barcode = request.Barcode;
        product.Price = request.Price;
        product.IsActive = request.IsActive;
        product.CategoryId = request.CategoryId;
        product.TrackStock = request.TrackStock;
        product.StockQuantity = request.StockQuantity;
        product.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                "تم تعديل هذا المنتج من شخص آخر في نفس الوقت. من فضلك أعد تحميل الصفحة وحاول مرة أخرى.");
        }

        var categoryName = await GetCategoryNameAsync(
            product.CategoryId,
            cancellationToken);

        return MapResponse(product, categoryName);
    }

    public async Task DeactivateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .SingleOrDefaultAsync(
                x =>
                    x.Id == id &&
                    x.TenantId == _currentUser.TenantId,
                cancellationToken);

        if (product is null)
        {
            throw new KeyNotFoundException(
                "المنتج غير موجود.");
        }

        product.IsActive = false;
        product.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureCategoryBelongsToTenantAsync(
        Guid? categoryId,
        CancellationToken cancellationToken)
    {
        if (!categoryId.HasValue)
        {
            return;
        }

        var categoryExists = await _dbContext.ProductCategories
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.Id == categoryId &&
                    x.TenantId == _currentUser.TenantId,
                cancellationToken);

        if (!categoryExists)
        {
            throw new KeyNotFoundException(
                "التصنيف المحدد غير موجود.");
        }
    }

    private async Task<string?> GetCategoryNameAsync(
        Guid? categoryId,
        CancellationToken cancellationToken)
    {
        if (!categoryId.HasValue)
        {
            return null;
        }

        return await _dbContext.ProductCategories
            .AsNoTracking()
            .Where(x => x.Id == categoryId)
            .Select(x => x.NameAr)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static ProductResponse MapResponse(
        Product product,
        string? categoryName)
    {
        return new ProductResponse(
            Id: product.Id,
            NameAr: product.NameAr,
            NameEn: product.NameEn,
            Barcode: product.Barcode,
            Price: product.Price,
            IsActive: product.IsActive,
            CategoryId: product.CategoryId,
            CategoryName: categoryName,
            TrackStock: product.TrackStock,
            StockQuantity: product.StockQuantity,
            RowVersion: Convert.ToBase64String(product.RowVersion));
    }
}
