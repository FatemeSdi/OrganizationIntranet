using Microsoft.AspNetCore.Authorization;

namespace OrganizationIntranet.Authorization;

/// <summary>نیازمندیِ داشتن یک مجوز با کد مشخص (مثلاً USER_VIEW) برای سیستم مجوزدهی ASP.NET Core.</summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public string PermissionCode { get; }

    public PermissionRequirement(string permissionCode)
    {
        PermissionCode = permissionCode;
    }
}
