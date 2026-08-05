namespace PosFlow.Application.Shifts;

public sealed record ShiftResponse(
    Guid Id,
    Guid TenantId,
    Guid BranchId,
    Guid UserId,
    decimal OpeningCash,
    decimal? ClosingCash,
    decimal CashSales,
    decimal? ExpectedCash,
    decimal? CashDifference,
    DateTime OpenedAtUtc,
    DateTime? ClosedAtUtc,
    string Status,
    string? CashierName = null
);