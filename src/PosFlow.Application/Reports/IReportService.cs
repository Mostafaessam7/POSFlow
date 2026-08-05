namespace PosFlow.Application.Reports;

public interface IReportService
{
    Task<DailySummaryResponse> GetDailySummaryAsync(
        CancellationToken cancellationToken = default);
}
