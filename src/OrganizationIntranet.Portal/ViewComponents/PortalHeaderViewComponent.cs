using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.ViewComponents;

/// <summary>هدر صفحه‌های داخلی پورتال (_LayoutPortal): نام کاربر، منوی حساب و شمار اعلان‌های خوانده‌نشده؛ برای بازدیدکننده‌ی ناشناس فقط دکمه‌ی ورود.</summary>
public sealed record PortalHeaderModel(bool IsAuthenticated, string DisplayName, string Initials, string RoleNames, bool IsAdmin, int Unread, string? Active, string ReturnUrl);

public class PortalHeaderViewComponent(ApiClient api) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(string? active)
    {
        var returnUrl = HttpContext.Request.Path + HttpContext.Request.QueryString;
        if (User.Identity?.IsAuthenticated != true)
            return View(new PortalHeaderModel(false, "", "", "", false, 0, active, returnUrl));
        var profile = await api.GetAsync<ProfileDto>("api/account/me");
        var notifications = await api.GetAsync<List<PortalNotificationDto>>("api/portal/notifications");
        var alerts = await api.GetAsync<AccessRequestAlertsDto>("api/portal/supervision/alerts");
        return View(new PortalHeaderModel(true, profile.DisplayName, profile.Initials, profile.RoleNames, UserClaimsPrincipal.IsInRole("ADMIN"),
            notifications.Count(n => n.Unread) + alerts.UnreadCount, active, returnUrl));
    }
}
