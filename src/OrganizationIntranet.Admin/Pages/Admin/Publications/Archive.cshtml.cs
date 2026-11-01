using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Admin.Publications;

/// <summary>صفحه‌ی مطالب بایگانی‌شده: جست‌وجو، صفحه‌بندی و بازگردانی. بازگردانی مطلب را به پیش‌نویس برمی‌گرداند.</summary>
[Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.PublicationEditPolicy)]
public class ArchiveModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Kind { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public PublicationListDto Result { get; set; } = new([], 0, 1, 20);

    public async Task OnGetAsync() =>
        Result = await api.GetAsync<PublicationListDto>($"api/admin/publications?page={PageNumber}&kind={Uri.EscapeDataString(Kind ?? "")}&search={Uri.EscapeDataString(Search ?? "")}&status=archived");

    public async Task<IActionResult> OnPostRestoreAsync(long id)
    {
        await api.PostAsync<PublicationDto>($"api/admin/publications/{id}/restore");
        TempData["SuccessMessage"] = "مطلب بازگردانی شد و به صورت پیش‌نویس است؛ برای نمایش در پورتال آن را ویرایش و منتشر کنید.";
        return RedirectToPage(new { Kind, Search, PageNumber });
    }
}
