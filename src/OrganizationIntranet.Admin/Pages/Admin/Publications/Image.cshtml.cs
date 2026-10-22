using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Admin.Publications;
// پیش‌نمایش تصویر شاخص در پنل مدیریت (شامل پیش‌نویس‌ها).
[Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.PublicationEditPolicy)]
public class ImageModel(ApiClient api) : PageModel
{
    public Task<IActionResult> OnGetAsync(long id) => PublicationImageResult.FromApiAsync(api, $"api/admin/publications/{id}/image", Response);
}
