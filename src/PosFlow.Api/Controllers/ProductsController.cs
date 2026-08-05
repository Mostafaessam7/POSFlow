using PosFlow.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosFlow.Application.Common;
using PosFlow.Application.Products;

namespace PosFlow.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/products")]
public sealed class ProductsController(
    IProductService productService)
    : ControllerBase
{
    private readonly IProductService _productService = productService;

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductResponse>>> GetAll(
        [FromQuery] bool includeInactive,
        [FromQuery] Guid? categoryId,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        var products = await _productService.GetAllAsync(
            includeInactive,
            categoryId,
            page == 0 ? 1 : page,
            pageSize == 0 ? Paging.DefaultPageSize : pageSize,
            cancellationToken);

        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var product = await _productService.GetByIdAsync(
            id,
            cancellationToken);

        if (product is null)
        {
            return NotFound(new
            {
                message = "المنتج غير موجود."
            });
        }

        return Ok(product);
    }

    [Authorize(Roles = Roles.AdminOrManager)]
    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _productService.CreateAsync(
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = product.Id },
            product);
    }

    [Authorize(Roles = Roles.AdminOrManager)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> Update(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _productService.UpdateAsync(
            id,
            request,
            cancellationToken);

        return Ok(product);
    }

    [Authorize(Roles = Roles.AdminOrManager)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _productService.DeactivateAsync(
            id,
            cancellationToken);

        return NoContent();
    }
}
