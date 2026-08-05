namespace PosFlow.Application.Branches;

public sealed record UpdateBranchRequest(
    string Name,
    string Code,
    bool IsActive
);
