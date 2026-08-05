using Microsoft.EntityFrameworkCore;
using PosFlow.Application.Common;
using PosFlow.Application.Reports;
using PosFlow.Domain.Entities;
using PosFlow.Infrastructure.Persistence;

namespace PosFlow.Infrastructure.Reports;

public sealed class ReportService(
    PosFlowDbContext dbContext,
    ICurrentUser currentUser)
    : IReportService
{
    private readonly PosFlowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<DailySummaryResponse> GetDailySummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;

        var branchOrdersToday = _dbContext.Orders
            .AsNoTracking()
            .Where(
                x =>
                    x.TenantId == _currentUser.TenantId &&
                    x.BranchId == _currentUser.BranchId &&
                    x.CreatedAtUtc >= today);

        var completedOrderIds = await branchOrdersToday
            .Where(x => x.Status == OrderStatus.Completed)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var voidedOrderCount = await branchOrdersToday
            .CountAsync(
                x => x.Status == OrderStatus.Cancelled,
                cancellationToken);

        var totalSales = await _dbContext.Orders
            .AsNoTracking()
            .Where(x => completedOrderIds.Contains(x.Id))
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0;

        var todaysPayments = await _dbContext.Payments
            .AsNoTracking()
            .Where(p => completedOrderIds.Contains(p.OrderId))
            .ToListAsync(cancellationToken);

        var paymentsByMethod = todaysPayments
            .GroupBy(p => p.Method)
            .Select(g => new { Method = g.Key, Total = g.Sum(p => p.Amount) })
            .ToList();

        var cashSales = paymentsByMethod
            .FirstOrDefault(x => x.Method == PaymentMethod.Cash)?.Total ?? 0;

        var cardSales = paymentsByMethod
            .FirstOrDefault(x => x.Method == PaymentMethod.Card)?.Total ?? 0;

        // Grouped client-side on purpose: a GroupBy + nested aggregate
        // Select (g.Sum(...) inside the projection) doesn't translate
        // reliably across providers once a global query filter is
        // present on the source set (confirmed failing against EF
        // Core InMemory). A day's worth of order lines for one branch
        // is small, so materializing first is cheap and safe.
        var todaysLines = await _dbContext.OrderLines
            .AsNoTracking()
            .Where(l => completedOrderIds.Contains(l.OrderId))
            .ToListAsync(cancellationToken);

        var topProducts = todaysLines
            .GroupBy(l => l.ProductName)
            .Select(g => new TopProductResponse(
                g.Key,
                g.Sum(x => x.Quantity),
                g.Sum(x => x.LineTotal)))
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .ToList();

        return new DailySummaryResponse(
            Date: today,
            OrderCount: completedOrderIds.Count,
            VoidedOrderCount: voidedOrderCount,
            TotalSales: totalSales,
            AverageTicket: completedOrderIds.Count > 0
                ? totalSales / completedOrderIds.Count
                : 0,
            CashSales: cashSales,
            CardSales: cardSales,
            TopProducts: topProducts);
    }
}
