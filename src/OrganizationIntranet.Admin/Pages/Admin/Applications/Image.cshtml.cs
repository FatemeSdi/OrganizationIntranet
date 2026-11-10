using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Admin.Applications;

/// <summary>تصویر کارت سامانه برای پیش‌نمایش مدیر در فرم ویرایش؛ از API می‌گیرد و با nosniff می‌دهد. مجوز APPLICATION_VIEW.</summary>
[Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.ApplicationViewPolicy)]
public class ImageModel(ApiClient api) : PageModel
{
    public Task<IActionResult> OnGetAsync(int id) => PublicationImageResult.FromApiAsync(api, $"api/admin/applications/{id}/image", Response);
}
