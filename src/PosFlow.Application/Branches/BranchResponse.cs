namespace PosFlow.Application.Branches;

public sealed record BranchResponse(
    Guid Id,
    string Name,
    string Code,
    bool IsActive
);
