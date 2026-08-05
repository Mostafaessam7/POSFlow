namespace PosFlow.Application.Branches;

public sealed record CreateBranchRequest(
    string Name,
    string Code
);
