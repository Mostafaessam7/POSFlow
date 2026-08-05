using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using PosFlow.Application.Common;

namespace PosFlow.Api.Authorization;

public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var role = context.User.FindFirstValue(ClaimTypes.Role);

        if (role is not null &&
            RolePermissions.RoleHasPermission(role, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
