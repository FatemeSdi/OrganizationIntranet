using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Tests;

/// <summary>داده‌ی پایه‌ی آزمون‌ها، هم‌ارز migration 008: مجوزهای سیستمی پنل مدیریت برای نقش ADMIN.</summary>
internal static class TestSeed
{
    public static void GrantAdminPanel(AppDbContext db, Role adminRole, Domain.Entities.Application owner)
    {
        foreach (var (code, name) in AdminPermissions.All)
            adminRole.RolePermissions.Add(new RolePermission { Permission = new Permission { Application = owner, PermissionCode = code, PermissionName = name, IsActive = true } });
    }
}
