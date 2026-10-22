using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace OrganizationIntranet.Authorization;

/// <summary>
/// بررسی مجوز در UI (Portal/Admin) از روی Claimهای کوکی. Claimها هنگام ورود ساخته و هر چند دقیقه از API همگام می‌شوند (UiHost)؛
/// تصمیم نهایی همیشه در API و از روی داده‌ی زنده‌ی بانک گرفته می‌شود.
/// </summary>
public sealed class ClaimsPermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasPermission(requirement.PermissionCode)) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

public static class PermissionClaimsExtensions
{
    public static bool HasPermission(this ClaimsPrincipal? user, string code) =>
        user?.Identity?.IsAuthenticated == true && user.HasClaim(PermissionClaimTypes.Permission, code);
}
