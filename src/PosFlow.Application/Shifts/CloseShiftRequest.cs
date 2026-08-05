namespace PosFlow.Application.Shifts;

public sealed record CloseShiftRequest(
    decimal ClosingCash
);