namespace PosFlow.Domain.Entities;

public enum ShiftStatus
{
    Open = 1,
    Closed = 2
}

public sealed class Shift : BaseEntity
{
    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid UserId { get; set; }

    public decimal OpeningCash { get; set; }

    public decimal? ClosingCash { get; set; }

    public decimal CashSales { get; set; }

    public decimal? ExpectedCash { get; set; }

    public decimal? CashDifference { get; set; }

    public DateTime OpenedAtUtc { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public ShiftStatus Status { get; set; } = ShiftStatus.Open;
}