namespace PosFlow.Application.Common;

public interface ICurrentUser
{
    Guid UserId { get; }

    Guid TenantId { get; }

    Guid BranchId { get; }

    string Role { get; }
}