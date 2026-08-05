using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosFlow.Application.Common;
using PosFlow.Application.Orders;

namespace PosFlow.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/orders")]
public sealed class OrdersController(
    IOrderService orderService)
    : ControllerBase
{
    private readonly IOrderService _orderService = orderService;

    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderResponse>>>
        GetCurrentShiftOrders(
            [FromQuery] int page,
            [FromQuery] int pageSize,
            CancellationToken cancellationToken)
    {
        var orders = await _orderService.GetCurrentShiftOrdersAsync(
            page == 0 ? 1 : page,
            pageSize == 0 ? Paging.DefaultPageSize : pageSize,
            cancellationToken);

        return Ok(orders);
    }

    [HttpGet("by-shift/{shiftId:guid}")]
    public async Task<ActionResult<PagedResult<OrderResponse>>> GetByShift(
        Guid shiftId,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        var orders = await _orderService.GetByShiftIdAsync(
            shiftId,
            page == 0 ? 1 : page,
            pageSize == 0 ? Paging.DefaultPageSize : pageSize,
            cancellationToken);

        return Ok(orders);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var order = await _orderService.GetByIdAsync(
            id,
            cancellationToken);

        if (order is null)
        {
            return NotFound(new
            {
                message = "الفاتورة غير موجودة."
            });
        }

        return Ok(order);
    }

    [HttpPost("checkout")]
    public async Task<ActionResult<OrderResponse>> Checkout(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = await _orderService.CheckoutAsync(
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = order.Id },
            order);
    }

    [HttpPost("{id:guid}/void")]
    public async Task<ActionResult<OrderResponse>> Void(
        Guid id,
        VoidOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = await _orderService.VoidAsync(
            id,
            request,
            cancellationToken);

        return Ok(order);
    }
}
