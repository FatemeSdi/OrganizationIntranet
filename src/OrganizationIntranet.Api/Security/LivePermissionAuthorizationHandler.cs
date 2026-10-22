using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Authorization;

namespace OrganizationIntranet.Api.Security;

/// <summary>
/// مجوزهای فعلی کاربر از بانک (نه Claimهای توکن): نقش و مجوز غیرفعال یا گرفته‌شده بلافاصله اثر می‌کند و توکن‌های قدیمی هم معتبر می‌مانند.
/// نتیجه در هر درخواست یک‌بار محاسبه و در HttpContext نگه داشته می‌شود.
/// </summary>
public sealed class LivePermissions(IIntranetRepository users, IHttpContextAccessor accessor)
{
    private const string CacheKey = "OrganizationIntranet.LivePermissions";
    public async Task<IReadOnlySet<string>> ForAsync(ClaimsPrincipal principal)
    {
        var items = accessor.HttpContext?.Items;
        if (items?[CacheKey] is IReadOnlySet<string> cached) return cached;
        IReadOnlySet<string> codes = new HashSet<string>();
        if (long.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && await users.FindUserAsync(id) is { IsActive: true } user)
            codes = user.UserRoles.Where(r => r.Role.IsActive).SelectMany(r => r.Role.RolePermissions)
                .Where(p => p.Permission.IsActive).Select(p => p.Permission.PermissionCode).ToHashSet(StringComparer.Ordinal);
        if (items is not null) items[CacheKey] = codes;
        return codes;
    }
}

public sealed class LivePermissionAuthorizationHandler(LivePermissions permissions) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true && (await permissions.ForAsync(context.User)).Contains(requirement.PermissionCode))
            context.Succeed(requirement);
    }
}
