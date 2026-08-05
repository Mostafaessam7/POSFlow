using PosFlow.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosFlow.Application.Common;
using PosFlow.Application.Shifts;

namespace PosFlow.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/shifts")]
public sealed class ShiftsController(
    IShiftService shiftService)
    : ControllerBase
{
    private readonly IShiftService _shiftService =
        shiftService;

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(
        CancellationToken cancellationToken)
    {
        var shift = await _shiftService.GetCurrentAsync(
            cancellationToken);

        return Ok(new
        {
            hasOpenShift = shift is not null,
            shift
        });
    }

    [HttpGet("history")]
    public async Task<ActionResult<PagedResult<ShiftResponse>>>
        GetHistory(
            [FromQuery] int page,
            [FromQuery] int pageSize,
            CancellationToken cancellationToken)
    {
        var shifts = await _shiftService.GetHistoryAsync(
            page == 0 ? 1 : page,
            pageSize == 0 ? 30 : pageSize,
            cancellationToken);

        return Ok(shifts);
    }

    [Authorize(Roles = Roles.AdminOrManager)]
    [HttpGet("branch-history")]
    public async Task<ActionResult<PagedResult<ShiftResponse>>>
        GetBranchHistory(
            [FromQuery] int page,
            [FromQuery] int pageSize,
            CancellationToken cancellationToken)
    {
        var shifts = await _shiftService.GetBranchHistoryAsync(
            page == 0 ? 1 : page,
            pageSize == 0 ? 50 : pageSize,
            cancellationToken);

        return Ok(shifts);
    }

    [HttpPost("open")]
    public async Task<ActionResult<ShiftResponse>>
        Open(
            OpenShiftRequest request,
            CancellationToken cancellationToken)
    {
        var shift =
            await _shiftService.OpenAsync(
                request,
                cancellationToken);

        return Ok(shift);
    }

    [HttpPost("{shiftId:guid}/close")]
    public async Task<ActionResult<ShiftResponse>>
        Close(
            Guid shiftId,
            CloseShiftRequest request,
            CancellationToken cancellationToken)
    {
        var shift =
            await _shiftService.CloseAsync(
                shiftId,
                request,
                cancellationToken);

        return Ok(shift);
    }
}
