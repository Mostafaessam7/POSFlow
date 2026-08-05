using PosFlow.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosFlow.Application.Categories;

namespace PosFlow.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(
    ICategoryService categoryService)
    : ControllerBase
{
    private readonly ICategoryService _categoryService = categoryService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var categories = await _categoryService.GetAllAsync(
            cancellationToken);

        return Ok(categories);
    }

    [Authorize(Roles = Roles.AdminOrManager)]
    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(
        CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await _categoryService.CreateAsync(
            request,
            cancellationToken);

        return Ok(category);
    }

    [Authorize(Roles = Roles.AdminOrManager)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CategoryResponse>> Update(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await _categoryService.UpdateAsync(
            id,
            request,
            cancellationToken);

        return Ok(category);
    }

    [Authorize(Roles = Roles.AdminOrManager)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _categoryService.DeleteAsync(
            id,
            cancellationToken);

        return NoContent();
    }
}
