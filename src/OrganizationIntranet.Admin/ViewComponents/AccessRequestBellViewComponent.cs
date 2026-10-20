using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.ViewComponents;

public class AccessRequestBellViewComponent(ApiClient api) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!HttpContext.Request.Path.StartsWithSegments("/Admin")) return Content("");
        return View(await api.GetAsync<AccessRequestAlertsDto>("api/admin/supervision/alerts"));
    }
}
