using PosFlow.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosFlow.Application.Common;
using PosFlow.Application.Branches;

namespace PosFlow.Api.Controllers;

[Authorize(Policy = Permissions.BranchesManage)]
[ApiController]
[Route("api/branches")]
public sealed class BranchesController(
    IBranchService branchService)
    : ControllerBase
{
    private readonly IBranchService _branchService = branchService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BranchResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var branches = await _branchService.GetAllAsync(
            cancellationToken);

        return Ok(branches);
    }

    [HttpPost]
    public async Task<ActionResult<BranchResponse>> Create(
        CreateBranchRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branchService.CreateAsync(
            request,
            cancellationToken);

        return Ok(branch);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BranchResponse>> Update(
        Guid id,
        UpdateBranchRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branchService.UpdateAsync(
            id,
            request,
            cancellationToken);

        return Ok(branch);
    }
}
