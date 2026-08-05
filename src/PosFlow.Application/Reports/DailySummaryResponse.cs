namespace PosFlow.Application.Reports;

public sealed record TopProductResponse(
    string ProductName,
    decimal QuantitySold,
    decimal Revenue
);

public sealed record DailySummaryResponse(
    DateTime Date,
    int OrderCount,
    int VoidedOrderCount,
    decimal TotalSales,
    decimal AverageTicket,
    decimal CashSales,
    decimal CardSales,
    IReadOnlyList<TopProductResponse> TopProducts
);
