using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PosFlow.Application.Common;

namespace PosFlow.Infrastructure.Authentication;

public sealed class CurrentTenantProvider(
    IHttpContextAccessor httpContextAccessor)
    : ICurrentTenantProvider
{
    public Guid? TenantId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User
                .FindFirstValue("tenant_id");

            return Guid.TryParse(value, out var id)
                ? id
                : null;
        }
    }
}
