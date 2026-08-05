using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosFlow.Application.Common;
using PosFlow.Application.Customers;

namespace PosFlow.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/customers")]
public sealed class CustomersController(
    ICustomerService customerService)
    : ControllerBase
{
    private readonly ICustomerService _customerService = customerService;

    [HttpGet]
    public async Task<ActionResult<PagedResult<CustomerResponse>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        var customers = await _customerService.GetAllAsync(
            search,
            page == 0 ? 1 : page,
            pageSize == 0 ? Paging.DefaultPageSize : pageSize,
            cancellationToken);

        return Ok(customers);
    }

    [Authorize(Policy = Permissions.CustomersManage)]
    [HttpPost]
    public async Task<ActionResult<CustomerResponse>> Create(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await _customerService.CreateAsync(
            request,
            cancellationToken);

        return Ok(customer);
    }

    [Authorize(Policy = Permissions.CustomersManage)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerResponse>> Update(
        Guid id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await _customerService.UpdateAsync(
            id,
            request,
            cancellationToken);

        return Ok(customer);
    }
}
