using Microsoft.EntityFrameworkCore;
using PosFlow.Application.Common;
using PosFlow.Application.Shifts;
using PosFlow.Domain.Entities;
using PosFlow.Infrastructure.Persistence;

namespace PosFlow.Infrastructure.Shifts;

public sealed class ShiftService(
    PosFlowDbContext dbContext,
    ICurrentUser currentUser)
    : IShiftService
{
    private readonly PosFlowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<ShiftResponse?> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        var shift = await _dbContext.Shifts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.TenantId == _currentUser.TenantId &&
                    x.BranchId == _currentUser.BranchId &&
                    x.UserId == _currentUser.UserId &&
                    x.Status == ShiftStatus.Open,
                cancellationToken);

        return shift is null
            ? null
            : MapResponse(shift);
    }

    public async Task<PagedResult<ShiftResponse>> GetHistoryAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (clampedPage, clampedPageSize) = Paging.Clamp(page, pageSize);

        var query = _dbContext.Shifts
            .AsNoTracking()
            .Where(
                x =>
                    x.TenantId == _currentUser.TenantId &&
                    x.BranchId == _currentUser.BranchId &&
                    x.UserId == _currentUser.UserId);

        var totalCount = await query.CountAsync(cancellationToken);

        var shifts = await query
            .OrderByDescending(x => x.OpenedAtUtc)
            .Skip((clampedPage - 1) * clampedPageSize)
            .Take(clampedPageSize)
            .ToListAsync(cancellationToken);

        var items = shifts
            .Select(shift => MapResponse(shift))
            .ToList();

        return new PagedResult<ShiftResponse>(
            items,
            clampedPage,
            clampedPageSize,
            totalCount);
    }

    public async Task<PagedResult<ShiftResponse>> GetBranchHistoryAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (clampedPage, clampedPageSize) = Paging.Clamp(page, pageSize);

        var query = _dbContext.Shifts
            .AsNoTracking()
            .Where(
                x =>
                    x.TenantId == _currentUser.TenantId &&
                    x.BranchId == _currentUser.BranchId);

        var totalCount = await query.CountAsync(cancellationToken);

        var shifts = await query
            .OrderByDescending(x => x.OpenedAtUtc)
            .Skip((clampedPage - 1) * clampedPageSize)
            .Take(clampedPageSize)
            .Join(
                _dbContext.Users.AsNoTracking(),
                shift => shift.UserId,
                user => user.Id,
                (shift, user) => new { shift, user.DisplayName })
            .ToListAsync(cancellationToken);

        var items = shifts
            .Select(x => MapResponse(x.shift, x.DisplayName))
            .ToList();

        return new PagedResult<ShiftResponse>(
            items,
            clampedPage,
            clampedPageSize,
            totalCount);
    }

    public async Task<ShiftResponse> OpenAsync(
        OpenShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.OpeningCash < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.OpeningCash),
                "رصيد بداية الوردية لا يمكن أن يكون أقل من صفر.");
        }

        var existingShift = await _dbContext.Shifts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.TenantId == _currentUser.TenantId &&
                    x.BranchId == _currentUser.BranchId &&
                    x.UserId == _currentUser.UserId &&
                    x.Status == ShiftStatus.Open,
                cancellationToken);

        if (existingShift is not null)
        {
            throw new InvalidOperationException(
                "لديك وردية مفتوحة بالفعل.");
        }

        var shift = new Shift
        {
            TenantId = _currentUser.TenantId,
            BranchId = _currentUser.BranchId,
            UserId = _currentUser.UserId,

            OpeningCash = request.OpeningCash,
            CashSales = 0,

            OpenedAtUtc = DateTime.UtcNow,
            Status = ShiftStatus.Open
        };

        _dbContext.Shifts.Add(shift);

        try
        {
            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException(
                "تعذر فتح الوردية. قد تكون هناك وردية مفتوحة بالفعل.");
        }

        return MapResponse(shift);
    }

    public async Task<ShiftResponse> CloseAsync(
        Guid shiftId,
        CloseShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ClosingCash < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.ClosingCash),
                "النقدية الفعلية لا يمكن أن تكون أقل من صفر.");
        }

        var shift = await _dbContext.Shifts
            .SingleOrDefaultAsync(
                x =>
                    x.Id == shiftId &&
                    x.TenantId == _currentUser.TenantId &&
                    x.BranchId == _currentUser.BranchId &&
                    x.UserId == _currentUser.UserId,
                cancellationToken);

        if (shift is null)
        {
            throw new KeyNotFoundException(
                "الوردية غير موجودة.");
        }

        if (shift.Status == ShiftStatus.Closed)
        {
            throw new InvalidOperationException(
                "الوردية مغلقة بالفعل.");
        }

        var cashSales = await _dbContext.Payments
            .Where(payment =>
                payment.TenantId == _currentUser.TenantId &&
                payment.Method == PaymentMethod.Cash &&
                payment.Order.ShiftId == shift.Id &&
                payment.Order.Status == OrderStatus.Completed)
            .SumAsync(
                payment => (decimal?)payment.Amount,
                cancellationToken)
            ?? 0m;

        var expectedCash =
            shift.OpeningCash + cashSales;

        var cashDifference =
            request.ClosingCash - expectedCash;

        shift.CashSales = cashSales;
        shift.ExpectedCash = expectedCash;
        shift.ClosingCash = request.ClosingCash;
        shift.CashDifference = cashDifference;
        shift.ClosedAtUtc = DateTime.UtcNow;
        shift.Status = ShiftStatus.Closed;
        shift.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return MapResponse(shift);
    }

    private static ShiftResponse MapResponse(
        Shift shift,
        string? cashierName = null)
    {
        return new ShiftResponse(
            Id: shift.Id,
            TenantId: shift.TenantId,
            BranchId: shift.BranchId,
            UserId: shift.UserId,
            OpeningCash: shift.OpeningCash,
            ClosingCash: shift.ClosingCash,
            CashSales: shift.CashSales,
            ExpectedCash: shift.ExpectedCash,
            CashDifference: shift.CashDifference,
            OpenedAtUtc: shift.OpenedAtUtc,
            ClosedAtUtc: shift.ClosedAtUtc,
            Status: shift.Status.ToString(),
            CashierName: cashierName);
    }
}