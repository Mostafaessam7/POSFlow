namespace PosFlow.Application.Branches;

public interface IBranchService
{
    Task<IReadOnlyList<BranchResponse>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<BranchResponse> CreateAsync(
        CreateBranchRequest request,
        CancellationToken cancellationToken = default);

    Task<BranchResponse> UpdateAsync(
        Guid id,
        UpdateBranchRequest request,
        CancellationToken cancellationToken = default);
}
