using PosFlow.Application.Common;

namespace PosFlow.Application.Shifts;

public interface IShiftService
{
    Task<ShiftResponse?> GetCurrentAsync(
        CancellationToken cancellationToken = default);

    Task<PagedResult<ShiftResponse>> GetHistoryAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Branch-wide shift history across all cashiers. Intended for
    /// Admin/Manager roles - enforce the role check at the controller.
    /// </summary>
    Task<PagedResult<ShiftResponse>> GetBranchHistoryAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ShiftResponse> OpenAsync(
        OpenShiftRequest request,
        CancellationToken cancellationToken = default);

    Task<ShiftResponse> CloseAsync(
        Guid shiftId,
        CloseShiftRequest request,
        CancellationToken cancellationToken = default);
}
