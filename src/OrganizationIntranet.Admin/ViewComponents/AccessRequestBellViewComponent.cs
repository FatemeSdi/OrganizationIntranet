using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.ViewComponents;

public class AccessRequestBellViewComponent(ApiClient api) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        // در همه‌ی صفحه‌های پوسته‌ی مدیریت (از جمله پروفایل) برای مدیر نمایش داده می‌شود؛ قبلاً فقط زیر /Admin بود و در پروفایل ناپدید می‌شد.
        if (!UserClaimsPrincipal.IsInRole("ADMIN")) return Content("");
        return View(await api.GetAsync<AccessRequestAlertsDto>("api/portal/supervision/alerts"));
    }
}
