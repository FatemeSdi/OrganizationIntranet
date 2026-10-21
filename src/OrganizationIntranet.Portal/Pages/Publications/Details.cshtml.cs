using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Publications;
// برای ناشناس، مطلب غیرعمومی یا پیش‌نویس «یافت نشد» (۴۰۴) است؛ وجود آن لو نمی‌رود.
[AllowAnonymous]
public class DetailsModel(ApiClient api) : PageModel
{
    public PublicationDto Item { get; private set; } = null!;
    /// <summary>چند مطلب تازه‌ی دیگر از همان دامنه‌ی دید (برای ستون «مطالب دیگر»).</summary>
    public List<PublicationDto> Related { get; private set; } = new();
    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;
    public async Task OnGetAsync(long id)
    {
        var scope = IsAuthenticated ? "api/publications" : "api/public/publications";
        Item = await api.GetAsync<PublicationDto>($"{scope}/{id}");
        Related = (await api.GetAsync<PublicationListDto>($"{scope}?page=1")).Items.Where(p => p.PublicationId != id).Take(4).ToList();
    }
}
