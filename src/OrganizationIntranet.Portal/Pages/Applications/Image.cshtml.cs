using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Applications;

/// <summary>تصویر کارت سامانه در پورتال: ناشناس فقط تصویر سامانه‌ی عمومی و کاربر واردشده تصویر سامانه‌ای را می‌گیرد که در دسترس اوست؛ تفکیک در API است. با nosniff و کش خصوصی.</summary>
[AllowAnonymous]
public class ImageModel(ApiClient api) : PageModel
{
    public Task<IActionResult> OnGetAsync(int id) => PublicationImageResult.FromApiAsync(api,
        User.Identity?.IsAuthenticated == true ? $"api/portal/applications/{id}/image" : $"api/public/applications/{id}/image", Response);
}
