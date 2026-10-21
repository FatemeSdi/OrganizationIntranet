using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.ViewComponents;

/// <summary>هدر صفحه‌های داخلی پورتال (_LayoutPortal): نام کاربر، منوی حساب و شمار اعلان‌های خوانده‌نشده.</summary>
public sealed record PortalHeaderModel(string DisplayName, string Initials, string RoleNames, bool IsAdmin, int Unread, string? Active);

public class PortalHeaderViewComponent(ApiClient api) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(string? active)
    {
        var profile = await api.GetAsync<ProfileDto>("api/account/me");
        var notifications = await api.GetAsync<List<PortalNotificationDto>>("api/portal/notifications");
        var alerts = await api.GetAsync<AccessRequestAlertsDto>("api/portal/supervision/alerts");
        return View(new PortalHeaderModel(profile.DisplayName, profile.Initials, profile.RoleNames, UserClaimsPrincipal.IsInRole("ADMIN"),
            notifications.Count(n => n.Unread) + alerts.UnreadCount, active));
    }
}
