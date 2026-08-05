using PosFlow.Application.Common;

namespace PosFlow.Application.Orders;

public interface IOrderService
{
    Task<OrderResponse> CheckoutAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<OrderResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<OrderResponse>> GetByShiftIdAsync(
        Guid shiftId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedResult<OrderResponse>> GetCurrentShiftOrdersAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<OrderResponse> VoidAsync(
        Guid id,
        VoidOrderRequest request,
        CancellationToken cancellationToken = default);
}
