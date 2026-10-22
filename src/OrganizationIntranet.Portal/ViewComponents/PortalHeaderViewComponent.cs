using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.ViewComponents;

/// <summary>هدر صفحه‌های داخلی پورتال (_LayoutPortal): نام کاربر، منوی حساب و شمار اعلان‌های خوانده‌نشده؛ برای بازدیدکننده‌ی ناشناس فقط دکمه‌ی ورود.</summary>
public sealed record PortalHeaderModel(bool IsAuthenticated, string DisplayName, string Initials, string RoleNames, bool IsAdmin, int Unread, string? Active, string ReturnUrl, bool IsSupervisor = false);

public class PortalHeaderViewComponent(ApiClient api) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(string? active)
    {
        var returnUrl = HttpContext.Request.Path + HttpContext.Request.QueryString;
        if (User.Identity?.IsAuthenticated != true)
            return View(new PortalHeaderModel(false, "", "", "", false, 0, active, returnUrl));
        var profile = await api.GetAsync<ProfileDto>("api/account/me");
        // شمار دقیق خوانده‌نشده‌ها از هر دو منبع (نه فقط ۵۰ مورد آخر)
        var unread = await api.GetAsync<NotificationCenterDto>($"api/portal/notifications/page?status={NotificationCenter.Unread}");
        var supervision = await api.GetAsync<SupervisionSummaryDto>("api/portal/supervision/inbox/summary");
        return View(new PortalHeaderModel(true, profile.DisplayName, profile.Initials, profile.RoleNames, OrganizationIntranet.Authorization.PermissionClaimsExtensions.HasPermission(UserClaimsPrincipal, AdminPermissions.IntranetAdmin),
            unread.UnreadCount, active, returnUrl, supervision.IsSupervisor));
    }
}
