using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.ViewComponents;

public class AccessRequestBellViewComponent(ApiClient api) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        // در همه‌ی صفحه‌های پوسته‌ی مدیریت (از جمله پروفایل) برای کسی که درخواست‌های دسترسی را بررسی می‌کند نمایش داده می‌شود.
        if (!OrganizationIntranet.Authorization.PermissionClaimsExtensions.HasPermission(UserClaimsPrincipal, OrganizationIntranet.Contracts.AdminPermissions.AccessRequestReview)) return Content("");
        return View(await api.GetAsync<AccessRequestAlertsDto>("api/portal/supervision/alerts"));
    }
}
