using PosFlow.Application.Common;

namespace PosFlow.Application.Tests.TestHelpers;

public sealed class FakeCurrentUser : ICurrentUser
{
    public Guid UserId { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; } = Guid.NewGuid();

    public Guid BranchId { get; set; } = Guid.NewGuid();

    public string Role { get; set; } = "Cashier";
}
