using Microsoft.EntityFrameworkCore;
using PosFlow.Application.Branches;
using PosFlow.Application.Common;
using PosFlow.Domain.Entities;
using PosFlow.Infrastructure.Persistence;

namespace PosFlow.Infrastructure.Branches;

public sealed class BranchService(
    PosFlowDbContext dbContext,
    ICurrentUser currentUser)
    : IBranchService
{
    private readonly PosFlowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<IReadOnlyList<BranchResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var branches = await _dbContext.Branches
            .AsNoTracking()
            .Where(x => x.TenantId == _currentUser.TenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return branches
            .Select(MapResponse)
            .ToList();
    }

    public async Task<BranchResponse> CreateAsync(
        CreateBranchRequest request,
        CancellationToken cancellationToken = default)
    {
        var codeTaken = await _dbContext.Branches
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.TenantId == _currentUser.TenantId &&
                    x.Code == request.Code,
                cancellationToken);

        if (codeTaken)
        {
            throw new InvalidOperationException(
                "يوجد فرع آخر بنفس الكود.");
        }

        var branch = new Branch
        {
            TenantId = _currentUser.TenantId,
            Name = request.Name,
            Code = request.Code,
            IsActive = true
        };

        _dbContext.Branches.Add(branch);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapResponse(branch);
    }

    public async Task<BranchResponse> UpdateAsync(
        Guid id,
        UpdateBranchRequest request,
        CancellationToken cancellationToken = default)
    {
        var branch = await _dbContext.Branches
            .SingleOrDefaultAsync(
                x =>
                    x.Id == id &&
                    x.TenantId == _currentUser.TenantId,
                cancellationToken);

        if (branch is null)
        {
            throw new KeyNotFoundException(
                "الفرع غير موجود.");
        }

        var codeTaken = await _dbContext.Branches
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.TenantId == _currentUser.TenantId &&
                    x.Code == request.Code &&
                    x.Id != id,
                cancellationToken);

        if (codeTaken)
        {
            throw new InvalidOperationException(
                "يوجد فرع آخر بنفس الكود.");
        }

        branch.Name = request.Name;
        branch.Code = request.Code;
        branch.IsActive = request.IsActive;
        branch.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapResponse(branch);
    }

    private static BranchResponse MapResponse(
        Branch branch)
    {
        return new BranchResponse(
            Id: branch.Id,
            Name: branch.Name,
            Code: branch.Code,
            IsActive: branch.IsActive);
    }
}
