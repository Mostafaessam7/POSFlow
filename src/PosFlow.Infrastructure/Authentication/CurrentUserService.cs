using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PosFlow.Application.Common;

namespace PosFlow.Infrastructure.Authentication;

public sealed class CurrentUserService(
    IHttpContextAccessor httpContextAccessor)
    : ICurrentUser
{
    private ClaimsPrincipal User =>
        httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException(
            "لا يوجد مستخدم مرتبط بالطلب الحالي.");

    public Guid UserId =>
        ReadGuidClaim(ClaimTypes.NameIdentifier);

    public Guid TenantId =>
        ReadGuidClaim("tenant_id");

    public Guid BranchId =>
        ReadGuidClaim("branch_id");

    public string Role =>
        User.FindFirstValue(ClaimTypes.Role)
        ?? throw new InvalidOperationException(
            "لا يوجد دور مرتبط بالتوكن الحالي.");

    private Guid ReadGuidClaim(string claimType)
    {
        var value = User.FindFirstValue(claimType);

        if (!Guid.TryParse(value, out var id))
        {
            throw new InvalidOperationException(
                $"القيمة المطلوبة غير موجودة داخل التوكن: {claimType}");
        }

        return id;
    }
}