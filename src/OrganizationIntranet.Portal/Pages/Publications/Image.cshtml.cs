using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Publications;
// تصویر شاخص: ناشناس فقط تصویر مطلب عمومی و کاربر واردشده تصویر هر مطلب منتشرشده را می‌گیرد؛ تفکیک در API است.
[AllowAnonymous]
public class ImageModel(ApiClient api) : PageModel
{
    public Task<IActionResult> OnGetAsync(long id) => PublicationImageResult.FromApiAsync(api,
        User.Identity?.IsAuthenticated == true ? $"api/publications/{id}/image" : $"api/public/publications/{id}/image", Response);
}
