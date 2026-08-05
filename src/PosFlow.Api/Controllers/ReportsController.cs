using PosFlow.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosFlow.Application.Common;
using PosFlow.Application.Reports;

namespace PosFlow.Api.Controllers;

[Authorize(Policy = Permissions.ReportsView)]
[ApiController]
[Route("api/reports")]
public sealed class ReportsController(
    IReportService reportService)
    : ControllerBase
{
    private readonly IReportService _reportService = reportService;

    [HttpGet("daily-summary")]
    public async Task<ActionResult<DailySummaryResponse>> GetDailySummary(
        CancellationToken cancellationToken)
    {
        var summary = await _reportService.GetDailySummaryAsync(
            cancellationToken);

        return Ok(summary);
    }
}
