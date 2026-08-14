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

    [HttpGet("by-barcode/{barcode}")]
    public async Task<ActionResult<ProductResponse>> GetByBarcode(
        string barcode,
        CancellationToken cancellationToken)
    {
        var product = await _productService.GetByBarcodeAsync(
            barcode,
            cancellationToken);

        if (product is null)
        {
            return NotFound(new
            {
                message = "لا يوجد منتج بهذا الباركود."
            });
        }

        return Ok(product);
    }

    [Authorize(Policy = Permissions.ProductsWrite)]
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

    [Authorize(Policy = Permissions.ProductsWrite)]
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

    [HttpGet("{id:guid}/stock-movements")]
    public async Task<ActionResult<PagedResult<StockMovementResponse>>> GetStockMovements(
        Guid id,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        var movements = await _productService.GetStockMovementsAsync(
            id,
            page == 0 ? 1 : page,
            pageSize == 0 ? Paging.DefaultPageSize : pageSize,
            cancellationToken);

        return Ok(movements);
    }

    [Authorize(Policy = Permissions.ProductsWrite)]
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
